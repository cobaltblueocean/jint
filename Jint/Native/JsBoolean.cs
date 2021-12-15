﻿#nullable enable

using System;
using Jint.Runtime;

namespace Jint.Native
{
    public sealed class JsBoolean : JsValue, IEquatable<JsBoolean>
    {
        public static readonly JsBoolean False = new JsBoolean(false);
        public static readonly JsBoolean True = new JsBoolean(true);

        internal static readonly object BoxedTrue = true;
        internal static readonly object BoxedFalse = false;

        internal readonly bool _value;

        public JsBoolean(bool value) : base(Types.Boolean)
        {
            _value = value;
        }

        public override object ToObject()
        {
            return _value ? BoxedTrue : BoxedFalse;
        }

        public override string ToString()
        {
            return _value ? "true" : "false";
        }

        public override bool NonStrictEquals(JsValue value)
        {
            return Equals(value) || !value.IsNullOrUndefined() && !value.IsBoolean() && base.NonStrictEquals(value);
        }

        public override bool Equals(JsValue obj)
        {
            return Equals(obj as JsBoolean);
        }

        public bool Equals(JsBoolean? other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (other is null)
            {
                return false;
            }

            return _value == other._value;
        }

        public override int GetHashCode()
        {
            return _value.GetHashCode();
        }
    }
}