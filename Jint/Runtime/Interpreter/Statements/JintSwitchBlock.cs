using Esprima.Ast;
using Jint.Native;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Runtime.Interpreter.Statements
{
    internal sealed class JintSwitchBlock
    {
        private readonly NodeList<SwitchCase> _switchBlock;
        private JintSwitchCase[] _jintSwitchBlock = Array.Empty<JintSwitchCase>();
        private bool _initialized;

        public JintSwitchBlock(NodeList<SwitchCase> switchBlock)
        {
            _switchBlock = switchBlock;
        }

        private void Initialize(EvaluationContext context)
        {
            var engine = context.Engine;
            _jintSwitchBlock = new JintSwitchCase[_switchBlock.Count];
            for (var i = 0; i < _jintSwitchBlock.Length; i++)
            {
                _jintSwitchBlock[i] = new JintSwitchCase(engine, _switchBlock[i]);
            }
        }

        public Completion Execute(EvaluationContext context, JsValue input)
        {
            if (!_initialized)
            {
                Initialize(context);
                _initialized = true;
            }

            var engine = context.Engine;
            JsValue v = JsValue.Undefined;
            SyntaxElement l = context.LastSyntaxElement;
            JintSwitchCase? defaultCase = null;
            bool hit = false;

            for (var i = 0; i < (uint) _jintSwitchBlock.Length; i++)
            {
                var clause = _jintSwitchBlock[i];

                EnvironmentRecord? oldEnv = null;
                if (clause.LexicalDeclarations != null)
                {
                    oldEnv = engine.ExecutionContext.LexicalEnvironment;
                    var blockEnv = JintEnvironment.NewDeclarativeEnvironment(engine, oldEnv);
                    JintStatementList.BlockDeclarationInstantiation(engine, blockEnv, clause.LexicalDeclarations);
                    engine.UpdateLexicalEnvironment(blockEnv);
                }

                if (clause.Test == null)
                {
                    defaultCase = clause;
                }
                else
                {
                    var clauseSelector = clause.Test.GetValue(context);
                    if (clauseSelector == input)
                    {
                        hit = true;
                    }
                }

                if (hit && clause.Consequent != null)
                {
                    var r = clause.Consequent.Execute(context);

                    if (oldEnv is not null)
                    {
                        engine.UpdateLexicalEnvironment(oldEnv);
                    }

                    if (r.Type != CompletionType.Normal)
                    {
                        return r;
                    }

                    l = r._source;
                    v = r.Value ?? JsValue.Undefined;
                }
            }

            // do we need to execute the default case ?
            if (hit == false && defaultCase != null)
            {
                EnvironmentRecord? oldEnv = null;
                if (defaultCase.LexicalDeclarations != null)
                {
                    oldEnv = engine.ExecutionContext.LexicalEnvironment;
                    var blockEnv = JintEnvironment.NewDeclarativeEnvironment(engine, oldEnv);
                    JintStatementList.BlockDeclarationInstantiation(engine, blockEnv, defaultCase.LexicalDeclarations);
                    engine.UpdateLexicalEnvironment(blockEnv);
                }

                var r = defaultCase.Consequent.Execute(context);

                if (oldEnv is not null)
                {
                    engine.UpdateLexicalEnvironment(oldEnv);
                }
                if (r.Type != CompletionType.Normal)
                {
                    return r;
                }

                l = r._source;
                v = r.Value ?? JsValue.Undefined;
            }

            return new Completion(CompletionType.Normal, v, l);
        }

        private sealed class JintSwitchCase
        {
            internal readonly JintStatementList Consequent;
            internal readonly JintExpression? Test;
            internal readonly List<Declaration>? LexicalDeclarations;

            public JintSwitchCase(Engine engine, SwitchCase switchCase)
            {
                Consequent = new JintStatementList(null, switchCase.Consequent);
                LexicalDeclarations = HoistingScope.GetLexicalDeclarations(switchCase);

                if (switchCase.Test != null)
                {
                    Test = JintExpression.Build(switchCase.Test);
                }
            }
        }
    }
}
