using Esprima.Ast;
using Jint.Native;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Runtime.Interpreter.Statements
{
    /// <summary>
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-12.6.1
    /// </summary>
    internal sealed class JintDoWhileStatement : JintStatement<DoWhileStatement>
    {
        private readonly JintStatement _body;
        private readonly string _labelSetName;
        private readonly JintExpression _test;

        public JintDoWhileStatement(Engine engine, DoWhileStatement statement) : base(engine, statement)
        {
            _body = Build(_engine, statement.Body);
            _test = JintExpression.Build(engine, statement.Test);
            _labelSetName = statement.LabelSet?.Name;
        }

        public override Completion Execute()
        {
            JsValue v = Undefined.Instance;
            bool iterating;

            do
            {
                var completion = _body.Execute();
                if (!ReferenceEquals(completion.Value, null))
                {
                    v = completion.Value;
                }

                if (completion.Type != CompletionType.Continue || completion.Identifier != _labelSetName)
                {
                    if (completion.Type == CompletionType.Break && (completion.Identifier == null || completion.Identifier == _labelSetName))
                    {
                        return new Completion(CompletionType.Normal, v, null);
                    }

                    if (completion.Type != CompletionType.Normal)
                    {
                        return completion;
                    }
                }

                var exprRef = _test.Evaluate();
                iterating = TypeConverter.ToBoolean(_engine.GetValue(exprRef, true));
            } while (iterating);

            return new Completion(CompletionType.Normal, v, null);
        }
    }
}