using System.Collections.Generic;
using Esprima.Ast;
using Jint.Native;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Runtime.Interpreter.Statements
{
    internal sealed class JintSwitchBlock
    {
        private readonly Engine _engine;
        private readonly List<SwitchCase> _switchBlock;
        private JintSwitchCase[] _jintSwitchBlock;
        private bool _initialized;

        public JintSwitchBlock(Engine engine, List<SwitchCase> switchBlock)
        {
            _engine = engine;
            _switchBlock = switchBlock;
        }

        private void Initialize()
        {
            _jintSwitchBlock = new JintSwitchCase[_switchBlock.Count];
            for (var i = 0; i < _jintSwitchBlock.Length; i++)
            {
                _jintSwitchBlock[i] = new JintSwitchCase(_engine, _switchBlock[i]);
            }
        }

        public Completion Execute(JsValue input)
        {
            if (!_initialized)
            {
                Initialize();
                _initialized = true;
            }

            JsValue v = Undefined.Instance;
            JintSwitchCase defaultCase = null;
            bool hit = false;

            for (var i = 0; i < (uint) _jintSwitchBlock.Length; i++)
            {
                var clause = _jintSwitchBlock[i];
                if (clause.Test == null)
                {
                    defaultCase = clause;
                }
                else
                {
                    var clauseSelector = clause.TestValue ?? _engine.GetValue(clause.Test.Evaluate(), true);
                    if (JintBinaryExpression.StrictlyEqual(clauseSelector, input))
                    {
                        hit = true;
                    }
                }

                if (hit && clause.Consequent != null)
                {
                    var r = clause.ConsequentValue ?? clause.Consequent.Execute();
                    if (r.Type != CompletionType.Normal)
                    {
                        return r;
                    }

                    v = r.Value ?? Undefined.Instance;
                }
            }

            // do we need to execute the default case ?
            if (hit == false && defaultCase != null)
            {
                var r = defaultCase.Consequent.Execute();
                if (r.Type != CompletionType.Normal)
                {
                    return r;
                }

                v = r.Value ?? Undefined.Instance;
            }

            return new Completion(CompletionType.Normal, v, null);
        }

        private sealed class JintSwitchCase
        {
            internal readonly JintStatementList Consequent;
            internal readonly JintExpression Test;
            public readonly JsValue TestValue;
            public readonly Completion? ConsequentValue;

            public JintSwitchCase(Engine engine, SwitchCase switchCase)
            {
                if (switchCase.Consequent != null)
                {
                    Consequent = new JintStatementList(engine, null, switchCase.Consequent);
                    ConsequentValue = Consequent.FastResolve();
                }

                if (switchCase.Test != null)
                {
                    Test = JintExpression.Build(engine, switchCase.Test);
                    TestValue = JintExpression.FastResolve(Test);
                }
            }
        }
    }
}