using System.Globalization;
using System.Text;
using Jint.Collections;
using Jint.Native.ArrayBuffer;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.TypedArray;

internal sealed class Uint8ArrayPrototype : Prototype
{
    private readonly TypedArrayConstructor _constructor;

    public Uint8ArrayPrototype(
        Engine engine,
        IntrinsicTypedArrayPrototype objectPrototype,
        TypedArrayConstructor constructor)
        : base(engine, engine.Realm)
    {
        _prototype = objectPrototype;
        _constructor = constructor;

    }

    protected override void Initialize()
    {
        const PropertyFlag PropertyFlags = PropertyFlag.Configurable | PropertyFlag.Writable;
        var properties = new PropertyDictionary(6, checkExistingKeys: false)
        {
            ["BYTES_PER_ELEMENT"] = new(JsNumber.PositiveOne, PropertyFlag.AllForbidden),
            ["constructor"] = new(_constructor, PropertyFlag.NonEnumerable),
            ["setFromBase64"] = new(new ClrFunction(Engine, "setFromBase64", SetFromBase64, 1, PropertyFlag.Configurable), PropertyFlags),
            ["setFromHex"] = new(new ClrFunction(Engine, "setFromHex", SetFromHex, 1, PropertyFlag.Configurable), PropertyFlags),
            ["toBase64"] = new(new ClrFunction(Engine, "toBase64", ToBase64, 0, PropertyFlag.Configurable), PropertyFlags),
            ["toHex"] = new(new ClrFunction(Engine, "toHex", ToHex, 0, PropertyFlag.Configurable), PropertyFlags),
        };
        SetProperties(properties);
    }

    private JsObject SetFromBase64(JsValue thisObject, JsValue[] arguments)
    {
        var into = ValidateUint8Array(thisObject);
        var s = arguments.At(0);

        if (!s.IsString())
        {
            ExceptionHelper.ThrowTypeError(_realm, "setFromBase64 must be called with a string");
        }

        var opts = Uint8ArrayConstructor.GetOptionsObject(_engine, arguments.At(1));
        var alphabet = Uint8ArrayConstructor.GetAndValidateAlphabetOption(_engine, opts);
        var lastChunkHandling = Uint8ArrayConstructor.GetAndValidateLastChunkHandling(_engine, opts);

        var taRecord = IntrinsicTypedArrayPrototype.MakeTypedArrayWithBufferWitnessRecord(into, ArrayBufferOrder.SeqCst);
        if (taRecord.IsTypedArrayOutOfBounds)
        {
            ExceptionHelper.ThrowTypeError(_realm, "TypedArray is out of bounds");
        }

        var byteLength = taRecord.TypedArrayLength;
        var result = Uint8ArrayConstructor.FromBase64(s.ToString(), alphabet.ToString(), lastChunkHandling.ToString(), byteLength);
        if (result.Error is not null)
        {
            throw result.Error;
        }

        SetUint8ArrayBytes(into, result.Bytes);

        var resultObject = OrdinaryObjectCreate(_engine, _engine.Intrinsics.Object);
        resultObject.CreateDataPropertyOrThrow("read", result.Read);
        resultObject.CreateDataPropertyOrThrow("written", result.Bytes.Length);
        return resultObject;
    }

    private static void SetUint8ArrayBytes(JsTypedArray into, byte[] bytes)
    {
        var offset = into._byteOffset;
        var len = bytes.Length;
        var index = 0;
        while (index < len)
        {
            var b = bytes[index];
            var byteIndexInBuffer = index + offset;
            into._viewedArrayBuffer.SetValueInBuffer(byteIndexInBuffer, TypedArrayElementType.Uint8, b, isTypedArray: true, ArrayBufferOrder.Unordered);
            index++;
        }
    }

    private JsObject SetFromHex(JsValue thisObject, JsValue[] arguments)
    {
        var into = ValidateUint8Array(thisObject);
        var s = arguments.At(0);

        if (!s.IsString())
        {
            ExceptionHelper.ThrowTypeError(_realm, "setFromHex must be called with a string");
        }

        var taRecord = IntrinsicTypedArrayPrototype.MakeTypedArrayWithBufferWitnessRecord(into, ArrayBufferOrder.SeqCst);
        if (taRecord.IsTypedArrayOutOfBounds)
        {
            ExceptionHelper.ThrowTypeError(_realm, "TypedArray is out of bounds");
        }

        var byteLength = taRecord.TypedArrayLength;
        var result = Uint8ArrayConstructor.FromHex(_engine, s.ToString(), byteLength);
        if (result.Error is not null)
        {
            throw result.Error;
        }

        SetUint8ArrayBytes(into, result.Bytes);

        var resultObject = OrdinaryObjectCreate(_engine, _engine.Intrinsics.Object);
        resultObject.CreateDataPropertyOrThrow("read", result.Read);
        resultObject.CreateDataPropertyOrThrow("written", result.Bytes.Length);
        return resultObject;
    }

    private JsValue ToBase64(JsValue thisObject, JsValue[] arguments)
    {
       var o = ValidateUint8Array(thisObject);

        var opts = Uint8ArrayConstructor.GetOptionsObject(_engine, arguments.At(0));
        var alphabet = Uint8ArrayConstructor.GetAndValidateAlphabetOption(_engine, opts);

        var omitPadding = TypeConverter.ToBoolean(opts.Get("omitPadding"));
        var toEncode = GetUint8ArrayBytes(o);
        string outAscii;
        if (alphabet == "base64")
        {
            outAscii = Convert.ToBase64String(toEncode, omitPadding ? Base64FormattingOptions.None : Base64FormattingOptions.InsertLineBreaks);
        }
        else
        {
            outAscii = Convert.ToBase64String(toEncode, omitPadding ? Base64FormattingOptions.None : Base64FormattingOptions.InsertLineBreaks) + "";
        }

        return outAscii;
    }

    private JsValue ToHex(JsValue thisObject, JsValue[] arguments)
    {
        var o = ValidateUint8Array(thisObject);
        var toEncode = GetUint8ArrayBytes(o);
        using var outString = new ValueStringBuilder();
        for (var i = 0; i < toEncode.Length; i++)
        {
            outString.Append(toEncode[i].ToString("X2", CultureInfo.InvariantCulture));
        }

        return outString.ToString();
    }

    private byte[] GetUint8ArrayBytes(JsTypedArray ta)
    {
        var buffer = ta._viewedArrayBuffer;
        var taRecord = IntrinsicTypedArrayPrototype.MakeTypedArrayWithBufferWitnessRecord(ta, ArrayBufferOrder.SeqCst);
        if (taRecord.IsTypedArrayOutOfBounds)
        {
            ExceptionHelper.ThrowTypeError(_realm, "TypedArray is out of bounds");
        }

        var len = taRecord.TypedArrayLength;
        var byteOffset = ta._byteOffset;
        var bytes = new byte[len];
        var index = 0;
        while (index < len)
        {
            var byteIndex = byteOffset + index;
            var b = buffer.GetValueFromBuffer(byteIndex, TypedArrayElementType.Uint8, isTypedArray: true, ArrayBufferOrder.Unordered);
            bytes[index] = (byte) b.DoubleValue;
            index++;
        }

        return bytes;
    }

    private JsTypedArray ValidateUint8Array(JsValue ta)
    {
        if (ta is not JsTypedArray { _arrayElementType: TypedArrayElementType.Uint8 } typedArray)
        {
            ExceptionHelper.ThrowTypeError(_engine.Realm, "Not a Uint8Array");
            return default;
        }

        return typedArray;
    }
}
