using System;
using System.Numerics;
using Jint.Runtime;

namespace Jint.Native.TypedArray;

/// <summary>
/// Container for either double or BigInteger.
/// </summary>
internal readonly record struct TypedArrayValue(Types Type, double DoubleValue, BigInteger BigInteger) : IConvertible
{
    public static implicit operator TypedArrayValue(double value)
    {
        return new TypedArrayValue(Types.Number, value, default);
    }

    public static implicit operator TypedArrayValue(BigInteger value)
    {
        return new TypedArrayValue(Types.BigInt, default, value);
    }

    public JsValue ToJsValue()
    {
        return Type == Types.Number
            ? JsNumber.Create(DoubleValue)
            : JsBigInt.Create(BigInteger);
    }

    public TypeCode GetTypeCode()
    {
        ExceptionHelper.ThrowNotImplementedException();
        return default;
    }

    public bool ToBoolean(IFormatProvider provider)
    {
        ExceptionHelper.ThrowNotImplementedException();
        return default;
    }

    public char ToChar(IFormatProvider provider)
    {
        ExceptionHelper.ThrowNotImplementedException();
        return default;
    }

    public sbyte ToSByte(IFormatProvider provider)
    {
        return (sbyte) DoubleValue;
    }

    public byte ToByte(IFormatProvider provider)
    {
        return (byte) DoubleValue;
    }

    public short ToInt16(IFormatProvider provider)
    {
        return (short) DoubleValue;
    }

    public ushort ToUInt16(IFormatProvider provider)
    {
        return (ushort) DoubleValue;
    }

    public int ToInt32(IFormatProvider provider)
    {
        return (int) DoubleValue;
    }

    public uint ToUInt32(IFormatProvider provider)
    {
        return (uint) DoubleValue;
    }

    public long ToInt64(IFormatProvider provider)
    {
        return (long) DoubleValue;
    }

    public ulong ToUInt64(IFormatProvider provider)
    {
        return (ulong) DoubleValue;
    }

    public float ToSingle(IFormatProvider provider)
    {
        return (float) DoubleValue;
    }

    public double ToDouble(IFormatProvider provider)
    {
        return DoubleValue;
    }

    public decimal ToDecimal(IFormatProvider provider)
    {
        return (decimal) DoubleValue;
    }

    public DateTime ToDateTime(IFormatProvider provider)
    {
        ExceptionHelper.ThrowNotImplementedException();
        return default;
    }

    public string ToString(IFormatProvider provider)
    {
        ExceptionHelper.ThrowNotImplementedException();
        return default;
    }

    public object ToType(Type conversionType, IFormatProvider provider)
    {
        if (conversionType == typeof(BigInteger) && Type == Types.BigInt)
        {
            return BigInteger;
        }

        ExceptionHelper.ThrowNotImplementedException();
        return default;
    }
}
