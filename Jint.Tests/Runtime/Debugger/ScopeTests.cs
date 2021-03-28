﻿using Jint.Native;
using Jint.Runtime.Debugger;
using System.Linq;
using Xunit;

namespace Jint.Tests.Runtime.Debugger
{
    public class ScopeTests
    {
        private static JsValue AssertOnlyScopeContains(DebugScopes scopes, string name, DebugScopeType scopeType)
        {
            var containingScope = Assert.Single(scopes, s => s.ScopeType == scopeType && s.BindingNames.Contains(name));
            Assert.DoesNotContain(scopes, s => s != containingScope && s.BindingNames.Contains(name));

            return containingScope.GetBindingValue(name);
        }

        private static void AssertScope(DebugScope actual, DebugScopeType expectedType, params string[] expectedBindingNames)
        {
            Assert.Equal(expectedType, actual.ScopeType);
            // Global scope will have a number of intrinsic bindings that are outside the scope [no pun] of these tests
            if (actual.ScopeType != DebugScopeType.Global)
            {
                Assert.Equal(expectedBindingNames.Length, actual.BindingNames.Count);
            }
            foreach (string expectedName in expectedBindingNames)
            {
                Assert.Contains(expectedName, actual.BindingNames);
            }
        }

        [Fact]
        public void GlobalScopeIncludesGlobalConst()
        {
            string script = @"
                const globalConstant = 'test';
                debugger;
            ";

            TestHelpers.TestAtBreak(script, info =>
            {
                var value = AssertOnlyScopeContains(info.CurrentScopeChain, "globalConstant", DebugScopeType.Global);
                Assert.Equal("test", value.AsString());
            });
        }

        [Fact]
        public void GlobalScopeIncludesGlobalLet()
        {
            string script = @"
                let globalLet = 'test';
                debugger;";

            TestHelpers.TestAtBreak(script, info =>
            {
                var value = AssertOnlyScopeContains(info.CurrentScopeChain, "globalLet", DebugScopeType.Global);
                Assert.Equal("test", value.AsString());
            });
        }

        [Fact]
        public void GlobalScopeIncludesGlobalVar()
        {
            string script = @"
                var globalVar = 'test';
                debugger;";

            TestHelpers.TestAtBreak(script, info =>
            {
                var value = AssertOnlyScopeContains(info.CurrentScopeChain, "globalVar", DebugScopeType.Global);
                Assert.Equal("test", value.AsString());
            });
        }

        [Fact]
        public void TopLevelBlockScopeIsIdentified()
        {
            string script = @"
                function test()
                {
                    const localConst = 'test';
                    debugger;
                }
                test();";

            TestHelpers.TestAtBreak(script, info =>
            {
                Assert.Equal(3, info.CurrentScopeChain.Count);
                Assert.Equal(DebugScopeType.Block, info.CurrentScopeChain[0].ScopeType);
                Assert.True(info.CurrentScopeChain[0].IsTopLevel);
            });
        }

        [Fact]
        public void NonTopLevelBlockScopeIsIdentified()
        {
            string script = @"
                function test()
                {
                    {
                        const localConst = 'test';
                        debugger;
                    }
                }
                test();";

            TestHelpers.TestAtBreak(script, info =>
            {
                // We only have 3 scopes, because the function top level block scope is empty.
                Assert.Equal(3, info.CurrentScopeChain.Count);
                Assert.Equal(DebugScopeType.Block, info.CurrentScopeChain[0].ScopeType);
                Assert.False(info.CurrentScopeChain[0].IsTopLevel);
            });
        }

        [Fact]
        public void BlockScopeIncludesLocalConst()
        {
            string script = @"
                function test()
                {
                    {
                        const localConst = 'test';
                        debugger;
                    }
                }
                test();";

            TestHelpers.TestAtBreak(script, info =>
            {
                var value = AssertOnlyScopeContains(info.CurrentScopeChain, "localConst", DebugScopeType.Block);
                Assert.Equal("test", value.AsString());
            });
        }
        [Fact]
        public void BlockScopeIncludesLocalLet()
        {
            string script = @"
                function test()
                {
                    {
                        let localLet = 'test';
                        debugger;
                    }
                }
                test();";

            TestHelpers.TestAtBreak(script, info =>
            {
                var value = AssertOnlyScopeContains(info.CurrentScopeChain, "localLet", DebugScopeType.Block);
                Assert.Equal("test", value.AsString());
            });
        }

