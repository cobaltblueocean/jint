﻿using System.Collections.Generic;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;

namespace Jint.Native.Object
{
    public class ObjectInstance
    {
        private const string PropertyNamePrototype = "prototype";
        private const string PropertyNameConstructor = "constructor";
        private const string PropertyNameLength = "length";

        private JsValue _jsValue;

        private Dictionary<string, IPropertyDescriptor> _intrinsicProperties;
        private MruPropertyCache2<string, IPropertyDescriptor> _properties;

        private IPropertyDescriptor _prototype;
        private IPropertyDescriptor _constructor;
        private IPropertyDescriptor _length;

        public ObjectInstance(Engine engine)
        {
            Engine = engine;
        }

        public Engine Engine { get; set; }

        /// <summary>
        /// Caches the constructed JS.
        /// </summary>
        internal JsValue JsValue
        {
            get { return _jsValue = _jsValue ?? new JsValue(this); }
        }

        protected bool TryGetIntrinsicValue(JsSymbol symbol, out JsValue value)
        {
            IPropertyDescriptor descriptor;

            if (_intrinsicProperties != null && _intrinsicProperties.TryGetValue(symbol.AsSymbol(), out descriptor))
            {
                value = descriptor.Value;
                return true;
            }

            if (Prototype == null)
            {
                value = JsValue.Undefined;
                return false;
            }

            return Prototype.TryGetIntrinsicValue(symbol, out value);
        }

        public void SetIntrinsicValue(string name, JsValue value, bool writable, bool enumerable, bool configurable)
        {
            SetOwnProperty(name, new PropertyDescriptor(value, writable, enumerable, configurable));
        }

        protected void SetIntrinsicValue(JsSymbol symbol, JsValue value, bool writable, bool enumerable, bool configurable)
        {
            if (_intrinsicProperties == null)
            {
                _intrinsicProperties = new Dictionary<string, IPropertyDescriptor>();
            }

            _intrinsicProperties[symbol.AsSymbol()] = new PropertyDescriptor(value, writable, enumerable, configurable);
        }

        /// <summary>
        /// The prototype of this object.
        /// </summary>
        public ObjectInstance Prototype { get; set; }

        /// <summary>
        /// If true, own properties may be added to the
        /// object.
        /// </summary>
        public bool Extensible { get; set; }

        /// <summary>
        /// A String value indicating a specification defined
        /// classification of objects.
        /// </summary>
        public virtual string Class => "Object";

        public virtual IEnumerable<KeyValuePair<string, IPropertyDescriptor>> GetOwnProperties()
        {
            EnsureInitialized();

            if (_prototype != null)
            {
                yield return new KeyValuePair<string, IPropertyDescriptor>(PropertyNamePrototype, _prototype);
            }

            if (_constructor != null)
            {
                yield return new KeyValuePair<string, IPropertyDescriptor>(PropertyNameConstructor, _constructor);
            }

            if (_length != null)
            {
                yield return new KeyValuePair<string, IPropertyDescriptor>(PropertyNameLength, _length);
            }

            if (_properties != null)
            {
                foreach (var pair in _properties.GetEnumerator())
                {
                    yield return pair;
                }
            }
        }

        protected void AddProperty(string propertyName, IPropertyDescriptor descriptor)
        {
            if (propertyName == PropertyNamePrototype)
            {
                _prototype = descriptor;
                return;
            }

            if (propertyName == PropertyNameConstructor)
            {
                _constructor = descriptor;
                return;
            }

            if (propertyName == PropertyNameLength)
            {
                _length = descriptor;
                return;
            }

            if (_properties == null)
            {
                _properties = new MruPropertyCache2<string, IPropertyDescriptor>();
            }

            _properties.Add(propertyName, descriptor);
        }

        protected bool TryGetProperty(string propertyName, out IPropertyDescriptor descriptor)
        {
            if (propertyName == PropertyNamePrototype)
            {
                descriptor = _prototype;
                return _prototype != null;
            }

            if (propertyName == PropertyNameConstructor)
            {
                descriptor = _constructor;
                return _constructor != null;
            }

            if (propertyName == PropertyNameLength)
            {
                descriptor = _length;
                return _length != null;
            }

            if (_properties == null)
            {
                descriptor = null;
                return false;
            }

            return _properties.TryGetValue(propertyName, out descriptor);
        }

