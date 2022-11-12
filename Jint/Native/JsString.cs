using System.Text;
using Jint.Runtime;

namespace Jint.Native;

public class JsString : JsValue, IEquatable<JsString>
{
    private const int AsciiMax = 126;
    private static readonly JsString[] _charToJsValue;
    private static readonly JsString[] _charToStringJsValue;
    private static readonly JsString[] _intToStringJsValue;

    public static readonly JsString Empty = new JsString("");
    internal static readonly JsString NullString = new JsString("null");
    internal static readonly JsString UndefinedString = new JsString("undefined");
    internal static readonly JsString ObjectString = new JsString("object");
    internal static readonly JsString FunctionString = new JsString("function");
    internal static readonly JsString BooleanString = new JsString("boolean");
    internal static readonly JsString StringString = new JsString("string");
    internal static readonly JsString NumberString = new JsString("number");
    internal static readonly JsString BigIntString = new JsString("bigint");
    internal static readonly JsString SymbolString = new JsString("symbol");
    internal static readonly JsString DefaultString = new JsString("default");
    internal static readonly JsString NumberZeroString = new JsString("0");
    internal static readonly JsString NumberOneString = new JsString("1");
    internal static readonly JsString TrueString = new JsString("true");
    internal static readonly JsString FalseString = new JsString("false");
    internal static readonly JsString LengthString = new JsString("length");
    internal static readonly JsValue CommaString = new JsString(",");

    internal string _value;

    static JsString()
    {
        _charToJsValue = new JsString[AsciiMax + 1];
        _charToStringJsValue = new JsString[AsciiMax + 1];

        for (var i = 0; i <= AsciiMax; i++)
        {
            _charToJsValue[i] = new JsString((char) i);
            _charToStringJsValue[i] = new JsString(((char) i).ToString());
        }

        _intToStringJsValue = new JsString[1024];
        for (var i = 0; i < _intToStringJsValue.Length; ++i)
        {
            _intToStringJsValue[i] = new JsString(TypeConverter.ToString(i));
        }
    }

    public JsString(string value) : this(value, InternalTypes.String)
    {
    }

    private JsString(string value, InternalTypes type) : base(type)
    {
        _value = value;
    }

    public JsString(char value) : base(Types.String)
    {
        _value = value.ToString();
    }

    public override object ToObject()
    {
        return _value;
    }

    public static bool operator ==(JsValue? a, JsString? b)
    {
        if (a is JsString s && b is not null)
        {
            return s.ToString() == b.ToString();
        }

        if (a is null)
        {
            return b is null;
        }

        return b is not null && a.Equals(b);
    }

    public static bool operator ==(JsString? a, JsValue? b)
    {
        if (a is not null && b is JsString s)
        {
            return s.ToString() == b.ToString();
        }

        if (a is null)
        {
            return b is null;
        }

        return b is not null && a.Equals(b);
    }

    public static bool operator !=(JsString a, JsValue b)
    {
        return !(a == b);
    }

    public static bool operator !=(JsValue a, JsString b)
    {
        return !(a == b);
    }

    public virtual char this[int index] => _value[index];

    public virtual JsString Append(JsValue jsValue)
    {
        return new ConcatenatedString(string.Concat(_value, TypeConverter.ToString(jsValue)));
    }

    internal virtual JsString EnsureCapacity(int capacity)
    {
        return new ConcatenatedString(_value, capacity);
    }

    internal virtual bool IsNullOrEmpty()
    {
        return string.IsNullOrEmpty(_value);
    }

    public virtual int Length => _value.Length;

    internal static JsString Create(string value)
    {
        if (value.Length > 1)
        {
            return new JsString(value);
        }

        if (value.Length == 0)
        {
            return Empty;
        }

        var i = (uint) value[0];
        var temp = _charToStringJsValue;
        if (i < (uint) temp.Length)
        {
            return temp[i];
        }
        return new JsString(value);
    }

    internal static JsString Create(char value)
    {
        var temp = _charToJsValue;
        if (value < (uint) temp.Length)
        {
            return temp[value];
        }

        return new JsString(value);
    }

    internal static JsString Create(int value)
    {
        var temp = _intToStringJsValue;
        if (value < (uint) temp.Length)
        {
            return temp[value];
        }

        return new JsString(TypeConverter.ToString(value));
    }

