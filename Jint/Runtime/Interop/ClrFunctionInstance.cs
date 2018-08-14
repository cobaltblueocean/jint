﻿using System;
using Jint.Native;
using Jint.Native.Function;
using Jint.Runtime.Descriptors;

namespace Jint.Runtime.Interop
{
    /// <summary>
    /// Wraps a Clr method into a FunctionInstance
    /// </summary>
    public sealed class ClrFunctionInstance : FunctionInstance
    {
        private readonly Func<JsValue, JsValue[], JsValue> _func;

        public ClrFunctionInstance(
            Engine engine,
            string name,
            Func<JsValue, JsValue[], JsValue> func,
            int length) : base(engine, name, null, null, false)
        {
            _func = func;

            Prototype = engine.Function.PrototypeObject;
            Extensible = true;

            _length = new PropertyDescriptor(length, PropertyFlag.Configurable);
        }

        public ClrFunctionInstance(
            Engine engine,
            string name,
            Func<JsValue, JsValue[], JsValue> func)
            : this(engine, name, func, 0)
        {
        }

        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            try
            {
                var result = _func(thisObject, arguments);
                return result;
            }
            catch (InvalidCastException)
            {
                ExceptionHelper.ThrowTypeError(Engine);
                return null;
            }
        }
    }
}