        public virtual bool HasOwnProperty(string propertyName)
        {
            EnsureInitialized();

            if (propertyName == PropertyNamePrototype)
            {
                return _prototype != null;
            }

            if (propertyName == PropertyNameConstructor)
            {
                return _constructor != null;
            }

            if (propertyName == PropertyNameLength)
            {
                return _length != null;
            }

            return _properties?.ContainsKey(propertyName) ?? false;
        }

        public virtual void RemoveOwnProperty(string propertyName)
        {
            EnsureInitialized();

            if (propertyName == PropertyNamePrototype)
            {
                _prototype = null;
            }

            if (propertyName == PropertyNameConstructor)
            {
                _constructor = null;
            }

            if (propertyName == PropertyNameLength)
            {
                _length = null;
            }

            _properties?.Remove(propertyName);
        }

        /// <summary>
        /// Returns the value of the named property.
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-8.12.3
        /// </summary>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        public virtual JsValue Get(string propertyName)
        {
            var desc = GetProperty(propertyName);

            if (desc == PropertyDescriptor.Undefined)
            {
                return JsValue.Undefined;
            }

            if (desc.IsDataDescriptor())
            {
                var val = desc.Value;
                return val != null ? val : Undefined.Instance;
            }

            var getter = desc.Get != null ? desc.Get : Undefined.Instance;

            if (getter.IsUndefined())
            {
                return Undefined.Instance;
            }

            // if getter is not undefined it must be ICallable
            var callable = getter.TryCast<ICallable>();
            return callable.Call(this, Arguments.Empty);
        }

        /// <summary>
        /// Returns the Property Descriptor of the named
        /// own property of this object, or undefined if
        /// absent.
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-8.12.1
        /// </summary>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        public virtual IPropertyDescriptor GetOwnProperty(string propertyName)
        {
            EnsureInitialized();

            if (propertyName == PropertyNamePrototype)
            {
                return _prototype ?? PropertyDescriptor.Undefined;
            }

            if (propertyName == PropertyNameConstructor)
            {
                return _constructor ?? PropertyDescriptor.Undefined;
            }

            if (propertyName == PropertyNameLength)
            {
                return _length ?? PropertyDescriptor.Undefined;
            }

            if (_properties != null && _properties.TryGetValue(propertyName, out var x))
            {
                return x;
            }

            return PropertyDescriptor.Undefined;
        }

