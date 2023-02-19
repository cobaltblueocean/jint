using System.Globalization;
using Jint.Collections;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Date;

/// <summary>
/// https://tc39.es/ecma262/#sec-date-constructor
/// </summary>
internal sealed class DateConstructor : Constructor
{
    internal static readonly DateTime Epoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static readonly string[] DefaultFormats = {
        "yyyy-MM-dd",
        "yyyy-MM",
        "yyyy"
    };

    private static readonly string[] SecondaryFormats = {
        "yyyy-MM-ddTHH:mm:ss.FFF",
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-ddTHH:mm",

        // Formats used in DatePrototype toString methods
        "ddd MMM dd yyyy HH:mm:ss 'GMT'K",
        "ddd MMM dd yyyy",
        "HH:mm:ss 'GMT'K",

        // standard formats
        "yyyy-M-dTH:m:s.FFFK",
        "yyyy/M/dTH:m:s.FFFK",
        "yyyy-M-dTH:m:sK",
        "yyyy/M/dTH:m:sK",
        "yyyy-M-dTH:mK",
        "yyyy/M/dTH:mK",
        "yyyy-M-d H:m:s.FFFK",
        "yyyy/M/d H:m:s.FFFK",
        "yyyy-M-d H:m:sK",
        "yyyy/M/d H:m:sK",
        "yyyy-M-d H:mK",
        "yyyy/M/d H:mK",
        "yyyy-M-dK",
        "yyyy/M/dK",
        "yyyy-MK",
        "yyyy/MK",
        "yyyyK",
        "THH:mm:ss.FFFK",
        "THH:mm:ssK",
        "THH:mmK",
        "THHK"
    };

    private static readonly JsString _functionName = new JsString("Date");

