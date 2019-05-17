﻿using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Error
{
    public sealed class ErrorConstructor : FunctionInstance, IConstructor
    {
        private string _name;

        public ErrorConstructor(Engine engine) : base(engine, "Error", null, null, false)
        {
        }

        public static ErrorConstructor CreateErrorConstructor(Engine engine, string name)
        {
            var obj = new ErrorConstructor(engine)
            {
                Extensible = true,
                _name = name,
                Prototype = engine.Function.PrototypeObject
            };

            // The value of the [[Prototype]] internal property of the Error constructor is the Function prototype object (15.11.3)
            obj.PrototypeObject = ErrorPrototype.CreatePrototypeObject(engine, obj, name);

            obj._length = new PropertyDescriptor(1, PropertyFlag.AllForbidden);

            // The initial value of Error.prototype is the Error prototype object
            obj._prototype = new PropertyDescriptor(obj.PrototypeObject, PropertyFlag.AllForbidden);

            return obj;
        }

        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return Construct(arguments);
        }

        public ObjectInstance Construct(JsValue[] arguments)
        {
            var instance = new ErrorInstance(Engine, _name);
            instance.Prototype = PrototypeObject;
            instance.Extensible = true;

            var jsValue = arguments.At(0);
            if (!jsValue.IsUndefined())
            {
                instance.Put("message", TypeConverter.ToString(jsValue), false);
            }

            return instance;
        }

        public ErrorPrototype PrototypeObject { get; private set; }
    }
}
