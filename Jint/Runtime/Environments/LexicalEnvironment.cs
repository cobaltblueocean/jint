﻿using Jint.Native;
using Jint.Native.Global;
using Jint.Native.Object;

namespace Jint.Runtime.Environments
{
    /// <summary>
    /// Represents a Lexical Environment (a.k.a Scope)
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-10.2
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-10.2.2
    /// </summary>
    public sealed class LexicalEnvironment
    {
        private readonly Engine _engine;
        internal EnvironmentRecord _record;
        internal LexicalEnvironment _outer;

        public LexicalEnvironment(Engine engine, EnvironmentRecord record, LexicalEnvironment outer)
        {
            _engine = engine;
            _record = record;
            _outer = outer;
        }

        internal static bool TryGetIdentifierEnvironmentWithBindingValue(
            LexicalEnvironment lex,
            in Key name,
            bool strict,
            out EnvironmentRecord record,
            out JsValue value)
        {
            record = default;
            value = default;

            while (lex != null)
            {
                if (lex._record.TryGetBinding(
                    name,
                    strict,
                    out _,
                    out value))
                {
                    record = lex._record;
                    return true;
                }

                lex = lex._outer;
            }

            return false;
        }

        public static LexicalEnvironment NewDeclarativeEnvironment(Engine engine, LexicalEnvironment outer = null)
        {
            var environment = new LexicalEnvironment(engine, null, null);
            environment._record = new DeclarativeEnvironmentRecord(engine);
            environment._outer = outer;

            return environment;
        }

        internal static LexicalEnvironment NewGlobalEnvironment(Engine engine, GlobalObject objectInstance)
        {
            return new LexicalEnvironment(engine, new GlobalEnvironmentRecord(engine, objectInstance), null);
        }

        internal static LexicalEnvironment NewObjectEnvironment(Engine engine, ObjectInstance objectInstance, LexicalEnvironment outer, bool provideThis)
        {
            return new LexicalEnvironment(engine, new ObjectEnvironmentRecord(engine, objectInstance, provideThis), outer);
        } 
    }
}
