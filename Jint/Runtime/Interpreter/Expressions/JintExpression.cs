using Esprima;
using Esprima.Ast;
using Jint.Native;
using Jint.Native.Number;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal abstract class JintExpression
    {
        public abstract Location Location { get; }

        public abstract object Evaluate();

        protected abstract object EvaluateInternal();

        protected internal static JintExpression Build(Engine engine, Expression expression)
        {
            switch (expression.Type)
            {
                case Nodes.AssignmentExpression:
                    return new JintAssignmentExpression(engine, (AssignmentExpression) expression);

                case Nodes.ArrayExpression:
                    return new JintArrayExpression(engine, (ArrayExpression) expression);

                case Nodes.BinaryExpression:
                    return JintBinaryExpression.Build(engine, (BinaryExpression) expression);

                case Nodes.CallExpression:
                    return new JintCallExpression(engine, (CallExpression) expression);

                case Nodes.ConditionalExpression:
                    return new JintConditionalExpression(engine, (ConditionalExpression) expression);

                case Nodes.FunctionExpression:
                    return new JintFunctionExpression(engine, (IFunction) expression);

                case Nodes.Identifier:
                    return new JintIdentifierExpression(engine, (Identifier) expression);

                case Nodes.Literal:
                    return new JintLiteralExpression(engine, (Literal) expression);

                case Nodes.LogicalExpression:
                    var binaryExpression = (BinaryExpression) expression;
                    switch (binaryExpression.Operator)
                    {
                        case BinaryOperator.LogicalAnd:
                            return new JintLogicalAndExpression(engine, binaryExpression);
                        case BinaryOperator.LogicalOr:
                            return new JintLogicalOrExpression(engine, binaryExpression);
                        default:
                            return ExceptionHelper.ThrowArgumentOutOfRangeException<JintExpression>();
                    }

                case Nodes.MemberExpression:
                    return new JintMemberExpression(engine, (MemberExpression) expression);

                case Nodes.NewExpression:
                    return new JintNewExpression(engine, (NewExpression) expression);

                case Nodes.ObjectExpression:
                    return new JintObjectExpression(engine, (ObjectExpression) expression);

                case Nodes.SequenceExpression:
                    return new JintSequenceExpression(engine, (SequenceExpression) expression);

                case Nodes.ThisExpression:
                    return new JintThisExpression(engine, (ThisExpression) expression);

                case Nodes.UpdateExpression:
                    return new JintUpdateExpression(engine, (UpdateExpression) expression);

                case Nodes.UnaryExpression:
                    return new JintUnaryExpression(engine, (UnaryExpression) expression);

                default:
                    ExceptionHelper.ThrowArgumentOutOfRangeException();
                    return null;
            }
        }

        protected JsValue Divide(JsValue lval, JsValue rval)
        {
            if (lval.IsUndefined() || rval.IsUndefined())
            {
                return Undefined.Instance;
            }
            else
            {
                var lN = TypeConverter.ToNumber(lval);
                var rN = TypeConverter.ToNumber(rval);

                if (double.IsNaN(rN) || double.IsNaN(lN))
                {
                    return double.NaN;
                }

                if (double.IsInfinity(lN) && double.IsInfinity(rN))
                {
                    return double.NaN;
                }

                if (double.IsInfinity(lN) && rN == 0)
                {
                    if (NumberInstance.IsNegativeZero(rN))
                    {
                        return -lN;
                    }

                    return lN;
                }

                if (lN == 0 && rN == 0)
                {
                    return double.NaN;
                }

                if (rN == 0)
                {
                    if (NumberInstance.IsNegativeZero(rN))
                    {
                        return lN > 0 ? -double.PositiveInfinity : -double.NegativeInfinity;
                    }

                    return lN > 0 ? double.PositiveInfinity : double.NegativeInfinity;
                }

                return lN / rN;
            }
        }


        protected static bool Equal(JsValue x, JsValue y)
        {
            if (x._type == y._type)
            {
                return JintBinaryExpression.StrictlyEqual(x, y);
            }

            if (x._type == Types.Null && y._type == Types.Undefined)
            {
                return true;
            }

            if (x._type == Types.Undefined && y._type == Types.Null)
            {
                return true;
            }

            if (x._type == Types.Number && y._type == Types.String)
            {
                return Equal(x, TypeConverter.ToNumber(y));
            }

            if (x._type == Types.String && y._type == Types.Number)
            {
                return Equal(TypeConverter.ToNumber(x), y);
            }

            if (x._type == Types.Boolean)
            {
                return Equal(TypeConverter.ToNumber(x), y);
            }

            if (y._type == Types.Boolean)
            {
                return Equal(x, TypeConverter.ToNumber(y));
            }

            if (y._type == Types.Object && (x._type == Types.String || x._type == Types.Number))
            {
                return Equal(x, TypeConverter.ToPrimitive(y));
            }

            if (x._type == Types.Object && (y._type == Types.String || y._type == Types.Number))
            {
                return Equal(TypeConverter.ToPrimitive(x), y);
            }

            return false;
        }

        public static bool SameValue(JsValue x, JsValue y)
        {
            var typea = TypeConverter.GetPrimitiveType(x);
            var typeb = TypeConverter.GetPrimitiveType(y);

            if (typea != typeb)
            {
                return false;
            }

            switch (typea)
            {
                case Types.None:
                    return true;
                case Types.Number:
                    var nx = TypeConverter.ToNumber(x);
                    var ny = TypeConverter.ToNumber(y);

                    if (double.IsNaN(nx) && double.IsNaN(ny))
                    {
                        return true;
                    }

                    if (nx == ny)
                    {
                        if (nx == 0)
                        {
                            // +0 !== -0
                            return NumberInstance.IsNegativeZero(nx) == NumberInstance.IsNegativeZero(ny);
                        }

                        return true;
                    }

                    return false;
                case Types.String:
                    return TypeConverter.ToString(x) == TypeConverter.ToString(y);
                case Types.Boolean:
                    return TypeConverter.ToBoolean(x) == TypeConverter.ToBoolean(y);
                default:
                    return x == y;
            }
        }

        public static JsValue Compare(JsValue x, JsValue y, bool leftFirst = true)
        {
            JsValue px, py;
            if (leftFirst)
            {
                px = TypeConverter.ToPrimitive(x, Types.Number);
                py = TypeConverter.ToPrimitive(y, Types.Number);
            }
            else
            {
                py = TypeConverter.ToPrimitive(y, Types.Number);
                px = TypeConverter.ToPrimitive(x, Types.Number);
            }

            var typea = px.Type;
            var typeb = py.Type;

            if (typea != Types.String || typeb != Types.String)
            {
                var nx = TypeConverter.ToNumber(px);
                var ny = TypeConverter.ToNumber(py);

                if (double.IsNaN(nx) || double.IsNaN(ny))
                {
                    return Undefined.Instance;
                }

                if (nx == ny)
                {
                    return false;
                }

                if (double.IsPositiveInfinity(nx))
                {
                    return false;
                }

                if (double.IsPositiveInfinity(ny))
                {
                    return true;
                }

                if (double.IsNegativeInfinity(ny))
                {
                    return false;
                }

                if (double.IsNegativeInfinity(nx))
                {
                    return true;
                }

                return nx < ny;
            }
            else
            {
                return string.CompareOrdinal(TypeConverter.ToString(x), TypeConverter.ToString(y)) < 0;
            }
        }    
    }

    internal abstract class JintExpression<T> : JintExpression where T : class, Expression, INode
    {
        protected readonly Engine _engine;
        protected readonly T _expression;

        protected JintExpression(Engine engine, T expression)
        {
            _engine = engine;
            _expression = expression;
        }

        public override Location Location => _expression.Location;

        public sealed override object Evaluate()
        {
            _engine._lastSyntaxNode = _expression;
            return EvaluateInternal();
        }

        /// <summary>
        /// Helper that can be used when preparing expressions, null return value
        /// mean that engine resolution is required.
        /// </summary>
        protected static JsValue FastResolve(JintExpression expression)
        {
            if (expression is JintLiteralExpression literalExpression)
            {
                return literalExpression._cachedValue;
            }

            return null;
        }

        protected void BuildArguments(JintExpression[] jintExpressions, JsValue[] targetArray)
        {
            for (var i = 0; i < jintExpressions.Length; i++)
            {
                targetArray[i] = _engine.GetValue(jintExpressions[i].Evaluate(), true);
            }
        }
    }
}