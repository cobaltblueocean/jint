﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Runtime.CompilerServices;
using Jint.Collections;
using Jint.Native.Array;
using Jint.Native.Boolean;
using Jint.Native.Date;
using Jint.Native.Function;
using Jint.Native.Number;
using Jint.Native.RegExp;
using Jint.Native.String;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Object
{
    public class ObjectInstance : JsValue, IEquatable<ObjectInstance>
    {
        private bool _initialized;
        private readonly ObjectClass _class;

        internal PropertyDictionary _properties;
        internal SymbolDictionary _symbols;

        internal ObjectInstance _prototype;
        protected readonly Engine _engine;

        public ObjectInstance(Engine engine) : this(engine, ObjectClass.Object)
        {
        }

        internal ObjectInstance(Engine engine, ObjectClass objectClass, InternalTypes type = InternalTypes.Object)
            : base(type)
        {
            _engine = engine;
            _class = objectClass;
            // if engine is ready, we can take default prototype for object
            _prototype = engine.Realm.Intrinsics?.Object?.PrototypeObject;
            Extensible = true;
        }

        public Engine Engine
        {
            [DebuggerStepThrough]
            get => _engine;
        }

        /// <summary>
        /// The prototype of this object.
        /// </summary>
        public ObjectInstance Prototype
        {
            [DebuggerStepThrough]
            get => GetPrototypeOf();
        }

        /// <summary>
        /// If true, own properties may be added to the
        /// object.
        /// </summary>
        public virtual bool Extensible { get; private set; }

        internal PropertyDictionary Properties
        {
            [DebuggerStepThrough]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _properties;
        }

        /// <summary>
        /// A value indicating a specification defined classification of objects.
        /// </summary>
        internal ObjectClass Class
        {
            [DebuggerStepThrough]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _class;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-construct
        /// </summary>
        internal static ObjectInstance Construct(IConstructor f, JsValue[] argumentsList = null, IConstructor newTarget = null)
        {
            newTarget ??= f;
            argumentsList ??= System.Array.Empty<JsValue>();
            return f.Construct(argumentsList, (JsValue) newTarget);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-speciesconstructor
        /// </summary>
        internal static IConstructor SpeciesConstructor(ObjectInstance o, IConstructor defaultConstructor)
        {
            var c = o.Get(CommonProperties.Constructor);
            if (c.IsUndefined())
            {
                return defaultConstructor;
            }

            var oi = c as ObjectInstance;
            if (oi is null)
            {
                ExceptionHelper.ThrowTypeError(o._engine.Realm);
            }

            var s = oi.Get(GlobalSymbolRegistry.Species);
            if (s.IsNullOrUndefined())
            {
                return defaultConstructor;
            }

            if (s.IsConstructor)
            {
                return (IConstructor) s;
            }

            ExceptionHelper.ThrowTypeError(o._engine.Realm);
            return null;
        }

        internal void SetProperties(PropertyDictionary properties)
        {
            if (properties != null)
            {
                properties.CheckExistingKeys = true;
            }
            _properties = properties;
        }

        internal void SetSymbols(SymbolDictionary symbols)
        {
            _symbols = symbols;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SetProperty(JsValue property, PropertyDescriptor value)
        {
            if (property is JsString jsString)
            {
                SetProperty(jsString.ToString(), value);
            }
            else
            {
                SetPropertyUnlikely(property, value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SetProperty(string property, PropertyDescriptor value)
        {
            Key key = property;
            SetProperty(key, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SetProperty(Key property, PropertyDescriptor value)
        {
            _properties ??= new PropertyDictionary();
            _properties[property] = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SetDataProperty(string property, JsValue value)
        {
            _properties ??= new PropertyDictionary();
            _properties[property] = new PropertyDescriptor(value, PropertyFlag.ConfigurableEnumerableWritable);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void SetPropertyUnlikely(JsValue property, PropertyDescriptor value)
        {
            var propertyKey = TypeConverter.ToPropertyKey(property);
            if (!property.IsSymbol())
            {
                _properties ??= new PropertyDictionary();
                _properties[TypeConverter.ToString(propertyKey)] = value;
            }
            else
            {
                _symbols ??= new SymbolDictionary();
                _symbols[(JsSymbol) propertyKey] = value;
            }
        }

        internal void ClearProperties()
        {
            _properties?.Clear();
            _symbols?.Clear();
        }

        public virtual IEnumerable<KeyValuePair<JsValue, PropertyDescriptor>> GetOwnProperties()
        {
            EnsureInitialized();

            if (_properties != null)
            {
                foreach (var pair in _properties)
                {
                    yield return new KeyValuePair<JsValue, PropertyDescriptor>(new JsString(pair.Key), pair.Value);
                }
            }

            if (_symbols != null)
            {
                foreach (var pair in _symbols)
                {
                    yield return new KeyValuePair<JsValue, PropertyDescriptor>(pair.Key, pair.Value);
                }
            }
        }



        public virtual List<JsValue> GetOwnPropertyKeys(Types types = Types.String | Types.Symbol)
        {
            EnsureInitialized();

            var propertyKeys = new List<JsValue>();
            if ((types & Types.String) != 0)
            {
                propertyKeys.AddRange(GetInitialOwnStringPropertyKeys());
            }

            var keys = new List<JsValue>(_properties?.Count ?? 0 + _symbols?.Count ?? 0 + propertyKeys.Count);
            List<JsValue> symbolKeys = null;

            if ((types & Types.String) != 0 && _properties != null)
            {
                foreach (var pair in _properties)
                {
                    var isArrayIndex = ulong.TryParse(pair.Key, out var index) && index < ArrayOperations.MaxArrayLength;
                    if (isArrayIndex)
                    {
                        keys.Add(JsString.Create(index));
                    }
                    else
                    {
                        propertyKeys.Add(new JsString(pair.Key));
                    }
                }
            }

            keys.Sort((v1, v2) => TypeConverter.ToNumber(v1).CompareTo(TypeConverter.ToNumber(v2)));
            keys.AddRange(propertyKeys);

            if ((types & Types.Symbol) != 0 && _symbols != null)
            {
                foreach (var pair in _symbols)
                {
                    symbolKeys ??= new List<JsValue>();
                    symbolKeys.Add(pair.Key);
                }
            }

            if (symbolKeys != null)
            {
                keys.AddRange(symbolKeys);
            }

            return keys;
        }

        internal virtual IEnumerable<JsValue> GetInitialOwnStringPropertyKeys() => Enumerable.Empty<JsValue>();

        protected virtual void AddProperty(JsValue property, PropertyDescriptor descriptor)
        {
            SetProperty(property, descriptor);
        }

        protected virtual bool TryGetProperty(JsValue property, out PropertyDescriptor descriptor)
        {
            descriptor = null;

            var key = TypeConverter.ToPropertyKey(property);
            if (!key.IsSymbol())
            {
                return _properties?.TryGetValue(TypeConverter.ToString(key), out descriptor) == true;
            }

            return _symbols?.TryGetValue((JsSymbol) key, out descriptor) == true;
        }

        public override bool HasOwnProperty(JsValue property)
        {
            EnsureInitialized();

            var key = TypeConverter.ToPropertyKey(property);
            if (!key.IsSymbol())
            {
                return _properties?.ContainsKey(TypeConverter.ToString(key)) == true;
            }

            return _symbols?.ContainsKey((JsSymbol) key) == true;
        }

        public virtual void RemoveOwnProperty(JsValue property)
        {
            EnsureInitialized();

            var key = TypeConverter.ToPropertyKey(property);
            if (!key.IsSymbol())
            {
                _properties?.Remove(TypeConverter.ToString(key));
                return;
            }

            _symbols?.Remove((JsSymbol) key);
        }

        public override JsValue Get(JsValue property, JsValue receiver)
        {
            var desc = GetOwnProperty(property);

            if (desc != PropertyDescriptor.Undefined)
            {
                return UnwrapJsValue(desc, receiver);
            }

            return Prototype?.Get(property, receiver) ?? Undefined;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal JsValue UnwrapJsValue(PropertyDescriptor desc)
        {
            return UnwrapJsValue(desc, this);
        }

        internal static JsValue UnwrapJsValue(PropertyDescriptor desc, JsValue thisObject)
        {
            var value = (desc._flags & PropertyFlag.CustomJsValue) != 0
                ? desc.CustomValue
                : desc._value;

            // IsDataDescriptor inlined
            if ((desc._flags & (PropertyFlag.WritableSet | PropertyFlag.Writable)) != 0 || value is not null)
            {
                return value ?? Undefined;
            }

            return UnwrapFromGetter(desc, thisObject);
        }

        /// <summary>
        /// A rarer case.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static JsValue UnwrapFromGetter(PropertyDescriptor desc, JsValue thisObject)
        {
            var getter = desc.Get ?? Undefined;
            if (getter.IsUndefined())
            {
                return Undefined;
            }

            var functionInstance = (FunctionInstance) getter;
            return functionInstance._engine.Call(functionInstance, thisObject, Arguments.Empty, expression: null);
        }

        /// <summary>
        /// Returns the Property Descriptor of the named
        /// own property of this object, or undefined if
        /// absent.
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-8.12.1
        /// </summary>
        public virtual PropertyDescriptor GetOwnProperty(JsValue property)
        {
            EnsureInitialized();

            PropertyDescriptor descriptor = null;
            var key = TypeConverter.ToPropertyKey(property);
            if (!key.IsSymbol())
            {
                _properties?.TryGetValue(TypeConverter.ToString(key), out descriptor);
            }
            else
            {
                _symbols?.TryGetValue((JsSymbol) key, out descriptor);
            }

            return descriptor ?? PropertyDescriptor.Undefined;
        }

        protected internal virtual void SetOwnProperty(JsValue property, PropertyDescriptor desc)
        {
            EnsureInitialized();
            SetProperty(property, desc);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-8.12.2
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PropertyDescriptor GetProperty(JsValue property)
        {
            var prop = GetOwnProperty(property);

            if (prop != PropertyDescriptor.Undefined)
            {
                return prop;
            }

            return Prototype?.GetProperty(property) ?? PropertyDescriptor.Undefined;
        }

        public bool TryGetValue(JsValue property, out JsValue value)
        {
            value = Undefined;
            var desc = GetOwnProperty(property);
            if (desc != null && desc != PropertyDescriptor.Undefined)
            {
                if (desc == PropertyDescriptor.Undefined)
                {
                    return false;
                }

                var descValue = desc.Value;
                if (desc.WritableSet && !ReferenceEquals(descValue, null))
                {
                    value = descValue;
                    return true;
                }

                var getter = desc.Get ?? Undefined;
                if (getter.IsUndefined())
                {
                    value = Undefined;
                    return false;
                }

                // if getter is not undefined it must be ICallable
                var callable = getter.TryCast<ICallable>();
                value = callable.Call(this, Arguments.Empty);
                return true;
            }

            if (ReferenceEquals(Prototype, null))
            {
                return false;
            }

            return Prototype.TryGetValue(property, out value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Set(JsValue p, JsValue v, bool throwOnError)
        {
            if (!Set(p, v, this) && throwOnError)
            {
                ExceptionHelper.ThrowTypeError(_engine.Realm);
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Set(JsValue property, JsValue value)
        {
            return Set(property, value, this);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-ordinarysetwithowndescriptor
        /// </summary>
        public override bool Set(JsValue property, JsValue value, JsValue receiver)
        {
            var ownDesc = GetOwnProperty(property);

            if (ownDesc == PropertyDescriptor.Undefined)
            {
                var parent = GetPrototypeOf();
                if (parent is not null)
                {
                    return parent.Set(property, value, receiver);
                }
                ownDesc = new PropertyDescriptor(Undefined, PropertyFlag.ConfigurableEnumerableWritable);
            }

            if (ownDesc.IsDataDescriptor())
            {
                if (!ownDesc.Writable)
                {
                    return false;
                }

                if (receiver is not ObjectInstance oi)
                {
                    return false;
                }

                var existingDescriptor = oi.GetOwnProperty(property);
                if (existingDescriptor != PropertyDescriptor.Undefined)
                {
                    if (existingDescriptor.IsAccessorDescriptor())
                    {
                        return false;
                    }

                    if (!existingDescriptor.Writable)
                    {
                        return false;
                    }

                    var valueDesc = new PropertyDescriptor(value, PropertyFlag.None);
                    return oi.DefineOwnProperty(property, valueDesc);
                }
                else
                {
                    return oi.CreateDataProperty(property, value);
                }
            }

            if (ownDesc.Set is not FunctionInstance setter)
            {
                return false;
            }

            _engine.Call(setter, receiver, new[] { value }, expression: null);

            return true;
        }

        /// <summary>
        /// Returns a Boolean value indicating whether a
        /// [[Put]] operation with PropertyName can be
        /// performed.
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-8.12.4
        /// </summary>
        public bool CanPut(JsValue property)
        {
            var desc = GetOwnProperty(property);

            if (desc != PropertyDescriptor.Undefined)
            {
                if (desc.IsAccessorDescriptor())
                {
                    var set = desc.Set;
                    if (ReferenceEquals(set, null) || set.IsUndefined())
                    {
                        return false;
                    }

                    return true;
                }

                return desc.Writable;
            }

            if (ReferenceEquals(Prototype, null))
            {
                return Extensible;
            }

            var inherited = Prototype.GetProperty(property);

            if (inherited == PropertyDescriptor.Undefined)
            {
                return Extensible;
            }

            if (inherited.IsAccessorDescriptor())
            {
                var set = inherited.Set;
                if (ReferenceEquals(set, null) || set.IsUndefined())
                {
                    return false;
                }

                return true;
            }

            if (!Extensible)
            {
                return false;
            }

            return inherited.Writable;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-ordinary-object-internal-methods-and-internal-slots-hasproperty-p
        /// </summary>
        public virtual bool HasProperty(JsValue property)
        {
            var hasOwn = GetOwnProperty(property);
            if (hasOwn != PropertyDescriptor.Undefined)
            {
                return true;
            }

            var parent = GetPrototypeOf();
            if (parent != null)
            {
                return parent.HasProperty(property);
            }

            return false;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-deletepropertyorthrow
        /// </summary>
        public bool DeletePropertyOrThrow(JsValue property)
        {
            if (!Delete(property))
            {
                ExceptionHelper.ThrowTypeError(_engine.Realm);
            }
            return true;
        }

        /// <summary>
        /// Removes the specified named own property
        /// from the object. The flag controls failure
        /// handling.
        /// </summary>
        public virtual bool Delete(JsValue property)
        {
            var desc = GetOwnProperty(property);

            if (desc == PropertyDescriptor.Undefined)
            {
                return true;
            }

            if (desc.Configurable)
            {
                RemoveOwnProperty(property);
                return true;
            }

            return false;
        }

        public bool DefinePropertyOrThrow(JsValue property, PropertyDescriptor desc)
        {
            if (!DefineOwnProperty(property, desc))
            {
                ExceptionHelper.ThrowTypeError(_engine.Realm, "Cannot redefine property: " + property);
            }

            return true;
        }

        /// <summary>
        /// Creates or alters the named own property to
        /// have the state described by a Property
        /// Descriptor. The flag controls failure handling.
        /// </summary>
        public virtual bool DefineOwnProperty(JsValue property, PropertyDescriptor desc)
        {
            var current = GetOwnProperty(property);
            var extensible = Extensible;

            if (current == desc)
            {
                return true;
            }

            return ValidateAndApplyPropertyDescriptor(this, property, extensible, desc, current);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-validateandapplypropertydescriptor
        /// </summary>
        protected static bool ValidateAndApplyPropertyDescriptor(ObjectInstance o, JsValue property, bool extensible, PropertyDescriptor desc, PropertyDescriptor current)
        {
            var descValue = desc.Value;
            if (current == PropertyDescriptor.Undefined)
            {
                if (!extensible)
                {
                    return false;
                }

                if (o is object)
                {
                    if (desc.IsGenericDescriptor() || desc.IsDataDescriptor())
                    {
                        PropertyDescriptor propertyDescriptor;
                        if ((desc._flags & PropertyFlag.ConfigurableEnumerableWritable) == PropertyFlag.ConfigurableEnumerableWritable)
                        {
                            propertyDescriptor = new PropertyDescriptor(descValue ?? Undefined, PropertyFlag.ConfigurableEnumerableWritable);
                        }
                        else if ((desc._flags & PropertyFlag.ConfigurableEnumerableWritable) == 0)
                        {
                            propertyDescriptor = new PropertyDescriptor(descValue ?? Undefined, PropertyFlag.AllForbidden);
                        }
                        else
                        {
                            propertyDescriptor = new PropertyDescriptor(desc)
                            {
                                Value = descValue ?? Undefined
                            };
                        }

                        o.SetOwnProperty(property, propertyDescriptor);
                    }
                    else
                    {
                        var descriptor = new GetSetPropertyDescriptor(desc.Get, desc.Set, PropertyFlag.None)
                        {
                            Enumerable = desc.Enumerable,
                            Configurable = desc.Configurable
                        };

                        o.SetOwnProperty(property, descriptor);
                    }
                }

                return true;
            }

            // Step 3
            var currentGet = current.Get;
            var currentSet = current.Set;
            var currentValue = current.Value;

            // 4. If every field in Desc is absent, return true.
            if ((current._flags & (PropertyFlag.ConfigurableSet | PropertyFlag.EnumerableSet | PropertyFlag.WritableSet)) == 0 &&
                ReferenceEquals(currentGet, null) &&
                ReferenceEquals(currentSet, null) &&
                ReferenceEquals(currentValue, null))
            {
                return true;
            }

            // Step 6
            var descGet = desc.Get;
            var descSet = desc.Set;
            if (
                current.Configurable == desc.Configurable && current.ConfigurableSet == desc.ConfigurableSet &&
                current.Writable == desc.Writable && current.WritableSet == desc.WritableSet &&
                current.Enumerable == desc.Enumerable && current.EnumerableSet == desc.EnumerableSet &&
                ((ReferenceEquals(currentGet, null) && ReferenceEquals(descGet, null)) || (!ReferenceEquals(currentGet, null) && !ReferenceEquals(descGet, null) && SameValue(currentGet, descGet))) &&
                ((ReferenceEquals(currentSet, null) && ReferenceEquals(descSet, null)) || (!ReferenceEquals(currentSet, null) && !ReferenceEquals(descSet, null) && SameValue(currentSet, descSet))) &&
                ((ReferenceEquals(currentValue, null) && ReferenceEquals(descValue, null)) || (!ReferenceEquals(currentValue, null) && !ReferenceEquals(descValue, null) && currentValue == descValue))
            )
            {
                return true;
            }

            if (!current.Configurable)
            {
                if (desc.Configurable)
                {
                    return false;
                }

                if (desc.EnumerableSet && (desc.Enumerable != current.Enumerable))
                {
                    return false;
                }
            }

            if (!desc.IsGenericDescriptor())
            {
                if (current.IsDataDescriptor() != desc.IsDataDescriptor())
                {
                    if (!current.Configurable)
                    {
                        return false;
                    }

                    if (o is object)
                    {
                        var flags = current.Flags & ~(PropertyFlag.Writable | PropertyFlag.WritableSet | PropertyFlag.CustomJsValue);
                        if (current.IsDataDescriptor())
                        {
                            o.SetOwnProperty(property, current = new GetSetPropertyDescriptor(
                                get: Undefined,
                                set: Undefined,
                                flags
                            ));
                        }
                        else
                        {
                            o.SetOwnProperty(property, current = new PropertyDescriptor(
                                value: Undefined,
                                flags
                            ));
                        }
                    }
                }
                else if (current.IsDataDescriptor() && desc.IsDataDescriptor())
                {
                    if (!current.Configurable)
                    {
                        if (!current.Writable && desc.Writable)
                        {
                            return false;
                        }

                        if (!current.Writable)
                        {
                            if (!ReferenceEquals(descValue, null) && !SameValue(descValue, currentValue))
                            {
                                return false;
                            }
                        }
                    }
                }
                else if (current.IsAccessorDescriptor() && desc.IsAccessorDescriptor())
                {
                    if (!current.Configurable)
                    {
                        if ((!ReferenceEquals(descSet, null) && !SameValue(descSet, currentSet ?? Undefined))
                            ||
                            (!ReferenceEquals(descGet, null) && !SameValue(descGet, currentGet ?? Undefined)))
                        {
                            return false;
                        }
                    }
                }
            }

            if (o is object)
            {
                if (!ReferenceEquals(descValue, null))
                {
                    current.Value = descValue;
                }

                if (desc.WritableSet)
                {
                    current.Writable = desc.Writable;
                }

                if (desc.EnumerableSet)
                {
                    current.Enumerable = desc.Enumerable;
                }

                if (desc.ConfigurableSet)
                {
                    current.Configurable = desc.Configurable;
                }

                PropertyDescriptor mutable = null;
                if (!ReferenceEquals(descGet, null))
                {
                    mutable = new GetSetPropertyDescriptor(mutable ?? current);
                    ((GetSetPropertyDescriptor) mutable).SetGet(descGet);
                }

                if (!ReferenceEquals(descSet, null))
                {
                    mutable = new GetSetPropertyDescriptor(mutable ?? current);
                    ((GetSetPropertyDescriptor) mutable).SetSet(descSet);
                }

                if (mutable != null)
                {
                    // replace old with new type that supports get and set
                    o.FastSetProperty(property, mutable);
                }
            }

            return true;
        }

        /// <summary>
        /// Optimized version of [[Put]] when the property is known to be undeclared already
        /// </summary>
        public void FastAddProperty(JsValue name, JsValue value, bool writable, bool enumerable, bool configurable)
        {
            SetOwnProperty(name, new PropertyDescriptor(value, writable, enumerable, configurable));
        }

        /// <summary>
        /// Optimized version of [[Put]] when the property is known to be already declared
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        public void FastSetProperty(JsValue name, PropertyDescriptor value)
        {
            SetOwnProperty(name, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            // we need to set flag eagerly to prevent wrong recursion
            _initialized = true;
            Initialize();
        }

        protected virtual void Initialize()
        {
        }

        public override object ToObject()
        {
            return ToObject(new ObjectTraverseStack(_engine));
        }

        private object ToObject(ObjectTraverseStack stack)
        {
            if (this is IObjectWrapper wrapper)
            {
                return wrapper.Target;
            }

            stack.Enter(this);
            object converted = null;
            switch (Class)
            {
                case ObjectClass.Array:
                    if (this is ArrayInstance arrayInstance)
                    {
                        var result = new object[arrayInstance.Length];
                        for (uint i = 0; i < result.Length; i++)
                        {
                            var value = arrayInstance[i];
                            object valueToSet = null;
                            if (!value.IsUndefined())
                            {
                                valueToSet = value is ObjectInstance oi
                                    ? oi.ToObject(stack)
                                    : value.ToObject();
                            }
                            result[i] = valueToSet;
                        }
                        converted = result;
                    }
                    break;

                case ObjectClass.String:
                    if (this is StringInstance stringInstance)
                    {
                        converted = stringInstance.StringData.ToString();
                    }
                    break;

                case ObjectClass.Date:
                    if (this is DateInstance dateInstance)
                    {
                        converted = dateInstance.ToDateTime();
                    }
                    break;

                case ObjectClass.Boolean:
                    if (this is BooleanInstance booleanInstance)
                    {
                        converted = ((JsBoolean) booleanInstance.BooleanData)._value
                            ? JsBoolean.BoxedTrue
                            : JsBoolean.BoxedFalse;
                    }
                    break;

                case ObjectClass.Function:
                    if (this is FunctionInstance function)
                    {
                        converted = new Func<JsValue, JsValue[], JsValue>(
                            (thisVal, args) => function.Engine.Invoke(function, (object) thisVal, args));
                    }

                    break;

                case ObjectClass.Number:
                    if (this is NumberInstance numberInstance)
                    {
                        converted = numberInstance.NumberData._value;
                    }
                    break;

                case ObjectClass.RegExp:
                    if (this is RegExpInstance regeExpInstance)
                    {
                        converted = regeExpInstance.Value;
                    }
                    break;

                case ObjectClass.Arguments:
                case ObjectClass.Object:
                    IDictionary<string, object> o = new ExpandoObject();
                    foreach (var p in GetOwnProperties())
                    {
                        if (!p.Value.Enumerable)
                        {
                            continue;
                        }

                        var key = p.Key.ToString();
                        var propertyValue = Get(p.Key, this);
                        var value = propertyValue is ObjectInstance oi
                            ? oi.ToObject(stack)
                            : propertyValue.ToObject();
                        o.Add(key, value);
                    }

                    converted = o;
                    break;
                default:
                    converted = this;
                    break;
            }

            stack.Exit();
            return converted;
        }

        /// <summary>
        /// Handles the generic find of (callback[, thisArg])
        /// </summary>
        internal virtual bool FindWithCallback(
            JsValue[] arguments,
            out uint index,
            out JsValue value,
            bool visitUnassigned)
        {
            long GetLength()
            {
                var desc = GetProperty(CommonProperties.Length);
                var descValue = desc.Value;
                double len;
                if (desc.IsDataDescriptor() && !ReferenceEquals(descValue, null))
                {
                    len = TypeConverter.ToNumber(descValue);
                }
                else
                {
                    var getter = desc.Get ?? Undefined;
                    if (getter.IsUndefined())
                    {
                        len = 0;
                    }
                    else
                    {
                        // if getter is not undefined it must be ICallable
                        len = TypeConverter.ToNumber(((ICallable) getter).Call(this, Arguments.Empty));
                    }
                }

                return (long) System.Math.Max(
                    0,
                    System.Math.Min(len, ArrayOperations.MaxArrayLikeLength));
            }

            bool TryGetValue(uint idx, out JsValue jsValue)
            {
                var property = JsString.Create(idx);
                var kPresent = HasProperty(property);
                jsValue = kPresent ? Get(property, this) : Undefined;
                return kPresent;
            }

            if (GetLength() == 0)
            {
                index = 0;
                value = Undefined;
                return false;
            }

            var callbackfn = arguments.At(0);
            var thisArg = arguments.At(1);
            var callable = GetCallable(callbackfn);

            var args = _engine._jsValueArrayPool.RentArray(3);
            args[2] = this;
            var length = GetLength();
            for (uint k = 0; k < length; k++)
            {
                if (TryGetValue(k, out var kvalue) || visitUnassigned)
                {
                    args[0] = kvalue;
                    args[1] = k;
                    var testResult = callable.Call(thisArg, args);
                    if (TypeConverter.ToBoolean(testResult))
                    {
                        index = k;
                        value = kvalue;
                        return true;
                    }
                }
            }

            _engine._jsValueArrayPool.ReturnArray(args);

            index = 0;
            value = Undefined;
            return false;
        }

        internal ICallable GetCallable(JsValue source)
        {
            if (source is ICallable callable)
            {
                return callable;
            }

            ExceptionHelper.ThrowTypeError(_engine.Realm, "Argument must be callable");
            return null;
        }

        internal bool IsConcatSpreadable
        {
            get
            {
                var spreadable = Get(GlobalSymbolRegistry.IsConcatSpreadable, this);
                if (!spreadable.IsUndefined())
                {
                    return TypeConverter.ToBoolean(spreadable);
                }
                return IsArray();
            }
        }

        public virtual bool IsArrayLike => TryGetValue(CommonProperties.Length, out var lengthValue)
                                           && lengthValue.IsNumber()
                                           && ((JsNumber) lengthValue)._value >= 0;

        // safe default
        internal virtual bool HasOriginalIterator => false;

        internal override bool IsIntegerIndexedArray => false;

        public virtual uint Length => (uint) TypeConverter.ToLength(Get(CommonProperties.Length));

        public virtual bool PreventExtensions()
        {
            Extensible = false;
            return true;
        }

        protected internal virtual ObjectInstance GetPrototypeOf()
        {
            return _prototype;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-ordinarysetprototypeof
        /// </summary>
        public virtual bool SetPrototypeOf(JsValue value)
        {
            if (!value.IsObject() && !value.IsNull())
            {
                ExceptionHelper.ThrowArgumentException();
            }

            var current = _prototype ?? Null;
            if (ReferenceEquals(value, current))
            {
                return true;
            }

            if (!Extensible)
            {
                return false;
            }

            if (value.IsNull())
            {
                _prototype = null;
                return true;
            }

            // validate chain
            var p = value as ObjectInstance;
            bool done = false;
            while (!done)
            {
                if (p is null)
                {
                    done = true;
                }
                else if (ReferenceEquals(p, this))
                {
                    return false;
                }
                else
                {
                    p = p._prototype;
                }
            }

            _prototype = value as ObjectInstance;
            return true;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-setfunctionname
        /// </summary>
        internal void SetFunctionName(JsValue name, string prefix = null)
        {
            if (name is JsSymbol symbol)
            {
                name = symbol._value.IsUndefined()
                    ? JsString.Empty
                    : new JsString("[" + symbol._value + "]");
            }
            if (!string.IsNullOrWhiteSpace(prefix))
            {
                name = prefix + " " + name;
            }

            DefinePropertyOrThrow(CommonProperties.Name, new PropertyDescriptor(name, PropertyFlag.Configurable));
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-createmethodproperty
        /// </summary>
        internal virtual bool CreateMethodProperty(JsValue p, JsValue v)
        {
            var newDesc = new PropertyDescriptor(v, PropertyFlag.NonEnumerable);
            return DefineOwnProperty(p, newDesc);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-createdatapropertyorthrow
        /// </summary>
        internal bool CreateDataProperty(JsValue p, JsValue v)
        {
            var newDesc = new PropertyDescriptor(v, PropertyFlag.ConfigurableEnumerableWritable);
            return DefineOwnProperty(p, newDesc);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-createdataproperty
        /// </summary>
        internal bool CreateDataPropertyOrThrow(JsValue p, JsValue v)
        {
            if (!CreateDataProperty(p, v))
            {
                ExceptionHelper.ThrowTypeError(_engine.Realm);
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ICallable GetMethod(JsValue property)
        {
            return GetMethod(_engine.Realm, this, property);
        }

        internal static ICallable GetMethod(Realm realm, JsValue v, JsValue p)
        {
            var jsValue = v.Get(p);
            if (jsValue.IsNullOrUndefined())
            {
                return null;
            }

            var callable = jsValue as ICallable;
            if (callable is null)
            {
                ExceptionHelper.ThrowTypeError(realm, "Value returned for property '" + p + "' of object is not a function");
            }
            return callable;
        }

        internal void CopyDataProperties(
            ObjectInstance target,
            HashSet<JsValue> excludedItems)
        {
            var keys = GetOwnPropertyKeys();
            for (var i = 0; i < keys.Count; i++)
            {
                var key = keys[i];
                if (excludedItems == null || !excludedItems.Contains(key))
                {
                    var desc = GetOwnProperty(key);
                    if (desc.Enumerable)
                    {
                        target.CreateDataProperty(key, UnwrapJsValue(desc, this));
                    }
                }
            }
        }

        internal ArrayInstance EnumerableOwnPropertyNames(EnumerableOwnPropertyNamesKind kind)
        {
            var ownKeys = GetOwnPropertyKeys(Types.String);

            var array = Engine.Realm.Intrinsics.Array.ArrayCreate((uint) ownKeys.Count);
            uint index = 0;

            for (var i = 0; i < ownKeys.Count; i++)
            {
                var property = ownKeys[i];

                if (!property.IsString())
                {
                    continue;
                }

                var desc = GetOwnProperty(property);
                if (desc != PropertyDescriptor.Undefined && desc.Enumerable)
                {
                    if (kind == EnumerableOwnPropertyNamesKind.Key)
                    {
                        array.SetIndexValue(index, property, updateLength: false);
                    }
                    else
                    {
                        var value = Get(property);
                        if (kind == EnumerableOwnPropertyNamesKind.Value)
                        {
                            array.SetIndexValue(index, value, updateLength: false);
                        }
                        else
                        {
                            var objectInstance = _engine.Realm.Intrinsics.Array.ArrayCreate(2);
                            objectInstance.SetIndexValue(0, property, updateLength: false);
                            objectInstance.SetIndexValue(1, value, updateLength: false);
                            array.SetIndexValue(index, objectInstance, updateLength: false);
                        }
                    }

                    index++;
                }
            }

            array.SetLength(index);
            return array;
        }

        internal enum EnumerableOwnPropertyNamesKind
        {
            Key,
            Value,
            KeyValue
        }

        internal ObjectInstance AssertThisIsObjectInstance(JsValue value, string methodName)
        {
            var instance = value as ObjectInstance;
            if (instance is null)
            {
                ThrowIncompatibleReceiver(value, methodName);
            }
            return instance;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ThrowIncompatibleReceiver(JsValue value, string methodName)
        {
            ExceptionHelper.ThrowTypeError(_engine.Realm, $"Method {methodName} called on incompatible receiver {value}");
        }

        public override bool Equals(JsValue obj)
        {
            return Equals(obj as ObjectInstance);
        }

        public bool Equals(ObjectInstance other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return false;
        }

        public override string ToString()
        {
            return TypeConverter.ToString(this);
        }
    }
}