        [Fact]
        public void LocalScopeIncludesLocalVar()
        {
            string script = @"
                function test()
                {
                    var localVar = 'test';
                    debugger;
                }
                test();";

            TestHelpers.TestAtBreak(script, info =>
            {
                AssertOnlyScopeContains(info.CurrentScopeChain, "localVar", DebugScopeType.Local);
            });
        }

        [Fact]
        public void LocalScopeIncludesBlockVar()
        {
            string script = @"
                function test()
                {
                    debugger;
                    {
                        var localVar = 'test';
                    }
                }
                test();";

            TestHelpers.TestAtBreak(script, info =>
            {
                AssertOnlyScopeContains(info.CurrentScopeChain, "localVar", DebugScopeType.Local);
            });
        }

        [Fact]
        public void BlockScopedConstIsVisibleInsideBlock()
        {
            string script = @"
            'dummy statement';
            {
                debugger; // const is initialized (as undefined) at beginning of block
                const blockConst = 'block';
            }";

            TestHelpers.TestAtBreak(script, info =>
            {
                var value = AssertOnlyScopeContains(info.CurrentScopeChain, "blockConst", DebugScopeType.Block);
                Assert.Equal(JsUndefined.Undefined, value);
            });
        }

        [Fact]
        public void BlockScopedLetIsVisibleInsideBlock()
        {
            string script = @"
            'dummy statement';
            {
                let blockLet = 'block';
                debugger; // let isn't initialized until declaration
            }";

            TestHelpers.TestAtBreak(script, info =>
            {
                AssertOnlyScopeContains(info.CurrentScopeChain, "blockLet", DebugScopeType.Block);
            });
        }

        [Fact]
        public void HasCorrectScopeChainForFunction()
        {
            string script = @"
            function add(a, b)
            {
                debugger;
                return a + b;
            }
            const x = 1;
            const y = 2;
            const z = add(x, y);";

            TestHelpers.TestAtBreak(script, info =>
            {
                Assert.Collection(info.CurrentScopeChain,
                    scope => AssertScope(scope, DebugScopeType.Local, "arguments", "a", "b"),
                    scope => AssertScope(scope, DebugScopeType.Global, "x", "y", "z", "add"));
            });
        }

        [Fact]
        public void HasCorrectScopeChainForNestedFunction()
        {
            string script = @"
            function add(a, b)
            {
                function power(a)
                {
                    debugger;
                    return a * a;
                }
                return power(a) + b;
            }
            const x = 1;
            const y = 2;
            const z = add(x, y);";

            TestHelpers.TestAtBreak(script, info =>
            {
                Assert.Collection(info.CurrentScopeChain,
                    scope => AssertScope(scope, DebugScopeType.Local, "arguments", "a"),
                    scope => AssertScope(scope, DebugScopeType.Closure, "b", "power"), // a, this, arguments shadowed by local
                    scope => AssertScope(scope, DebugScopeType.Global, "x", "y", "z", "add"));
            });
        }

        [Fact]
        public void HasCorrectScopeChainForBlock()
        {
            string script = @"
            function add(a, b)
            {
                if (a > 0)
                {
                    const y = b / a;
                    debugger;
                }
                return a + b;
            }
            const x = 1;
            const y = 2;
            const z = add(x, y);";

            TestHelpers.TestAtBreak(script, info =>
            {
                Assert.Collection(info.CurrentScopeChain,
                    scope => AssertScope(scope, DebugScopeType.Block, "y"),
                    scope => AssertScope(scope, DebugScopeType.Local, "arguments", "a", "b"),
                    scope => AssertScope(scope, DebugScopeType.Global, "x", "z", "add")); // y shadowed
            });
        }

