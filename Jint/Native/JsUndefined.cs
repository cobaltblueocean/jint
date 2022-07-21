using Jint.Runtime;

namespace Jint.Native
{
    public sealed class JsUndefined : JsValue, IEquatable<JsUndefined>
    {
        internal JsUndefined() : base(Types.Undefined)
        {
        }

        public override object ToObject()
        {
            return null!;
        }

        public override string ToString()
        {
            return "undefined";
        }

        public override bool IsLooselyEqual(JsValue value)
        {
            return ReferenceEquals(Undefined, value) || ReferenceEquals(Null, value);
        }

        public override bool Equals(JsValue? obj)
        {
            return Equals(obj as JsUndefined);
        }

        public bool Equals(JsUndefined? other)
        {
            return !ReferenceEquals(null, other);
        }
    }
}
