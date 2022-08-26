using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Esprima.Ast;
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime.Environments;
using Jint.Runtime.References;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintCallExpression : JintExpression
    {
        private CachedArgumentsHolder _cachedArguments = null!;
        private bool _cached;

        private JintExpression _calleeExpression = null!;
        private bool _hasSpreads;

        public JintCallExpression(CallExpression expression) : base(expression)
        {
            _initialized = false;
        }

        protected override void Initialize(EvaluationContext context)
        {
            var engine = context.Engine;
            var expression = (CallExpression) _expression;
            _calleeExpression = Build(engine, expression.Callee);
            var cachedArgumentsHolder = new CachedArgumentsHolder
            {
                JintArguments = new JintExpression[expression.Arguments.Count]
            };

            static bool CanSpread(Node? e)
            {
                return e?.Type == Nodes.SpreadElement || e is AssignmentExpression { Right.Type: Nodes.SpreadElement };
            }

            bool cacheable = true;
            for (var i = 0; i < expression.Arguments.Count; i++)
            {
                var expressionArgument = expression.Arguments[i];
                cachedArgumentsHolder.JintArguments[i] = Build(engine, expressionArgument);
                cacheable &= expressionArgument.Type == Nodes.Literal;
                _hasSpreads |= CanSpread(expressionArgument);
                if (expressionArgument is ArrayExpression ae)
                {
                    for (var elementIndex = 0; elementIndex < ae.Elements.Count; elementIndex++)
                    {
                        _hasSpreads |= CanSpread(ae.Elements[elementIndex]);
                    }
                }
            }

            if (cacheable)
            {
                _cached = true;
                var arguments = System.Array.Empty<JsValue>();
                if (cachedArgumentsHolder.JintArguments.Length > 0)
                {
                    arguments = new JsValue[cachedArgumentsHolder.JintArguments.Length];
                    BuildArguments(context, cachedArgumentsHolder.JintArguments, arguments);
                }

                cachedArgumentsHolder.CachedArguments = arguments;
            }

            _cachedArguments = cachedArgumentsHolder;
        }

        protected override ExpressionResult EvaluateInternal(EvaluationContext context)
        {
            if (_calleeExpression._expression.Type == Nodes.Super)
            {
                return NormalCompletion(SuperCall(context));
            }

            // https://tc39.es/ecma262/#sec-function-calls

            var reference = _calleeExpression.Evaluate(context).Value;

            if (ReferenceEquals(reference, Undefined.Instance))
            {
                return NormalCompletion(Undefined.Instance);
            }

            var engine = context.Engine;
            var func = engine.GetValue(reference, false);

            if (func.IsNullOrUndefined() && _expression.IsOptional())
            {
                return NormalCompletion(Undefined.Instance);
            }

            var referenceRecord = reference as Reference;
            if (referenceRecord != null
                && !referenceRecord.IsPropertyReference()
                && referenceRecord.GetReferencedName() == CommonProperties.Eval
                && ReferenceEquals(func, engine.Realm.Intrinsics.Eval))
            {
                return HandleEval(context, func, engine, referenceRecord);
            }

            var thisCall = (CallExpression) _expression;
            var tailCall = IsInTailPosition(thisCall);

            // https://tc39.es/ecma262/#sec-evaluatecall

            JsValue thisObject;
            if (referenceRecord is not null)
            {
                if (referenceRecord.IsPropertyReference())
                {
                    thisObject = referenceRecord.GetThisValue();
                }
                else
                {
                    var baseValue = referenceRecord.GetBase();

                    // deviation from the spec to support null-propagation helper
                    if (baseValue.IsNullOrUndefined()
                        && engine._referenceResolver.TryUnresolvableReference(engine, referenceRecord, out var value))
                    {
                        thisObject = value;
                    }
                    else
                    {
                        var refEnv = (EnvironmentRecord) baseValue;
                        thisObject = refEnv.WithBaseObject();
                    }
                }
            }
            else
            {
                thisObject = Undefined.Instance;
            }

            var arguments = ArgumentListEvaluation(context);

            if (!func.IsObject() && !engine._referenceResolver.TryGetCallable(engine, reference, out func))
            {
                ThrowMemberisNotFunction(referenceRecord, reference, engine);
            }

            var callable = func as ICallable;
            if (callable is null)
            {
                ThrowReferenceNotFunction(referenceRecord, reference, engine);
            }

            if (tailCall)
            {
                // TODO tail call
                // PrepareForTailCall();
            }

            // ensure logic is in sync between Call, Construct and JintCallExpression!

            JsValue result;
            if (callable is FunctionInstance functionInstance)
            {
                var callStack = engine.CallStack;
                var recursionDepth = callStack.Push(functionInstance, _calleeExpression, engine.ExecutionContext);

                if (recursionDepth > engine.Options.Constraints.MaxRecursionDepth)
                {
                    // automatically pops the current element as it was never reached
                    ExceptionHelper.ThrowRecursionDepthOverflowException(callStack);
                }

                try
                {
                    result = functionInstance.Call(thisObject, arguments);
                }
                finally
                {
                    // if call stack was reset due to recursive call to engine or similar, we might not have it anymore
                    if (callStack.Count > 0)
                    {
                        callStack.Pop();
                    }
                }
            }
            else
            {
                result = callable.Call(thisObject, arguments);
            }

            if (!_cached && arguments.Length > 0)
            {
                engine._jsValueArrayPool.ReturnArray(arguments);
            }

            engine._referencePool.Return(referenceRecord);
            return NormalCompletion(result);
        }

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowReferenceNotFunction(Reference? referenceRecord1, object reference, Engine engine)
        {
            var message = $"{referenceRecord1?.GetReferencedName() ?? reference} is not a function";
            ExceptionHelper.ThrowTypeError(engine.Realm, message);
        }

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowMemberisNotFunction(Reference? referenceRecord1, object reference, Engine engine)
        {
            var message = referenceRecord1 == null
                ? reference + " is not a function"
                : $"Property '{referenceRecord1.GetReferencedName()}' of object is not a function";
            ExceptionHelper.ThrowTypeError(engine.Realm, message);
        }

        private ExpressionResult HandleEval(EvaluationContext context, JsValue func, Engine engine, Reference referenceRecord)
        {
            var argList = ArgumentListEvaluation(context);
            if (argList.Length == 0)
            {
                return NormalCompletion(Undefined.Instance);
            }

            var evalFunctionInstance = (EvalFunctionInstance) func;
            var evalArg = argList[0];
            var strictCaller = StrictModeScope.IsStrictModeCode;
            var evalRealm = evalFunctionInstance._realm;
            var direct = !_expression.IsOptional();
            var value = evalFunctionInstance.PerformEval(evalArg, evalRealm, strictCaller, direct);
            engine._referencePool.Return(referenceRecord);
            return NormalCompletion(value);
        }

        private JsValue SuperCall(EvaluationContext context)
        {
            var engine = context.Engine;
            var thisEnvironment = (FunctionEnvironmentRecord) engine.ExecutionContext.GetThisEnvironment();
            var newTarget = engine.GetNewTarget(thisEnvironment);
            var func = GetSuperConstructor(thisEnvironment);
            if (func is null || !func.IsConstructor)
            {
                ExceptionHelper.ThrowTypeError(engine.Realm, "Not a constructor");
            }

            var argList = ArgumentListEvaluation(context);
            var result = ((IConstructor) func).Construct(argList, newTarget ?? JsValue.Undefined);
            var thisER = (FunctionEnvironmentRecord) engine.ExecutionContext.GetThisEnvironment();
            return thisER.BindThisValue(result);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-getsuperconstructor
        /// </summary>
        private static ObjectInstance? GetSuperConstructor(FunctionEnvironmentRecord thisEnvironment)
        {
            var envRec = thisEnvironment;
            var activeFunction = envRec._functionObject;
            var superConstructor = activeFunction.GetPrototypeOf();
            return superConstructor;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-isintailposition
        /// </summary>
        private static bool IsInTailPosition(CallExpression call)
        {
            // TODO tail calls
            return false;
        }

        private JsValue[] ArgumentListEvaluation(EvaluationContext context)
        {
            var cachedArguments = _cachedArguments;
            var arguments = Array.Empty<JsValue>();
            if (_cached)
            {
                arguments = cachedArguments.CachedArguments;
            }
            else
            {
                if (cachedArguments.JintArguments.Length > 0)
                {
                    if (_hasSpreads)
                    {
                        arguments = BuildArgumentsWithSpreads(context, cachedArguments.JintArguments);
                    }
                    else
                    {
                        arguments = context.Engine._jsValueArrayPool.RentArray(cachedArguments.JintArguments.Length);
                        BuildArguments(context, cachedArguments.JintArguments, arguments);
                    }
                }
            }

            return arguments;
        }

        private sealed class CachedArgumentsHolder
        {
            internal JintExpression[] JintArguments = Array.Empty<JintExpression>();
            internal JsValue[] CachedArguments = Array.Empty<JsValue>();
        }
    }
}
