﻿using Jint.Native;
using Jint.Runtime.Debugger;
using Xunit;

namespace Jint.Tests.Runtime.Debugger
{
    public class ScopeTests
    {
        [Fact]
        public void GlobalsAndLocalsIncludeGlobalConst()
        {
            string script = @"
                const globalConstant = 'test';
                debugger;
            ";

            TestHelpers.TestAtBreak(script, info =>
            {
                var variable = Assert.Single(info.Scopes[DebugScopeType.Global], g => g.Key == "globalConstant");
                Assert.Equal("test", variable.Value.AsString());

                variable = Assert.Single(info.Scopes[DebugScopeType.Local], g => g.Key == "globalConstant");
                Assert.Equal("test", variable.Value.AsString());
            });
        }

        [Fact]
        public void GlobalsAndLocalsIncludeGlobalLet()
        {
            string script = @"
                let globalLet = 'test';
                debugger;";

            TestHelpers.TestAtBreak(script, info =>
            {
                var variable = Assert.Single(info.Scopes[DebugScopeType.Global], g => g.Key == "globalLet");
                Assert.Equal("test", variable.Value.AsString());

                variable = Assert.Single(info.Scopes[DebugScopeType.Local], g => g.Key == "globalLet");
                Assert.Equal("test", variable.Value.AsString());
            });
        }

        [Fact]
        public void GlobalsAndLocalsIncludeGlobalVar()
        {
            string script = @"
                var globalVar = 'test';
                debugger;";

            TestHelpers.TestAtBreak(script, info =>
            {
                var variable = Assert.Single(info.Scopes[DebugScopeType.Global], g => g.Key == "globalVar");
                Assert.Equal("test", variable.Value.AsString());

                variable = Assert.Single(info.Scopes[DebugScopeType.Local], g => g.Key == "globalVar");
                Assert.Equal("test", variable.Value.AsString());
            });
        }

        [Fact]
        public void OnlyLocalsIncludeLocalConst()
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
                var variable = Assert.Single(info.Scopes[DebugScopeType.Local], g => g.Key == "localConst");
                Assert.Equal("test", variable.Value.AsString());
                Assert.DoesNotContain(info.Scopes[DebugScopeType.Global], g => g.Key == "localConst");
            });
        }

        [Fact]
        public void OnlyLocalsIncludeLocalLet()
        {
            string script = @"
                function test()
                {
                    let localLet = 'test';
                    debugger;
                }
                test();";

            TestHelpers.TestAtBreak(script, info =>
            {
                var variable = Assert.Single(info.Scopes[DebugScopeType.Local], g => g.Key == "localLet");
                Assert.Equal("test", variable.Value.AsString());
                Assert.DoesNotContain(info.Scopes[DebugScopeType.Global], g => g.Key == "localLet");
            });
        }

        [Fact]
        public void OnlyLocalsIncludeLocalVar()
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
                var variable = Assert.Single(info.Scopes[DebugScopeType.Local], g => g.Key == "localVar");
                Assert.Equal("test", variable.Value.AsString());
                Assert.DoesNotContain(info.Scopes[DebugScopeType.Global], g => g.Key == "localVar");
            });
        }

        [Fact]
        public void BlockScopedVariablesAreInvisibleOutsideBlock()
        {
            string script = @"
            debugger;
            {
                let blockLet = 'block';
                const blockConst = 'block';
            }";

            TestHelpers.TestAtBreak(script, info =>
            {
                Assert.DoesNotContain(info.Scopes[DebugScopeType.Local], g => g.Key == "blockLet");
                Assert.DoesNotContain(info.Scopes[DebugScopeType.Global], g => g.Key == "blockConst");
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
                Assert.Single(info.Scopes[DebugScopeType.Local], c => c.Key == "blockConst" && c.Value == JsUndefined.Undefined);
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
                Assert.Single(info.Scopes[DebugScopeType.Local], v => v.Key == "blockLet" && v.Value.AsString() == "block");
            });
        }
    }
}
