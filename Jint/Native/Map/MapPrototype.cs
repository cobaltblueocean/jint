﻿using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Map
{
    /// <summary>
    /// https://www.ecma-international.org/ecma-262/6.0/#sec-map-objects
    /// </summary>
    public sealed class MapPrototype : MapInstance
    {
        private MapPrototype(Engine engine) : base(engine)
        {
        }

        public static MapPrototype CreatePrototypeObject(Engine engine, MapConstructor mapConstructor)
        {
            var obj = new MapPrototype(engine)
            {
                Extensible = true, Prototype = engine.Object.PrototypeObject
            };

            obj.SetOwnProperty("length", new PropertyDescriptor(0, PropertyFlag.Configurable));
            obj.SetOwnProperty("constructor", new PropertyDescriptor(mapConstructor, PropertyFlag.NonEnumerable));

            return obj;
        }

        public void Configure()
        {
            var iterator = new ClrFunctionInstance(Engine, "iterator", Iterator, 1);
            FastAddProperty(GlobalSymbolRegistry.Iterator._value, iterator, true, false, true);
            FastAddProperty(GlobalSymbolRegistry.ToStringTag._value, "Map", false, false, true);

            FastAddProperty("clear", new ClrFunctionInstance(Engine, "clear", Clear, 0), true, false, true);
            FastAddProperty("delete", new ClrFunctionInstance(Engine, "delete", Delete, 1), true, false, true);
            FastAddProperty("entries", iterator, true, false, true);
            FastAddProperty("forEach", new ClrFunctionInstance(Engine, "forEach", ForEach, 1), true, false, true);
            FastAddProperty("get", new ClrFunctionInstance(Engine, "get", Get, 1), true, false, true);
            FastAddProperty("has", new ClrFunctionInstance(Engine, "has", Has, 1), true, false, true);
            FastAddProperty("keys", new ClrFunctionInstance(Engine, "keys", Keys, 0), true, false, true);
            FastAddProperty("set", new ClrFunctionInstance(Engine, "set", Set, 2), true, false, true);
            FastAddProperty("values", new ClrFunctionInstance(Engine, "values", Values, 0), true, false, true);
        }

        private JsValue Get(JsValue thisObj, JsValue[] arguments)
        {
            return ((MapInstance) thisObj).Get(arguments.At(0));
        }

        private JsValue Clear(JsValue thisObj, JsValue[] arguments)
        {
            var map = thisObj as MapInstance
                      ?? ExceptionHelper.ThrowTypeError<MapInstance>(_engine, "object must be a Map");

            map.Clear();
            return Undefined;
        }

        private JsValue Delete(JsValue thisObj, JsValue[] arguments)
        {
            var map = thisObj as MapInstance
                      ?? ExceptionHelper.ThrowTypeError<MapInstance>(_engine, "object must be a Map");

            return map.Delete(arguments[0])
                ? JsBoolean.True
                : JsBoolean.False;
        }

        private JsValue Set(JsValue thisObj, JsValue[] arguments)
        {
            ((MapInstance) thisObj).Set(arguments[0], arguments[1]);
            return thisObj;
        }

        private JsValue Has(JsValue thisObj, JsValue[] arguments)
        {
            return ((MapInstance) thisObj).Has(arguments[0])
                ? JsBoolean.True
                : JsBoolean.False;
        }

        private JsValue ForEach(JsValue thisObj, JsValue[] arguments)
        {
            var callbackfn = arguments.At(0);
            var thisArg = arguments.At(1);

            var map = (MapInstance) thisObj;

            var callable = GetCallable(callbackfn);

            map.ForEach(callable, thisArg);

            return Undefined;
        }

        private ObjectInstance Iterator(JsValue thisObj, JsValue[] arguments)
        {
            return ((MapInstance) thisObj).Iterator();
        }

        private ObjectInstance Keys(JsValue thisObj, JsValue[] arguments)
        {
            return ((MapInstance) thisObj).Keys();
        }

        private ObjectInstance Values(JsValue thisObj, JsValue[] arguments)
        {
            return ((MapInstance) thisObj).Values();
        }


    }
}