    internal DateConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new DatePrototype(engine, this, objectPrototype);
        _length = new PropertyDescriptor(7, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal DatePrototype PrototypeObject { get; }

    protected override void Initialize()
    {
        const PropertyFlag LengthFlags = PropertyFlag.Configurable;
        const PropertyFlag PropertyFlags = PropertyFlag.Configurable | PropertyFlag.Writable;

        var properties = new PropertyDictionary(3, checkExistingKeys: false)
        {
            ["parse"] = new(new ClrFunctionInstance(Engine, "parse", Parse, 1, LengthFlags), PropertyFlags),
            ["UTC"] = new(new ClrFunctionInstance(Engine, "UTC", Utc, 7, LengthFlags), PropertyFlags),
            ["now"] = new(new ClrFunctionInstance(Engine, "now", Now, 0, LengthFlags), PropertyFlags)
        };
        SetProperties(properties);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-date.parse
    /// </summary>
    private JsValue Parse(JsValue thisObj, JsValue[] arguments)
    {
        var dateString = TypeConverter.ToString(arguments.At(0));
        var date = ParseFromString(dateString);
        return date.ToJsValue();
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-date.parse
    /// </summary>
    private DatePresentation ParseFromString(string date)
    {
        var negative = date.StartsWith("-");
        if (negative)
        {
            date = date.Substring(1);
        }

        var startParen = date.IndexOf('(');
        if (startParen != -1)
        {
            // informative text
            date = date.Substring(0, startParen);
        }

        date = date.Trim();

        if (!DateTime.TryParseExact(date, DefaultFormats, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var result))
        {
            if (!DateTime.TryParseExact(date, SecondaryFormats, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out result))
            {
                if (!DateTime.TryParse(date, Engine.Options.Culture, DateTimeStyles.AdjustToUniversal, out result))
                {
                    if (!DateTime.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out result))
                    {
                        // fall back to trying with MimeKit
                        if (DateUtils.TryParse(date, out var mimeKitResult))
                        {
                            return FromDateTimeOffset(mimeKitResult);
                        }

                        // unrecognized dates should return NaN (15.9.4.2)
                        return DatePresentation.NaN;
                    }
                }
            }
        }

        return FromDateTime(result, negative);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-date.utc
    /// </summary>
    private static JsValue Utc(JsValue thisObj, JsValue[] arguments)
    {
        var y = TypeConverter.ToNumber(arguments.At(0));
        var m = TypeConverter.ToNumber(arguments.At(1, JsNumber.PositiveZero));
        var dt = TypeConverter.ToNumber(arguments.At(2, JsNumber.PositiveOne));
        var h = TypeConverter.ToNumber(arguments.At(3, JsNumber.PositiveZero));
        var min = TypeConverter.ToNumber(arguments.At(4, JsNumber.PositiveZero));
        var s = TypeConverter.ToNumber(arguments.At(5, JsNumber.PositiveZero));
        var milli = TypeConverter.ToNumber(arguments.At(6, JsNumber.PositiveZero));

        var yInteger = TypeConverter.ToInteger(y);
        if (!double.IsNaN(y) && 0 <= yInteger && yInteger <= 99)
        {
            y  = yInteger + 1900;
        }

        var finalDate = DatePrototype.MakeDate(
            DatePrototype.MakeDay(y, m, dt),
            DatePrototype.MakeTime(h, min, s, milli));

        return finalDate.TimeClip().ToJsValue();
    }

    private static JsValue Now(JsValue thisObj, JsValue[] arguments)
    {
        return (long) (DateTime.UtcNow - Epoch).TotalMilliseconds;
    }

    protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
    {
        return PrototypeObject.ToString(Construct(Arguments.Empty, thisObject), Arguments.Empty);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-date
    /// </summary>
    public override ObjectInstance Construct(JsValue[] arguments, JsValue newTarget)
    {
        // fast path is building default, new Date()
        if (arguments.Length == 0 || newTarget.IsUndefined())
        {
            return OrdinaryCreateFromConstructor(
                newTarget,
                static intrinsics => intrinsics.Date.PrototypeObject,
                static (engine, _, dateValue) => new JsDate(engine, dateValue),
                (DateTime.UtcNow - Epoch).TotalMilliseconds);
        }

        return ConstructUnlikely(arguments, newTarget);
    }

    private JsDate ConstructUnlikely(JsValue[] arguments, JsValue newTarget)
    {
        DatePresentation dv;
        if (arguments.Length == 1)
        {
            if (arguments[0] is JsDate date)
            {
                return Construct(date._dateValue);
            }

            var v = TypeConverter.ToPrimitive(arguments[0]);
            if (v.IsString())
            {
                var value = ParseFromString(v.ToString());
                return Construct(value);
            }

            dv = TypeConverter.ToNumber(v);
        }
        else
        {
            var y = TypeConverter.ToNumber(arguments.At(0));
            var m = TypeConverter.ToNumber(arguments.At(1));
            var dt = TypeConverter.ToNumber(arguments.At(2, JsNumber.PositiveOne));
            var h = TypeConverter.ToNumber(arguments.At(3, JsNumber.PositiveZero));
            var min = TypeConverter.ToNumber(arguments.At(4, JsNumber.PositiveZero));
            var s = TypeConverter.ToNumber(arguments.At(5, JsNumber.PositiveZero));
            var milli = TypeConverter.ToNumber(arguments.At(6, JsNumber.PositiveZero));

            var yInteger = TypeConverter.ToInteger(y);
            if (!double.IsNaN(y) && 0 <= yInteger && yInteger <= 99)
            {
                y += 1900;
            }

            var finalDate = DatePrototype.MakeDate(
                DatePrototype.MakeDay(y, m, dt),
                DatePrototype.MakeTime(h, min, s, milli));

            dv = PrototypeObject.Utc(finalDate).TimeClip();
        }

        return OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.Date.PrototypeObject,
            static (engine, _, dateValue) => new JsDate(engine, dateValue), dv);
    }

    public JsDate Construct(DateTimeOffset value) => Construct(value.UtcDateTime);

    public JsDate Construct(DateTime value) => Construct(FromDateTime(value));

    public JsDate Construct(long time)
    {
        return OrdinaryCreateFromConstructor(
            Undefined,
            static intrinsics => intrinsics.Date.PrototypeObject,
            static (engine, _, dateValue) => new JsDate(engine, dateValue), time);
    }

    private JsDate Construct(DatePresentation time)
    {
        return OrdinaryCreateFromConstructor(
            Undefined,
            static intrinsics => intrinsics.Date.PrototypeObject,
            static (engine, _, dateValue) => new JsDate(engine, dateValue), time);
    }

    private static long FromDateTimeOffset(DateTimeOffset dt, bool negative = false)
    {
        var dateAsUtc = dt.ToUniversalTime();

        double result;
        if (negative)
        {
            var zero = (Epoch - DateTime.MinValue).TotalMilliseconds;
            result = zero - TimeSpan.FromTicks(dateAsUtc.Ticks).TotalMilliseconds;
            result *= -1;
        }
        else
        {
            result = (dateAsUtc - Epoch).TotalMilliseconds;
        }

        return (long) System.Math.Floor(result);
    }

    internal DatePresentation FromDateTime(DateTime dt, bool negative = false)
    {
        if (dt == DateTime.MinValue)
        {
            return DatePresentation.MinValue;
        }

        if (dt == DateTime.MaxValue)
        {
            return DatePresentation.MaxValue;
        }

        var convertToUtcAfter = dt.Kind == DateTimeKind.Unspecified;

        var dateAsUtc = dt.Kind == DateTimeKind.Local
            ? dt.ToUniversalTime()
            : DateTime.SpecifyKind(dt, DateTimeKind.Utc);

        DatePresentation result;
        if (negative)
        {
            result = DatePrototype.MakeDate(
                DatePrototype.MakeDay(-1 * dateAsUtc.Year, dateAsUtc.Month - 1, dateAsUtc.Day),
                DatePrototype.MakeTime(dateAsUtc.Hour, dateAsUtc.Minute, dateAsUtc.Second, dateAsUtc.Millisecond)
            );
        }
        else
        {
            result = (long) (dateAsUtc - Epoch).TotalMilliseconds;
        }

        if (convertToUtcAfter)
        {
            result = PrototypeObject.Utc(result);
        }

        return result;
    }
}
