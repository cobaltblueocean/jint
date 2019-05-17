﻿using Jint.Collections;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Boolean
{
    /// <summary>
    ///     http://www.ecma-international.org/ecma-262/5.1/#sec-15.6.4
    /// </summary>
    public sealed class BooleanPrototype : BooleanInstance
    {
        private BooleanPrototype(Engine engine) : base(engine)
        {
        }

        public static BooleanPrototype CreatePrototypeObject(Engine engine, BooleanConstructor booleanConstructor)
        {
            var obj = new BooleanPrototype(engine)
            {
                Prototype = engine.Object.PrototypeObject,
                PrimitiveValue = false,
                Extensible = true,
                _properties = new StringDictionarySlim<PropertyDescriptor>(3)
            };

            obj._properties["constructor"] = new PropertyDescriptor(booleanConstructor, PropertyFlag.NonEnumerable);

            return obj;
        }

        protected override void Initialize()
        {
            FastAddProperty("toString", new ClrFunctionInstance(Engine, "toString", ToBooleanString, 0, PropertyFlag.Configurable), true, false, true);
            FastAddProperty("valueOf", new ClrFunctionInstance(Engine, "valueOf", ValueOf, 0, PropertyFlag.Configurable), true, false, true);
        }

        private JsValue ValueOf(JsValue thisObj, JsValue[] arguments)
        {
            if (thisObj._type == Types.Boolean)
            {
                return thisObj;
            }

            if (thisObj is BooleanInstance bi)
            {
                return bi.PrimitiveValue;
            }

            return ExceptionHelper.ThrowTypeError<JsValue>(Engine);
        }

        private JsValue ToBooleanString(JsValue thisObj, JsValue[] arguments)
        {
            var b = ValueOf(thisObj, Arguments.Empty);
            return ((JsBoolean) b)._value ? "true" : "false";
        }
    }
}