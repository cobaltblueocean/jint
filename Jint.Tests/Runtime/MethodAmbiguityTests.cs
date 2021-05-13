﻿using System;
using Xunit;

namespace Jint.Tests.Runtime
{
    public class MethodAmbiguityTests : IDisposable
    {
        private readonly Engine _engine;

        public MethodAmbiguityTests()
        {
            _engine = new Engine(cfg => cfg
                .AllowOperatorOverloading())
                .SetValue("log", new Action<object>(Console.WriteLine))
                .SetValue("throws", new Func<Action, Exception>(Assert.Throws<Exception>))
                .SetValue("assert", new Action<bool>(Assert.True))
                .SetValue("assertFalse", new Action<bool>(Assert.False))
                .SetValue("equal", new Action<object, object>(Assert.Equal))
                .SetValue("TestClass", typeof(TestClass))
                .SetValue("ChildTestClass", typeof(ChildTestClass))
            ;
        }

        void IDisposable.Dispose()
        {
        }

        private void RunTest(string source)
        {
            _engine.Execute(source);
        }

        public class TestClass
        {
            public int TestMethod(double a, string b, double c) => 0;
            public int TestMethod(double a, double b, double c) => 1;
            public int TestMethod(TestClass a, string b, double c) => 2;
            public int TestMethod(TestClass a, TestClass b, double c) => 3;
            public int TestMethod(TestClass a, TestClass b, TestClass c) => 4;
            public int TestMethod(TestClass a, double b, string c) => 5;
            public int TestMethod(ChildTestClass a, double b, string c) => 6;

            public static implicit operator TestClass(double i) => new TestClass();
            public static implicit operator double(TestClass tc) => 0;
            public static explicit operator string(TestClass tc) => "";
        }

        public class ChildTestClass : TestClass { }

        [Fact]
        public void BestMatchingMethodShouldBeCalled()
        {
            RunTest(@"
                var tc = new TestClass();
                var cc = new ChildTestClass();

                equal(0, tc.TestMethod(0, '', 0));
                equal(1, tc.TestMethod(0, 0, 0));
                equal(2, tc.TestMethod(tc, '', 0));
                equal(3, tc.TestMethod(tc, tc, 0));
                equal(4, tc.TestMethod(tc, tc, tc));
                equal(5, tc.TestMethod(tc, tc, ''));
                equal(5, tc.TestMethod(0, 0, ''));

                equal(6, tc.TestMethod(cc, 0, ''));
                equal(1, tc.TestMethod(cc, 0, 0));
                equal(6, tc.TestMethod(cc, 0, tc));
            ");
        }
    }
}