        protected internal virtual void SetOwnProperty(string propertyName, IPropertyDescriptor desc)
        {
            EnsureInitialized();

            if (propertyName == PropertyNamePrototype)
            {
                _prototype = desc;
                return;
            }

            if (propertyName == PropertyNameConstructor)
            {
                _constructor = desc;
                return;
            }

            if (propertyName == PropertyNameLength)
            {
                _length = desc;
                return;
            }

            if (_properties == null)
            {
                _properties = new MruPropertyCache2<string, IPropertyDescriptor>();
            }

            _properties[propertyName] = desc;
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-8.12.2
        /// </summary>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        public IPropertyDescriptor GetProperty(string propertyName)
        {
            var prop = GetOwnProperty(propertyName);

            if (prop != PropertyDescriptor.Undefined)
            {
                return prop;
            }

            if (Prototype == null)
            {
                return PropertyDescriptor.Undefined;
            }

            return Prototype.GetProperty(propertyName);
        }

        public bool TryGetValue(string propertyName, out JsValue value)
        {
            value = JsValue.Undefined;
            var desc = GetOwnProperty(propertyName);
            if (desc != null && desc != PropertyDescriptor.Undefined)
            {
                if (desc == PropertyDescriptor.Undefined)
                {
                    return false;
                }

                if (desc.IsDataDescriptor() && desc.Value != null)
                {
                    value = desc.Value;
                    return true;
                }

                var getter = desc.Get != null ? desc.Get : Undefined.Instance;

                if (getter.IsUndefined())
                {
                    value = Undefined.Instance;
                    return false;
                }

                // if getter is not undefined it must be ICallable
                var callable = getter.TryCast<ICallable>();
                value = callable.Call(this, Arguments.Empty);
                return true;
            }

            if (Prototype == null)
            {
                return false;
            }

            return Prototype.TryGetValue(propertyName, out value);
        }

        /// <summary>
        /// Sets the specified named property to the value
        /// of the second parameter. The flag controls
        /// failure handling.
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="value"></param>
        /// <param name="throwOnError"></param>
        public virtual void Put(string propertyName, JsValue value, bool throwOnError)
        {
            if (!CanPut(propertyName))
            {
                if (throwOnError)
                {
                    throw new JavaScriptException(Engine.TypeError);
                }

                return;
            }

            var ownDesc = GetOwnProperty(propertyName);

            if (ownDesc.IsDataDescriptor())
            {
                ownDesc.Value = value;
                return;

                // as per specification
                // var valueDesc = new PropertyDescriptor(value: value, writable: null, enumerable: null, configurable: null);
                // DefineOwnProperty(propertyName, valueDesc, throwOnError);
                // return;
            }

            // property is an accessor or inherited
            var desc = GetProperty(propertyName);

            if (desc.IsAccessorDescriptor())
            {
                var setter = desc.Set.TryCast<ICallable>();
                setter.Call(JsValue, new[] {value});
            }
            else
            {
                var newDesc = new ConfigurableEnumerableWritablePropertyDescriptor(value);
                DefineOwnProperty(propertyName, newDesc, throwOnError);
            }
        }

        /// <summary>
        /// Returns a Boolean value indicating whether a
        /// [[Put]] operation with PropertyName can be
        /// performed.
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-8.12.4
        /// </summary>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        public bool CanPut(string propertyName)
        {
            var desc = GetOwnProperty(propertyName);

            if (desc != PropertyDescriptor.Undefined)
            {
                if (desc.IsAccessorDescriptor())
                {
                    if (desc.Set == null || desc.Set.IsUndefined())
                    {
                        return false;
                    }

                    return true;
                }

                return desc.Writable.HasValue && desc.Writable.Value;
            }

            if (Prototype == null)
            {
                return Extensible;
            }

            var inherited = Prototype.GetProperty(propertyName);

            if (inherited == PropertyDescriptor.Undefined)
            {
                return Extensible;
            }

            if (inherited.IsAccessorDescriptor())
            {
                if (inherited.Set == null || inherited.Set.IsUndefined())
                {
                    return false;
                }

                return true;
            }

            if (!Extensible)
            {
                return false;
            }
            else
            {
                return inherited.Writable.HasValue && inherited.Writable.Value;
            }
        }

        /// <summary>
        /// Returns a Boolean value indicating whether the
        /// object already has a property with the given
        /// name.
        /// </summary>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        public bool HasProperty(string propertyName)
        {
            return GetProperty(propertyName) != PropertyDescriptor.Undefined;
        }

        /// <summary>
        /// Removes the specified named own property
        /// from the object. The flag controls failure
        /// handling.
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="throwOnError"></param>
        /// <returns></returns>
        public virtual bool Delete(string propertyName, bool throwOnError)
        {
            var desc = GetOwnProperty(propertyName);

            if (desc == PropertyDescriptor.Undefined)
            {
                return true;
            }

            if (desc.Configurable.HasValue && desc.Configurable.Value)
            {
                RemoveOwnProperty(propertyName);
                return true;
            }
            else
            {
                if (throwOnError)
                {
                    throw new JavaScriptException(Engine.TypeError);
                }

                return false;
            }
        }

        /// <summary>
        /// Hint is a String. Returns a default value for the
        /// object.
        /// </summary>
        /// <param name="hint"></param>
        /// <returns></returns>
        public JsValue DefaultValue(Types hint)
        {
            EnsureInitialized();

            if (hint == Types.String || (hint == Types.None && Class == "Date"))
            {
                var toString = Get("toString").TryCast<ICallable>();
                if (toString != null)
                {
                    var str = toString.Call(JsValue, Arguments.Empty);
                    if (str.IsPrimitive())
                    {
                        return str;
                    }
                }

                var valueOf = Get("valueOf").TryCast<ICallable>();
                if (valueOf != null)
                {
                    var val = valueOf.Call(JsValue, Arguments.Empty);
                    if (val.IsPrimitive())
                    {
                        return val;
                    }
                }

                throw new JavaScriptException(Engine.TypeError);
            }

            if (hint == Types.Number || hint == Types.None)
            {
                var valueOf = Get("valueOf").TryCast<ICallable>();
                if (valueOf != null)
                {
                    var val = valueOf.Call(JsValue, Arguments.Empty);
                    if (val.IsPrimitive())
                    {
                        return val;
                    }
                }

                var toString = Get("toString").TryCast<ICallable>();
                if (toString != null)
                {
                    var str = toString.Call(JsValue, Arguments.Empty);
                    if (str.IsPrimitive())
                    {
                        return str;
                    }
                }

                throw new JavaScriptException(Engine.TypeError);
            }

            return ToString();
        }

        /// <summary>
        /// Creates or alters the named own property to
        /// have the state described by a Property
        /// Descriptor. The flag controls failure handling.
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="desc"></param>
        /// <param name="throwOnError"></param>
        /// <returns></returns>
        public virtual bool DefineOwnProperty(string propertyName, IPropertyDescriptor desc, bool throwOnError)
        {
            var current = GetOwnProperty(propertyName);

            if (current == desc)
            {
                return true;
            }

            if (current == PropertyDescriptor.Undefined)
            {
                if (!Extensible)
                {
                    if (throwOnError)
                    {
                        throw new JavaScriptException(Engine.TypeError);
                    }

                    return false;
                }
                else
                {
                    if (desc.IsGenericDescriptor() || desc.IsDataDescriptor())
                    {
                        IPropertyDescriptor propertyDescriptor;
                        if (desc.Configurable.GetValueOrDefault() && desc.Enumerable.GetValueOrDefault() && desc.Writable.GetValueOrDefault())
                        {
                            propertyDescriptor = new ConfigurableEnumerableWritablePropertyDescriptor(desc.Value != null ? desc.Value : JsValue.Undefined);
                        }
                        else if (!desc.Configurable.GetValueOrDefault() && !desc.Enumerable.GetValueOrDefault() && !desc.Writable.GetValueOrDefault())
                        {
                            propertyDescriptor = new AllForbiddenPropertyDescriptor(desc.Value != null ? desc.Value : JsValue.Undefined);
                        }
                        else
                        {
                            propertyDescriptor = new PropertyDescriptor(desc)
                            {
                                Value = desc.Value != null ? desc.Value : JsValue.Undefined,
                                Writable = desc.Writable.HasValue ? desc.Writable.Value : false,
                                Enumerable = desc.Enumerable.HasValue ? desc.Enumerable.Value : false,
                                Configurable = desc.Configurable.HasValue ? desc.Configurable.Value : false
                            };
                        }
                        SetOwnProperty(propertyName, propertyDescriptor);
                    }
                    else
                    {
                        SetOwnProperty(propertyName, new PropertyDescriptor(desc)
                        {
                            Get = desc.Get,
                            Set = desc.Set,
                            Enumerable = desc.Enumerable.HasValue ? desc.Enumerable : false,
                            Configurable = desc.Configurable.HasValue ? desc.Configurable : false,
                        });
                    }
                }

                return true;
            }

            // Step 5
            if (!current.Configurable.HasValue &&
                !current.Enumerable.HasValue &&
                !current.Writable.HasValue &&
                current.Get == null &&
                current.Set == null &&
                current.Value == null)
            {
                return true;
            }

            // Step 6
            if (
                current.Configurable == desc.Configurable &&
                current.Writable == desc.Writable &&
                current.Enumerable == desc.Enumerable &&
                ((current.Get == null && desc.Get == null) || (current.Get != null && desc.Get != null && ExpressionInterpreter.SameValue(current.Get, desc.Get))) &&
                ((current.Set == null && desc.Set == null) || (current.Set != null && desc.Set != null && ExpressionInterpreter.SameValue(current.Set, desc.Set))) &&
                ((current.Value == null && desc.Value == null) || (current.Value != null && desc.Value != null && ExpressionInterpreter.StrictlyEqual(current.Value, desc.Value)))
            )
            {
                return true;
            }

            if (!current.Configurable.HasValue || !current.Configurable.Value)
            {
                if (desc.Configurable.HasValue && desc.Configurable.Value)
                {
                    if (throwOnError)
                    {
                        throw new JavaScriptException(Engine.TypeError);
                    }

                    return false;
                }

                if (desc.Enumerable.HasValue && (!current.Enumerable.HasValue || desc.Enumerable.Value != current.Enumerable.Value))
                {
                    if (throwOnError)
                    {
                        throw new JavaScriptException(Engine.TypeError);
                    }

                    return false;
                }
            }

            if (!desc.IsGenericDescriptor())
            {
                if (current.IsDataDescriptor() != desc.IsDataDescriptor())
                {
                    if (!current.Configurable.HasValue || !current.Configurable.Value)
                    {
                        if (throwOnError)
                        {
                            throw new JavaScriptException(Engine.TypeError);
                        }

                        return false;
                    }

                    if (current.IsDataDescriptor())
                    {
                        SetOwnProperty(propertyName, current = new PropertyDescriptor(
                            get: Undefined.Instance,
                            set: Undefined.Instance,
                            enumerable: current.Enumerable,
                            configurable: current.Configurable
                        ));
                    }
                    else
                    {
                        SetOwnProperty(propertyName, current = new PropertyDescriptor(
                            value: Undefined.Instance,
                            writable: null,
                            enumerable: current.Enumerable,
                            configurable: current.Configurable
                        ));
                    }
                }
                else if (current.IsDataDescriptor() && desc.IsDataDescriptor())
                {
                    if (!current.Configurable.HasValue || current.Configurable.Value == false)
                    {
                        if (!current.Writable.HasValue || !current.Writable.Value && desc.Writable.HasValue && desc.Writable.Value)
                        {
                            if (throwOnError)
                            {
                                throw new JavaScriptException(Engine.TypeError);
                            }

                            return false;
                        }

                        if (!current.Writable.Value)
                        {
                            if (desc.Value != null && !ExpressionInterpreter.SameValue(desc.Value, current.Value))
                            {
                                if (throwOnError)
                                {
                                    throw new JavaScriptException(Engine.TypeError);
                                }

                                return false;
                            }
                        }
                    }
                }
                else if (current.IsAccessorDescriptor() && desc.IsAccessorDescriptor())
                {
                    if (!current.Configurable.HasValue || !current.Configurable.Value)
                    {
                        if ((desc.Set != null && !ExpressionInterpreter.SameValue(desc.Set, current.Set != null ? current.Set : Undefined.Instance))
                            ||
                            (desc.Get != null && !ExpressionInterpreter.SameValue(desc.Get, current.Get != null ? current.Get : Undefined.Instance)))
                        {
                            if (throwOnError)
                            {
                                throw new JavaScriptException(Engine.TypeError);
                            }

                            return false;
                        }
                    }
                }
            }

            if (desc.Value != null)
            {
                current.Value = desc.Value;
            }

            PropertyDescriptor mutable = null;
            if (desc.Writable.HasValue)
            {
                current = mutable = current as PropertyDescriptor ?? new PropertyDescriptor(current);
                mutable.Writable = desc.Writable;
            }

            if (desc.Enumerable.HasValue)
            {
                current = mutable = current as PropertyDescriptor ?? new PropertyDescriptor(current);
                mutable.Enumerable = desc.Enumerable;
            }

            if (desc.Configurable.HasValue)
            {
                current = mutable = current as PropertyDescriptor ?? new PropertyDescriptor(current);
                mutable.Configurable = desc.Configurable;
            }

            if (desc.Get != null)
            {
                current = mutable = current as PropertyDescriptor ?? new PropertyDescriptor(current);
                mutable.Get = desc.Get;
            }

            if (desc.Set != null)
            {
                mutable = current as PropertyDescriptor ?? new PropertyDescriptor(current);
                mutable.Set = desc.Set;
            }

            if (mutable != null)
            {
                FastSetProperty(propertyName, mutable);
            }

            return true;
        }

        /// <summary>
        /// Optimized version of [[Put]] when the property is known to be undeclared already
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <param name="writable"></param>
        /// <param name="configurable"></param>
        /// <param name="enumerable"></param>
        public void FastAddProperty(string name, JsValue value, bool writable, bool enumerable, bool configurable)
        {
            SetOwnProperty(name, new PropertyDescriptor(value, writable, enumerable, configurable));
        }

        /// <summary>
        /// Optimized version of [[Put]] when the property is known to be already declared
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        public void FastSetProperty(string name, IPropertyDescriptor value)
        {
            SetOwnProperty(name, value);
        }

        protected virtual void EnsureInitialized()
        {
        }

        public override string ToString()
        {
            return TypeConverter.ToString(this);
        }

        protected uint GetLengthValue() => TypeConverter.ToUint32(_length.Value);
    }
}