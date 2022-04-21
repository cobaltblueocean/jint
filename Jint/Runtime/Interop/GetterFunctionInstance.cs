﻿using System;
using Jint.Native;
using Jint.Native.Function;

namespace Jint.Runtime.Interop
{
    /// <summary>
    /// Represents a FunctionInstance wrapping a Clr getter.
    /// </summary>
    public sealed class GetterFunctionInstance: FunctionInstance
    {
        private static readonly JsString _name = new JsString("get");
        private readonly Func<JsValue, JsValue> _getter;

        public GetterFunctionInstance(Engine engine, Func<JsValue, JsValue> getter)
            : base(engine, engine.Realm, _name, FunctionThisMode.Global)
        {
            _getter = getter;
        }

        public override JsValue Call(JsValue thisObject, in Arguments arguments)
        {
            return _getter(thisObject);
        }
    }
}
