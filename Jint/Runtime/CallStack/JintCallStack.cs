using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text;
using Esprima;
using Esprima.Ast;
using Jint.Collections;
using Jint.Native.Function;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Runtime.CallStack
{
    // smaller version with only required info
    internal readonly record struct CallStackExecutionContext
    {
        public CallStackExecutionContext(in ExecutionContext context)
        {
            LexicalEnvironment = context.LexicalEnvironment;
        }

        internal readonly EnvironmentRecord LexicalEnvironment;

        internal EnvironmentRecord GetThisEnvironment()
        {
            var lex = LexicalEnvironment;
            while (true)
            {
                if (lex is not null)
                {
                    if (lex.HasThisBinding())
                    {
                        return lex;

                    }

                    lex = lex._outerEnv;
                }
            }
        }
    }

    internal sealed class JintCallStack
    {
        private readonly RefStack<CallStackElement> _stack = new();
        private readonly Dictionary<CallStackElement, int>? _statistics;

        // Internal for use by DebugHandler
        internal RefStack<CallStackElement> Stack => _stack;

        public JintCallStack(bool trackRecursionDepth)
        {
            if (trackRecursionDepth)
            {
                _statistics = new Dictionary<CallStackElement, int>(CallStackElementComparer.Instance);
            }
        }

        public int Push(FunctionInstance functionInstance, JintExpression? expression, in ExecutionContext executionContext)
        {
            var item = new CallStackElement(functionInstance, expression, new CallStackExecutionContext(executionContext));
            _stack.Push(item);
            if (_statistics is not null)
            {
#pragma warning disable CA1854
#pragma warning disable CA1864
                if (_statistics.ContainsKey(item))
#pragma warning restore CA1854
#pragma warning restore CA1864
                {
                    return ++_statistics[item];
                }
                else
                {
                    _statistics.Add(item, 0);
                    return 0;
                }
            }

            return -1;
        }

        public CallStackElement Pop()
        {
            ref readonly var item = ref _stack.Pop();
            if (_statistics is not null)
            {
                if (_statistics[item] == 0)
                {
                    _statistics.Remove(item);
                }
                else
                {
                    _statistics[item]--;
                }
            }

            return item;
        }

        public bool TryPeek([NotNullWhen(true)] out CallStackElement item)
        {
            return _stack.TryPeek(out item);
        }

        public int Count => _stack._size;

        public void Clear()
        {
            _stack.Clear();
            _statistics?.Clear();
        }

        public override string ToString()
        {
            return string.Join("->", _stack.Select(static cse => cse.ToString()).Reverse());
        }

        internal string BuildCallStackString(Location location, int excludeTop = 0)
        {
            static void AppendLocation(
                ref ValueStringBuilder sb,
                string shortDescription,
                in Location loc,
                in CallStackElement? element)
            {
                sb.Append("   at");

                if (!string.IsNullOrWhiteSpace(shortDescription))
                {
                    sb.Append(' ');
                    sb.Append(shortDescription);
                }

                if (element?.Arguments is not null)
                {
                    // it's a function
                    sb.Append(" (");
                    for (var index = 0; index < element.Value.Arguments.Value.Count; index++)
                    {
                        if (index != 0)
                        {
                            sb.Append(", ");
                        }

                        var arg = element.Value.Arguments.Value[index];
                        sb.Append(GetPropertyKey(arg));
                    }
                    sb.Append(')');
                }

                sb.Append(' ');
                sb.Append(loc.Source);
                sb.Append(':');
                sb.Append(loc.End.Line.ToString(CultureInfo.InvariantCulture));
                sb.Append(':');
                sb.Append((loc.Start.Column + 1).ToString(CultureInfo.InvariantCulture)); // report column number instead of index
                sb.Append(Environment.NewLine);
            }

            var builder = new ValueStringBuilder();

            // stack is one frame behind function-wise when we start to process it from expression level
            var index = _stack._size - 1 - excludeTop;
            var element = index >= 0 ? _stack[index] : (CallStackElement?) null;
            var shortDescription = element?.ToString() ?? "";

            AppendLocation(ref builder, shortDescription, location, element);

            location = element?.Location ?? default;
            index--;

            while (index >= -1)
            {
                element = index >= 0 ? _stack[index] : null;
                shortDescription = element?.ToString() ?? "";

                AppendLocation(ref builder, shortDescription, location, element);

                location = element?.Location ?? default;
                index--;
            }

            var result = builder.AsSpan().TrimEnd().ToString();

            builder.Dispose();

            return result;
        }

        /// <summary>
        /// A version of <see cref="EsprimaExtensions.GetKey"/> that cannot get into loop as we are already building a stack.
        /// </summary>
        private static string GetPropertyKey(Node expression)
        {
            if (expression is Literal literal)
            {
                return EsprimaExtensions.LiteralKeyToString(literal);
            }

            if (expression is Identifier identifier)
            {
                return identifier.Name ?? "";
            }

            if (expression is StaticMemberExpression staticMemberExpression)
            {
                return GetPropertyKey(staticMemberExpression.Object) + "." +
                       GetPropertyKey(staticMemberExpression.Property);
            }

            return "?";
        }

    }
}