    internal static JsValue Create(uint value)
    {
        var temp = _intToStringJsValue;
        if (value < (uint) temp.Length)
        {
            return temp[value];
        }

        return new JsString(TypeConverter.ToString(value));
    }

    internal static JsValue Create(ulong value)
    {
        var temp = _intToStringJsValue;
        if (value < (uint) temp.Length)
        {
            return temp[value];
        }

        return new JsString(TypeConverter.ToString(value));
    }

    public override string ToString()
    {
        return _value;
    }

    internal int IndexOf(string value, StringComparison comparisonType)
    {
        return ToString().IndexOf(value, comparisonType);
    }

    internal int IndexOf(char value)
    {
        return ToString().IndexOf(value);
    }

    internal string Substring(int startIndex, int length)
    {
        return ToString().Substring(startIndex, length);
    }

    internal string Substring(int startIndex)
    {
        return ToString().Substring(startIndex);
    }

    public override bool Equals(JsValue? obj)
    {
        return Equals(obj as JsString);
    }

    public bool Equals(JsString? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return _value == other.ToString();
    }

    public override bool IsLooselyEqual(JsValue value)
    {
        if (value is JsString jsString)
        {
            return Equals(jsString);
        }

        if (value.IsBigInt())
        {
            return value.IsBigInt() && TypeConverter.TryStringToBigInt(ToString(), out var temp) && temp == value.AsBigInt();
        }

        return base.IsLooselyEqual(value);
    }

    public override bool Equals(object obj)
    {
        return Equals(obj as JsString);
    }

    public override int GetHashCode()
    {
        return _value.GetHashCode();
    }

    internal sealed class ConcatenatedString : JsString
    {
        private StringBuilder? _stringBuilder;
        private bool _dirty;

        internal ConcatenatedString(string value, int capacity = 0)
            : base(value, InternalTypes.String | InternalTypes.RequiresCloning)
        {
            if (capacity > 0)
            {
                _stringBuilder = new StringBuilder(value, capacity);
            }
            else
            {
                _value = value;
            }
        }

        public override string ToString()
        {
            if (_dirty)
            {
                _value = _stringBuilder!.ToString();
                _dirty = false;
            }

            return _value;
        }

        public override char this[int index] => _stringBuilder?[index] ?? _value[index];

        public override JsString Append(JsValue jsValue)
        {
            var value = TypeConverter.ToString(jsValue);
            if (_stringBuilder == null)
            {
                _stringBuilder = new StringBuilder(_value, _value.Length + value.Length);
            }

            _stringBuilder.Append(value);
            _dirty = true;

            return this;
        }

        internal override JsString EnsureCapacity(int capacity)
        {
            _stringBuilder!.EnsureCapacity(capacity);
            return this;
        }

        internal override bool IsNullOrEmpty()
        {
            return _stringBuilder == null && string.IsNullOrEmpty(_value)
                   || _stringBuilder != null && _stringBuilder.Length == 0;
        }

        public override int Length => _stringBuilder?.Length ?? _value?.Length ?? 0;

        public override object ToObject() => ToString();

        public override bool Equals(JsValue? other)
        {
            if (other is ConcatenatedString cs)
            {
                var stringBuilder = _stringBuilder;
                var csStringBuilder = cs._stringBuilder;

                // we cannot use StringBuilder.Equals as it also checks Capacity on full framework / pre .NET Core 3
                if (stringBuilder != null && csStringBuilder != null && stringBuilder.Length == csStringBuilder.Length)
                {
                    for (var i = 0; i < stringBuilder.Length; ++i)
                    {
                        if (stringBuilder[i] != csStringBuilder[i])
                        {
                            return false;
                        }
                    }

                    return true;
                }

                return ToString() == cs.ToString();
            }

            if (other is JsString jsString)
            {
                if (jsString._value.Length != Length)
                {
                    return false;
                }

                return ToString() == jsString._value;
            }

            return base.Equals(other);
        }

        public override int GetHashCode()
        {
            return _stringBuilder?.GetHashCode() ?? _value.GetHashCode();
        }

        internal override JsValue DoClone()
        {
            return new JsString(ToString());
        }
    }
}
