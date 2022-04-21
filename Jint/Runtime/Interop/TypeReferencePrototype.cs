﻿//using Jint.Native;
//using Jint.Native.Object;

//namespace Jint.Runtime.Interop
//{
//    public sealed class TypeReferencePrototype : ObjectInstance
//    {
//        private TypeReferencePrototype(Engine engine)
//            : base(engine)
//        {
//        }

//        public static TypeReferencePrototype CreatePrototypeObject(Engine engine, TypeReference typeReferenceConstructor)
//        {
//            var obj = new TypeReferencePrototype(engine);
//            obj.Prototype = engine.Object.PrototypeObject;
//            obj.Extensible = false;

//            obj.FastAddProperty("constructor", typeReferenceConstructor, true, false, true);

//            return obj;
//        }

//        public void Configure()
//        {
//            FastAddProperty("toString", new ClrFunctionInstance(Engine, ToTypeReferenceString), true, false, true);
//        }

//        private JsValue ToTypeReferenceString(JsValue thisObj, in Arguments arguments)
//        {
//            var typeReference = thisObj.As<TypeReference>();
//            if (typeReference == null)
//            {
//                ExceptionHelper.ThrowTypeError(Engine);
//            }

//            return typeReference.Type.FullName;
//        }
//    }
//}