        [Fact]
        public void HasCorrectScopeChainForNestedBlock()
        {
            string script = @"
            function add(a, b)
            {
                if (a > 0)
                {
                    const y = b / a;
                    if (y > 0)
                    {
                        const x = b / y;
                        debugger;
                    }
                }
                return a + b;
            }
            const x = 1;
            const y = 2;
            const z = add(x, y);";

            TestHelpers.TestAtBreak(script, info =>
            {
                Assert.Collection(info.CurrentScopeChain,
                    scope => AssertScope(scope, DebugScopeType.Block, "x"),
                    scope => AssertScope(scope, DebugScopeType.Block, "y"),
                    scope => AssertScope(scope, DebugScopeType.Local, "arguments", "a", "b"),
                    scope => AssertScope(scope, DebugScopeType.Global, "z", "add")); // x, y shadowed
            });
        }

        [Fact]
        public void HasCorrectScopeChainForCatch()
        {
            string script = @"
            function func()
            {
                let a = 1;
                try
                {
                    throw new Error('test');
                }
                catch (error)
                {
                    debugger;
                }
            }
            func();";

            TestHelpers.TestAtBreak(script, info =>
            {
                Assert.Collection(info.CurrentScopeChain,
                    scope => AssertScope(scope, DebugScopeType.Catch, "error"),
                    scope => AssertScope(scope, DebugScopeType.Block, "a"),
                    scope => AssertScope(scope, DebugScopeType.Local, "arguments"),
                    scope => AssertScope(scope, DebugScopeType.Global, "func"));
            });
        }

        [Fact]
        public void HasCorrectScopeChainForWith()
        {
            string script = @"
            const obj = { a: 2, b: 4 };
            with (obj)
            {
                const x = a;
                debugger;
            };";

            TestHelpers.TestAtBreak(script, info =>
            {
                Assert.Collection(info.CurrentScopeChain,
                    scope => AssertScope(scope, DebugScopeType.Block, "x"),
                    scope => AssertScope(scope, DebugScopeType.With, "a", "b"),
                    scope => AssertScope(scope, DebugScopeType.Global, "obj"));
            });
        }

        [Fact]
        public void ScopeChainIncludesNonEmptyScopes()
        {
            string script = @"
            const x = 2;
            if (x > 0)
            {
                const y = x;
                if (x > 1)
                {
                    const z = x;
                    debugger;
                }
            }";

            TestHelpers.TestAtBreak(script, info =>
            {
                Assert.Collection(info.CurrentScopeChain,
                    scope => AssertScope(scope, DebugScopeType.Block, "z"),
                    scope => AssertScope(scope, DebugScopeType.Block, "y"),
                    scope => AssertScope(scope, DebugScopeType.Global, "x"));
            });
        }

        [Fact]
        public void ScopeChainExcludesEmptyScopes()
        {
            string script = @"
            const x = 2;
            if (x > 0)
            {
                if (x > 1)
                {
                    const z = x;
                    debugger;
                }
            }";

            TestHelpers.TestAtBreak(script, info =>
            {
                Assert.Collection(info.CurrentScopeChain,
                    scope => AssertScope(scope, DebugScopeType.Block, "z"),
                    scope => AssertScope(scope, DebugScopeType.Global, "x"));
            });
        }

        [Fact]
        public void ResolvesScopeChainsUpTheCallStack()
        {
            string script = @"
            const x = 1;
            function foo(a, c)
            {
                debugger;
            }
            
            function bar(b)
            {
                foo(b, 2);
            }

            bar(x);";

            TestHelpers.TestAtBreak(script, info =>
            {
                Assert.Collection(info.CallStack,
                    frame => Assert.Collection(frame.ScopeChain,
                        // in foo()
                        scope => AssertScope(scope, DebugScopeType.Local, "arguments", "a", "c"),
                        scope => AssertScope(scope, DebugScopeType.Global, "x", "foo", "bar")
                    ),
                    frame => Assert.Collection(frame.ScopeChain,
                        // in bar()
                        scope => AssertScope(scope, DebugScopeType.Local, "arguments", "b"),
                        scope => AssertScope(scope, DebugScopeType.Global, "x", "foo", "bar")
                    ),
                    frame => Assert.Collection(frame.ScopeChain,
                        // in global
                        scope => AssertScope(scope, DebugScopeType.Global, "x", "foo", "bar")
                    )
                );
            });
        }
    }
}
