﻿using System.Collections.Generic;
using Esprima.Ast;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter;

namespace Jint.Native.Function
{
    public sealed class ScriptFunctionInstance : FunctionInstance, IConstructor
    {
        internal readonly JintFunctionDefinition _function;

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-13.2
        /// </summary>
        public ScriptFunctionInstance(
            Engine engine,
            IFunction functionDeclaration,
            LexicalEnvironment scope,
            bool strict)
            : this(engine, new JintFunctionDefinition(engine, functionDeclaration), scope, strict)
        {
        }

        internal ScriptFunctionInstance(
            Engine engine,
            JintFunctionDefinition function,
            LexicalEnvironment scope,
            bool strict)
            : base(engine, function, scope, strict)
        {
            _function = function;

            _prototype = _engine.Function.PrototypeObject;

            _length = new PropertyDescriptor(JsNumber.Create(function._length), PropertyFlag.Configurable);

            var proto = new ObjectInstanceWithConstructor(engine, this)
            {
                _prototype = _engine.Object.PrototypeObject
            };

            _prototypeDescriptor = new PropertyDescriptor(proto, PropertyFlag.OnlyWritable);

            if (strict)
            {
                DefineOwnProperty(CommonProperties.Caller, engine._getSetThrower);
                DefineOwnProperty(CommonProperties.Arguments, engine._getSetThrower);
            }
        }

        // for example RavenDB wants to inspect this
        public IFunction FunctionDeclaration => _function._function;

        /// <summary>
        /// https://tc39.es/ecma262/#sec-ecmascript-function-objects-call-thisargument-argumentslist
        /// </summary>
        public override JsValue Call(JsValue thisArgument, JsValue[] arguments)
        {
            var callerContext = _engine.ExecutionContext;
            var calleeContext = PrepareForOrdinaryCall(this, Undefined);
            OrdinaryCallBindThis(this, calleeContext, thisArgument);

            var strict = _strict || _engine._isStrict;
            using (new StrictModeScope(strict, true))
            {
                try
                {
                    var argumentsInstance = _engine.FunctionDeclarationInstantiation(
                        functionInstance: this,
                        arguments);

                    var result = _function._body.Execute();
                    var value = result.GetValueOrDefault().Clone();
                    argumentsInstance?.FunctionWasCalled();

                    if (result.Type == CompletionType.Throw)
                    {
                        ExceptionHelper.ThrowJavaScriptException(_engine, value, result);
                    }

                    if (result.Type == CompletionType.Return)
                    {
                        return value;
                    }
                }
                finally
                {
                    _engine.LeaveExecutionContext();
                }

                return Undefined;
            }
        }

        private void OrdinaryCallBindThis(ScriptFunctionInstance f, in ExecutionContext calleeContext, JsValue thisArgument)
        {
            // we have separate arrow function instance, so this is never true
            // Let thisMode be F.[[ThisMode]].
            // If thisMode is lexical, return NormalCompletion(undefined).
            var localEnv = calleeContext.LexicalEnvironment;
            JsValue thisValue;
            if (_strict || _engine._isStrict)
            {
                thisValue = thisArgument;
            }
            else
            {
                if (thisArgument.IsNullOrUndefined())
                {
                    var globalEnv = _engine.GlobalEnvironment;
                    var globalEnvRec = (GlobalEnvironmentRecord) globalEnv._record;
                    thisValue = globalEnvRec.GlobalThisValue;
                    thisValue = _engine.Global;
                }
                else
                {
                    thisValue = TypeConverter.ToObject(_engine, thisArgument);
                }
            }

            var envRec = (FunctionEnvironmentRecord) localEnv._record;
            envRec.BindThisValue(thisValue);
        }

        private ExecutionContext PrepareForOrdinaryCall(FunctionInstance f, JsValue newTarget)
        {
            // var callerContext = _engine.ExecutionContext;
            // Let calleeRealm be F.[[Realm]].
            // Set the Realm of calleeContext to calleeRealm.
            // Set the ScriptOrModule of calleeContext to F.[[ScriptOrModule]].
            var localEnv = LexicalEnvironment.NewFunctionEnvironment(_engine, f, newTarget);
            // If callerContext is not already suspended, suspend callerContext.
            // Push calleeContext onto the execution context stack; calleeContext is now the running execution context.
            // NOTE: Any exception objects produced after this point are associated with calleeRealm.
            // Return calleeContext.
            var calleeContext = _engine.EnterExecutionContext(localEnv, localEnv);
            return calleeContext;
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-13.2.2
        /// </summary>
        public ObjectInstance Construct(JsValue[] arguments, JsValue newTarget)
        {
            var thisArgument = OrdinaryCreateFromConstructor(TypeConverter.ToObject(_engine, newTarget), _engine.Object.PrototypeObject);

            var result = Call(thisArgument, arguments).TryCast<ObjectInstance>();
            if (!ReferenceEquals(result, null))
            {
                return result;
            }

            return thisArgument;
        }

        private class ObjectInstanceWithConstructor : ObjectInstance
        {
            private PropertyDescriptor _constructor;

            public ObjectInstanceWithConstructor(Engine engine, ObjectInstance thisObj) : base(engine)
            {
                _constructor = new PropertyDescriptor(thisObj, PropertyFlag.NonEnumerable);
            }

            public override IEnumerable<KeyValuePair<JsValue, PropertyDescriptor>> GetOwnProperties()
            {
                if (_constructor != null)
                {
                    yield return new KeyValuePair<JsValue, PropertyDescriptor>(CommonProperties.Constructor, _constructor);
                }

                foreach (var entry in base.GetOwnProperties())
                {
                    yield return entry;
                }
            }

            public override PropertyDescriptor GetOwnProperty(JsValue property)
            {
                if (property == CommonProperties.Constructor)
                {
                    return _constructor ?? PropertyDescriptor.Undefined;
                }

                return base.GetOwnProperty(property);
            }

            protected internal override void SetOwnProperty(JsValue property, PropertyDescriptor desc)
            {
                if (property == CommonProperties.Constructor)
                {
                    _constructor = desc;
                }
                else
                {
                    base.SetOwnProperty(property, desc);
                }
            }

            public override bool HasOwnProperty(JsValue property)
            {
                if (property == CommonProperties.Constructor)
                {
                    return _constructor != null;
                }

                return base.HasOwnProperty(property);
            }

            public override void RemoveOwnProperty(JsValue property)
            {
                if (property == CommonProperties.Constructor)
                {
                    _constructor = null;
                }
                else
                {
                    base.RemoveOwnProperty(property);
                }
            }
        }
    }
}