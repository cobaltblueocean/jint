using System.Collections.Generic;
using Esprima.Ast;
using Jint.Native;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Runtime.Interpreter.Statements
{
    /// <summary>
    /// https://tc39.es/ecma262/#sec-forbodyevaluation
    /// </summary>
    internal sealed class JintForStatement : JintStatement<ForStatement>
    {
        private JintVariableDeclaration _initStatement;
        private JintExpression _initExpression;
        
        private JintExpression _test;
        private JintExpression _increment;
        
        private JintStatement _body;
        private List<string> _boundNames;

        public JintForStatement(Engine engine, ForStatement statement) : base(engine, statement)
        {
            _initialized = false;
        }

        protected override void Initialize()
        {
            _body = Build(_engine, _statement.Body);

            if (_statement.Init != null)
            {
                if (_statement.Init.Type == Nodes.VariableDeclaration)
                {
                    var variableDeclaration = (VariableDeclaration) _statement.Init;
                    if (variableDeclaration.Kind != VariableDeclarationKind.Var)
                    {
                        _boundNames = new List<string>();
                        JintForInStatement.GetBoundNames(variableDeclaration, _boundNames);
                    }
                    _initStatement = new JintVariableDeclaration(_engine, variableDeclaration);
                }
                else
                {
                    _initExpression = JintExpression.Build(_engine, (Expression) _statement.Init);
                }
            }

            if (_statement.Test != null)
            {
                _test = JintExpression.Build(_engine, _statement.Test);
            }

            if (_statement.Update != null)
            {
                _increment = JintExpression.Build(_engine, _statement.Update);
            }
        }

        protected override Completion ExecuteInternal()
        {
            LexicalEnvironment oldEnv = null;
            LexicalEnvironment loopEnv = null;
            if (_boundNames != null)
            {
                oldEnv = _engine.ExecutionContext.LexicalEnvironment;
                loopEnv = LexicalEnvironment.NewDeclarativeEnvironment(_engine, oldEnv);
                var loopEnvRec = loopEnv._record;
                var kind = _initStatement._statement.Kind;
                for (var i = 0; i < _boundNames.Count; i++)
                {
                    var name = _boundNames[i];
                    if (kind == VariableDeclarationKind.Const)
                    {
                        loopEnvRec.CreateImmutableBinding(name, true);
                    }
                    else
                    {
                        loopEnvRec.CreateMutableBinding(name, false);
                    }
                }

                _engine.UpdateLexicalEnvironment(loopEnv);
            }

            try
            {
                if (_initExpression != null)
                {
                    _initExpression?.GetValue();
                }
                else
                {
                    _initStatement?.ExecuteDeclaration(loopEnv);
                }

                return ForBodyEvaluation();
            }
            finally
            {
                if (oldEnv != null)
                {
                    _engine.UpdateLexicalEnvironment(oldEnv);
                }
            }
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-forbodyevaluation
        /// </summary>
        private Completion ForBodyEvaluation()
        {
            var v = Undefined.Instance;

            var shouldCreatePerIterationEnvironment = _initStatement?._statement?.Kind == VariableDeclarationKind.Let;
            if (shouldCreatePerIterationEnvironment)
            {
                CreatePerIterationEnvironment();
            }

            while (true)
            {
                if (_test != null)
                {
                    if (!TypeConverter.ToBoolean(_test.GetValue()))
                    {
                        return new Completion(CompletionType.Normal, v, null, Location);
                    }
                }

                var result = _body.Execute();
                if (!ReferenceEquals(result.Value, null))
                {
                    v = result.Value;
                }

                if (result.Type == CompletionType.Break && (result.Identifier == null || result.Identifier == _statement?.LabelSet?.Name))
                {
                    return new Completion(CompletionType.Normal, result.Value, null, Location);
                }

                if (result.Type != CompletionType.Continue || (result.Identifier != null && result.Identifier != _statement?.LabelSet?.Name))
                {
                    if (result.Type != CompletionType.Normal)
                    {
                        return result;
                    }
                }

                if (shouldCreatePerIterationEnvironment)
                {
                    CreatePerIterationEnvironment();
                }

                _increment?.GetValue();
            }
        }

        private void CreatePerIterationEnvironment()
        {
            if (_boundNames == null || _boundNames.Count == 0)
            {
                return;
            }
            
            var lastIterationEnv = _engine.ExecutionContext.LexicalEnvironment;
            var lastIterationEnvRec = lastIterationEnv._record;
            var outer = lastIterationEnv._outer;
            var thisIterationEnv = LexicalEnvironment.NewDeclarativeEnvironment(_engine, outer);
            var thisIterationEnvRec = thisIterationEnv._record;
            
            for (var j = 0; j < _boundNames.Count; j++)
            {
                var bn = _boundNames[j];
                thisIterationEnvRec.CreateMutableBinding(bn, false);
                var lastValue = lastIterationEnvRec.GetBindingValue(bn, true);
                thisIterationEnvRec.InitializeBinding(bn, lastValue);
            }

            _engine.UpdateLexicalEnvironment(thisIterationEnv);
        }
    }
}