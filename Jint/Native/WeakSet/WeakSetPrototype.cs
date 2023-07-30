using Jint.Collections;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.WeakSet;

/// <summary>
/// https://tc39.es/ecma262/#sec-weakset-objects
/// </summary>
internal sealed class WeakSetPrototype : Prototype
{
    private readonly WeakSetConstructor _constructor;
    internal ClrFunctionInstance _originalAddFunction = null!;

    internal WeakSetPrototype(
        Engine engine,
        Realm realm,
        WeakSetConstructor constructor,
        ObjectPrototype prototype) : base(engine, realm)
    {
        _prototype = prototype;
        _constructor = constructor;
    }

    protected override void Initialize()
    {
        _originalAddFunction = new ClrFunctionInstance(Engine, "add", Add, 1, PropertyFlag.Configurable);

        const PropertyFlag PropertyFlags = PropertyFlag.Configurable | PropertyFlag.Writable;
        var properties = new PropertyDictionary(5, checkExistingKeys: false)
        {
            ["length"] = new(0, PropertyFlag.Configurable),
            ["constructor"] = new(_constructor, PropertyFlag.NonEnumerable),
            ["delete"] = new(new ClrFunctionInstance(Engine, "delete", Delete, 1, PropertyFlag.Configurable), PropertyFlags),
            ["add"] = new(_originalAddFunction, PropertyFlags),
            ["has"] = new(new ClrFunctionInstance(Engine, "has", Has, 1, PropertyFlag.Configurable), PropertyFlags),
        };
        SetProperties(properties);

        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.ToStringTag] = new("WeakSet", false, false, true)
        };
        SetSymbols(symbols);
    }

    private JsValue Add(JsValue thisObject, JsValue[] arguments)
    {
        var set = AssertWeakSetInstance(thisObject);
        set.WeakSetAdd(arguments.At(0));
        return thisObject;
    }

    private JsValue Delete(JsValue thisObject, JsValue[] arguments)
    {
        var set = AssertWeakSetInstance(thisObject);
        return set.WeakSetDelete(arguments.At(0)) ? JsBoolean.True : JsBoolean.False;
    }

    private JsValue Has(JsValue thisObject, JsValue[] arguments)
    {
        var set = AssertWeakSetInstance(thisObject);
        return set.WeakSetHas(arguments.At(0)) ? JsBoolean.True : JsBoolean.False;
    }

    private WeakSetInstance AssertWeakSetInstance(JsValue thisObject)
    {
        if (thisObject is WeakSetInstance set)
        {
            return set;
        }

        ExceptionHelper.ThrowTypeError(_realm, "object must be a WeakSet");
        return default;
    }
}
