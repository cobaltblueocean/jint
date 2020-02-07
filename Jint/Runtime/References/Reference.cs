﻿using System;
using System.Runtime.CompilerServices;
using Jint.Native;
using Jint.Runtime.Environments;

namespace Jint.Runtime.References
{
    /// <summary>
    /// Represents the Reference Specification Type
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-8.7
    /// </summary>
    public sealed class Reference
    {
        private JsValue _baseValue;
        private JsValue _property;
        internal bool _strict;

        public Reference(JsValue baseValue, JsValue property, bool strict)
        {
            _baseValue = baseValue;
            _property = property;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public JsValue GetBase() => _baseValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public JsValue GetReferencedName() => _property;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsStrictReference() => _strict;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasPrimitiveBase()
        {
            return (_baseValue._type & InternalTypes.Primitive) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsUnresolvableReference()
        {
            return _baseValue._type == InternalTypes.Undefined;
        }

        public bool IsSuperReference()
        {
            // TODO super not implemented
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsPropertyReference()
        {
            // http://www.ecma-international.org/ecma-262/#sec-ispropertyreference
            return (_baseValue._type & (InternalTypes.Primitive | InternalTypes.Object)) != 0;
        }

        internal Reference Reassign(JsValue baseValue, JsValue name, bool strict)
        {
            _baseValue = baseValue;
            _property = name;
            _strict = strict;

            return this;
        }

        internal void AssertValid(Engine engine)
        {
            if (_strict
                && (_baseValue._type & InternalTypes.ObjectEnvironmentRecord) != 0
                && (_property == CommonProperties.Eval || _property == CommonProperties.Arguments))
            {
                ExceptionHelper.ThrowSyntaxError(engine);
            }
        }
    }
}
