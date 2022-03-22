﻿using Esprima;
using Jint.Native;
using Jint.Runtime.Debugger;
using Xunit;

namespace Jint.Tests.Runtime.Debugger
{
    public class EvaluateTests
    {
        [Fact]
        public void EvalutesInCurrentContext()
        {
            var script = @"
            function test(x)
            {
                x *= 10;
                debugger;
            }

            test(5);
            ";

            TestHelpers.TestAtBreak(script, (engine, info) =>
            {
                var evaluated = engine.DebugHandler.Evaluate("x");
                Assert.IsType<JsNumber>(evaluated);
                Assert.Equal(50, evaluated.AsNumber());
            });
        }

        [Fact]
        public void ThrowsOnRuntimeError()
        {
            var script = @"
            function test(x)
            {
                x *= 10;
                debugger;
            }

            test(5);
            ";

            TestHelpers.TestAtBreak(script, (engine, info) =>
            {
                var exception = Assert.Throws<DebugEvaluationException>(() => engine.DebugHandler.Evaluate("y"));
                // We should check InnerException, but currently there is none.
                //Assert.IsType<JavaScriptException>(exception.InnerException);
            });
        }

        [Fact]
        public void ThrowsOnExecutionError()
        {
            var script = @"
            function test(x)
            {
                x *= 10;
                debugger;
            }

            test(5);
            ";

            TestHelpers.TestAtBreak(script, (engine, info) =>
            {
                var exception = Assert.Throws<DebugEvaluationException>(() =>
                    engine.DebugHandler.Evaluate("this is a syntax error"));
                Assert.IsType<ParserException>(exception.InnerException);
            });
        }

        [Fact]
        public void RestoresStackAfterEvaluation()
        {
            var script = @"
            function throws()
            {
                throw new Error('Take this!');
            }

            function test(x)
            {
                x *= 10;
                debugger;
            }

            test(5);
            ";

            TestHelpers.TestAtBreak(script, (engine, info) =>
            {
                Assert.Equal(1, engine.CallStack.Count);
                var frameBefore = engine.CallStack.Stack[0];

                Assert.Throws<DebugEvaluationException>(() => engine.DebugHandler.Evaluate("throws()"));
                Assert.Equal(1, engine.CallStack.Count);
                var frameAfter = engine.CallStack.Stack[0];
                // Stack frames and some of their properties are structs - can't check reference equality
                // Besides, even if we could, it would be no guarantee. Neither is the following, but it'll do for now.
                Assert.Equal(frameBefore.CallingExecutionContext.Function,
                    frameAfter.CallingExecutionContext.Function);
                Assert.Equal(frameBefore.CallingExecutionContext.LexicalEnvironment,
                    frameAfter.CallingExecutionContext.LexicalEnvironment);
                Assert.Equal(frameBefore.CallingExecutionContext.PrivateEnvironment,
                    frameAfter.CallingExecutionContext.PrivateEnvironment);
                Assert.Equal(frameBefore.CallingExecutionContext.VariableEnvironment,
                    frameAfter.CallingExecutionContext.VariableEnvironment);
                Assert.Equal(frameBefore.CallingExecutionContext.Realm, frameAfter.CallingExecutionContext.Realm);

                Assert.Equal(frameBefore.Arguments, frameAfter.Arguments);
                Assert.Equal(frameBefore.Expression, frameAfter.Expression);
                Assert.Equal(frameBefore.Location, frameAfter.Location);
                Assert.Equal(frameBefore.Function, frameAfter.Function);
            });
        }
    }
}
