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
        private CachedArgumentsHolder _cachedArguments;
        private bool _cached;

        private JintExpression _calleeExpression;
        private bool _hasSpreads;

        public JintCallExpression(Engine engine, CallExpression expression) : base(engine, expression)
        {
            _initialized = false;
        }

        protected override void Initialize()
        {
            var expression = (CallExpression) _expression;
            _calleeExpression = Build(_engine, expression.Callee);
            var cachedArgumentsHolder = new CachedArgumentsHolder
            {
                JintArguments = new JintExpression[expression.Arguments.Count]
            };

            static bool CanSpread(Node e)
            {
                return e?.Type == Nodes.SpreadElement
                    || e is AssignmentExpression ae && ae.Right?.Type == Nodes.SpreadElement;
            }

            bool cacheable = true;
            for (var i = 0; i < expression.Arguments.Count; i++)
            {
                var expressionArgument = expression.Arguments[i];
                cachedArgumentsHolder.JintArguments[i] = Build(_engine, expressionArgument);
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
                    BuildArguments(cachedArgumentsHolder.JintArguments, arguments);
                }

                cachedArgumentsHolder.CachedArguments = arguments;
            }

            _cachedArguments = cachedArgumentsHolder;
        }

        protected override object EvaluateInternal()
        {
            return _calleeExpression is JintSuperExpression 
                ? SuperCall()
                : Call();
        }

        private object SuperCall()
        {
            var thisEnvironment = (FunctionEnvironmentRecord) _engine.ExecutionContext.GetThisEnvironment();
            var newTarget = _engine.GetNewTarget(thisEnvironment);
            var func = GetSuperConstructor(thisEnvironment);
            if (!func.IsConstructor)
            {
                ExceptionHelper.ThrowTypeError(_engine, "Not a constructor");
            }

            var argList = ArgumentListEvaluation();
            var result = ((IConstructor) func).Construct(argList, newTarget);
            var thisER = (FunctionEnvironmentRecord) _engine.ExecutionContext.GetThisEnvironment();
            return thisER.BindThisValue(result);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-getsuperconstructor
        /// </summary>
        private static ObjectInstance GetSuperConstructor(FunctionEnvironmentRecord thisEnvironment)
        {
            var envRec = thisEnvironment;
            var activeFunction = envRec._functionObject;
            var superConstructor = activeFunction.GetPrototypeOf();
            return superConstructor;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-function-calls
        /// </summary>
        private object Call()
        {
            var reference = _calleeExpression.Evaluate();

            if (ReferenceEquals(reference, Undefined.Instance))
            {
                return Undefined.Instance;
            }
            
            var func = _engine.GetValue(reference, false);

            if (reference is Reference referenceRecord 
                && !referenceRecord.IsPropertyReference()
                && referenceRecord.GetReferencedName() == CommonProperties.Eval
                && func is EvalFunctionInstance eval)
            {
                var argList = ArgumentListEvaluation();
                if (argList.Length == 0)
                {
                    return Undefined.Instance;
                }

                var evalArg = argList[0];
                var strictCaller = StrictModeScope.IsStrictModeCode;
                var evalRealm = _engine.ExecutionContext.Realm;
                var direct = !((CallExpression) _expression).Optional;
                var value = eval.PerformEval(evalArg, evalRealm, strictCaller, direct);
                _engine._referencePool.Return(referenceRecord);
                return value;
            }

            var thisCall = (CallExpression) _expression;
            var tailCall = IsInTailPosition(thisCall);
            return EvaluateCall(func, reference, thisCall.Arguments, tailCall);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-evaluatecall
        /// </summary>
        private object EvaluateCall(JsValue func, object reference, in NodeList<Expression> arguments, bool tailPosition)
        {
            JsValue thisValue;
            var referenceRecord = reference as Reference;
            if (referenceRecord is not null)
            {
                if (referenceRecord.IsPropertyReference())
                {
                    thisValue = referenceRecord.GetThisValue();
                }
                else
                {
                    var baseValue = referenceRecord.GetBase();
                    
                    // deviation from the spec to support null-propagation helper
                    if (baseValue.IsNullOrUndefined() 
                        && _engine._referenceResolver.TryUnresolvableReference(_engine, referenceRecord, out var value))
                    {
                        return value;
                    }
                    
                    var refEnv = (EnvironmentRecord) baseValue;
                    thisValue = refEnv.WithBaseObject();
                }
            }
            else
            {
                thisValue = Undefined.Instance;
            }
            
            var argList = ArgumentListEvaluation();

            if (!func.IsObject() && !_engine._referenceResolver.TryGetCallable(_engine, reference, out func))
            {
                var message = referenceRecord == null 
                    ? reference + " is not a function" 
                    : $"Property '{referenceRecord.GetReferencedName()}' of object is not a function";
                ExceptionHelper.ThrowTypeError(_engine, message);
            }

            if (func is not ICallable callable)
            {
                var message = $"{referenceRecord?.GetReferencedName() ?? reference} is not a function";
                return ExceptionHelper.ThrowTypeError<object>(_engine, message);
            }

            if (tailPosition)
            {
                // TODO tail call
                // PrepareForTailCall();
            }

            var result = _engine.Call(callable, thisValue, argList, _calleeExpression);

            if (!_cached && argList.Length > 0)
            {
                _engine._jsValueArrayPool.ReturnArray(argList);
            }

            _engine._referencePool.Return(referenceRecord);
            return result;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-isintailposition
        /// </summary>
        private static bool IsInTailPosition(CallExpression call)
        {
            // TODO tail calls
            return false;
        }

        private JsValue[] ArgumentListEvaluation()
        {
            var cachedArguments = _cachedArguments;
            var arguments = System.Array.Empty<JsValue>();
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
                        arguments = BuildArgumentsWithSpreads(cachedArguments.JintArguments);
                    }
                    else
                    {
                        arguments = _engine._jsValueArrayPool.RentArray(cachedArguments.JintArguments.Length);
                        BuildArguments(cachedArguments.JintArguments, arguments);
                    }
                }
            }

            return arguments;
        }

        private class CachedArgumentsHolder
        {
            internal JintExpression[] JintArguments;
            internal JsValue[] CachedArguments;
        }
    }
}