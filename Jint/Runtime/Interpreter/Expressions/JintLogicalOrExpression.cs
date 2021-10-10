using Esprima.Ast;
using Jint.Native;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintLogicalOrExpression : JintExpression
    {
        private readonly JintExpression _left;
        private readonly JintExpression _right;

        public JintLogicalOrExpression(Engine engine, BinaryExpression expression) : base(expression)
        {
            _left = Build(engine, expression.Left);
            _right = Build(engine, expression.Right);
        }

        protected override object EvaluateInternal(EvaluationContext context)
        {
            var left = _left.GetValue(context);

            if (left is JsBoolean b && b._value)
            {
                return b;
            }

            if (TypeConverter.ToBoolean(left))
            {
                return left;
            }

            return _right.GetValue(context);
        }
    }
}