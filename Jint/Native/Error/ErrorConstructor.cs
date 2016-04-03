﻿using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using System.Text;

namespace Jint.Native.Error
{
    public class ErrorConstructor : FunctionInstance, IConstructor
    {
        private string _name;

        public ErrorConstructor(Engine engine) : base(engine, null, null, false)
        {
        }

        public static ErrorConstructor CreateErrorConstructor(Engine engine, string name)
        {
            var obj = new ErrorConstructor(engine);
            obj.Extensible = true;
            obj._name = name;

            // The value of the [[Prototype]] internal property of the Error constructor is the Function prototype object (15.11.3)
            obj.Prototype = engine.Function.PrototypeObject;
            obj.PrototypeObject = ErrorPrototype.CreatePrototypeObject(engine, obj, name);

            obj.FastAddProperty("length", 1, false, false, false);

            // The initial value of Error.prototype is the Error prototype object
            obj.FastAddProperty("prototype", obj.PrototypeObject, false, false, false);

            return obj;
        }

        public void Configure()
        {
            
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

            if (Engine.ShouldCreateStackTrace)
            {
                StringBuilder builder = new StringBuilder();
                builder.Append(this._name);

                if (arguments.At(0) != Undefined.Instance)
                {
                    var message = TypeConverter.ToString(arguments.At(0));
                    builder.Append(": ").Append(message).Append('\n');
                    instance.Put("message", message, false);
                }
                else
                {
                    builder.AppendLine();
                }
                Engine.AppendStack(builder);
                instance.Put("stack", builder.ToString(), false);
            }
            else if (arguments.At(0) != Undefined.Instance)
            {
                var message = TypeConverter.ToString(arguments.At(0));
                instance.Put("message", message, false);
            }

            return instance;
        }

        public ErrorPrototype PrototypeObject { get; private set; }        
    }
}
