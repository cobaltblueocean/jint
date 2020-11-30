﻿using Jint.Runtime.Debugger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Jint.Tests.Runtime.Debugger
{
    public class CallStackTests
    {
        [Fact]
        public void NamesRegularFunction()
        {
            var engine = new Engine(options => options.DebugMode(true));

            int steps = 0;
            engine.Step += (sender, info) =>
            {
                if (steps == 2)
                {
                    Assert.Equal("regularFunction", info.CallStack.Peek());
                }
                steps++;
                return StepMode.Into;
            };

            engine.Execute(
                @"function regularFunction() { return 'test'; }
                regularFunction()");

            Assert.Equal(3, steps);
        }

        [Fact]
        public void NamesFunctionExpression()
        {
            var engine = new Engine(options => options.DebugMode(true));

            int steps = 0;
            engine.Step += (sender, info) =>
            {
                if (steps == 2)
                {
                    Assert.Equal("functionExpression", info.CallStack.Peek());
                }
                steps++;
                return StepMode.Into;
            };

            engine.Execute(
                @"const functionExpression = function() { return 'test'; }
                functionExpression()");

            Assert.Equal(3, steps);
        }
    }
}
