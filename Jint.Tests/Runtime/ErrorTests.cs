﻿using Esprima;
using Jint.Runtime;
using Jint.Tests.Runtime.TestClasses;
using System;
using System.Collections.Generic;
using Xunit;

namespace Jint.Tests.Runtime
{
    public class ErrorTests
    {
        [Fact]
        public void CanReturnCorrectErrorMessageAndLocation1()
        {
            const string script = @"
var a = {};

var b = a.user.name;
";

            var engine = new Engine();
            var e = Assert.Throws<JavaScriptException>(() => engine.Execute(script));
            Assert.Equal("Cannot read property 'name' of undefined", e.Message);
            Assert.Equal(4, e.Location.Start.Line);
            Assert.Equal(15, e.Location.Start.Column);
        }
        [Fact]
        public void CanReturnCorrectErrorMessageAndLocation1WithoutReferencedName()
        {
            const string script = @"
var c = a(b().Length);
";

            var engine = new Engine();
            engine.SetValue("a", new Action<string>((_) => { }));
            engine.SetValue("b", new Func<string>(() => null));
            var e = Assert.Throws<JavaScriptException>(() => engine.Execute(script));
            Assert.Equal("Cannot read property 'Length' of null", e.Message);
            Assert.Equal(2, e.Location.Start.Line);
            Assert.Equal(14, e.Location.Start.Column);
        }

        [Fact]
        public void CanReturnCorrectErrorMessageAndLocation2()
        {
            const string script = @"
 test();
";

            var engine = new Engine();
            var e = Assert.Throws<JavaScriptException>(() => engine.Execute(script));
            Assert.Equal("test is not defined", e.Message);
            Assert.Equal(2, e.Location.Start.Line);
            Assert.Equal(1, e.Location.Start.Column);
        }

        [Fact]
        public void CanProduceCorrectStackTrace()
        {
            var engine = new Engine();

            engine.Execute(@"
var a = function(v) {
  return v.xxx.yyy;
}

var b = function(v) {
  return a(v);
}
            ", new ParserOptions("custom.js"));

            var e = Assert.Throws<JavaScriptException>(() => engine.Execute("var x = b(7);", new ParserOptions("main.js")));
            Assert.Equal("Cannot read property 'yyy' of undefined", e.Message);
            Assert.Equal(3, e.Location.Start.Line);
            Assert.Equal(15, e.Location.Start.Column);
            Assert.Equal("custom.js", e.Location.Source);

            var stack = e.StackTrace;
            EqualIgnoringNewLineDifferences(@"   at a (v) custom.js:3:16
   at b (v) custom.js:7:10
   at main.js:1:9", stack);
        }

        [Fact]
        public void ErrorObjectHasTheStackTraceImmediately()
        {
            var engine = new Engine();

            engine.Execute(@"
var a = function(v) {
  return Error().stack;
}

var b = function(v) {
  return a(v);
}
            ", new ParserOptions("custom.js"));

            var e = engine.Evaluate(@"b(7)", new ParserOptions("main.js")).AsString();

            var stack = e;
            EqualIgnoringNewLineDifferences(@"   at Error custom.js:3:10
   at a (v) custom.js:3:10
   at b (v) custom.js:7:10
   at main.js:1:1", stack);
        }

        [Fact]
        public void ThrownErrorObjectHasStackTraceInCatch()
        {
            var engine = new Engine();

            engine.Execute(@"
var a = function(v) {
  try {
    throw Error();
  } catch(err) {
    return err.stack;
  }
}

var b = function(v) {
  return a(v);
}
            ", new ParserOptions("custom.js"));

            var e = engine.Evaluate(@"b(7)", new ParserOptions("main.js")).AsString();

            var stack = e;
            EqualIgnoringNewLineDifferences(@"   at Error custom.js:4:11
   at a (v) custom.js:4:11
   at b (v) custom.js:11:10
   at main.js:1:1", stack);
        }


        [Fact]
        public void GeneratedErrorHasStackTraceInCatch()
        {
            var engine = new Engine();

            engine.Execute(@"
var a = function(v) {
  try {
    var a = ''.xyz();
  } catch(err) {
    return err.stack;
  }
}

var b = function(v) {
  return a(v);
}
            ", new ParserOptions("custom.js"));

            var e = engine.Evaluate(@"b(7)", new ParserOptions("main.js")).AsString();

            var stack = e;
            EqualIgnoringNewLineDifferences(@"   at a (v) custom.js:4:13
   at b (v) custom.js:11:10
   at main.js:1:1", stack);
        }

        [Fact]
        public void ErrorObjectHasOwnPropertyStack()
        {
            var res = new Engine().Evaluate(@"Error().hasOwnProperty('stack')").AsBoolean();
            Assert.True(res);
        }

        private class Folder
        {
            public Folder Parent { get; set; }
            public string Name { get; set; }
        }

        [Fact]
        public void CallStackBuildingShouldSkipResolvingFromEngine()
        {
            var engine = new Engine(o => o.LimitRecursion(200));
            var recordedFolderTraversalOrder = new List<string>();
            engine.SetValue("log", new Action<object>(o => recordedFolderTraversalOrder.Add(o.ToString())));

            var folder = new Folder
            {
                Name = "SubFolder2",
                Parent = new Folder
                {
                    Name = "SubFolder1",
                    Parent = new Folder
                    {
                        Name = "Root",
                        Parent = null,
                    }
                }
            };

            engine.SetValue("folder", folder);

            var javaScriptException = Assert.Throws<JavaScriptException>(() =>
           engine.Execute(@"
                var Test = {
                    recursive: function(folderInstance) {
                        // Enabling the guard here corrects the problem, but hides the hard fault
                        // if (folderInstance==null) return null;
                        log(folderInstance.Name);
                    if (folderInstance==null) return null;
                        return this.recursive(folderInstance.parent);
                    }
                }

                Test.recursive(folder);"
           ));

            Assert.Equal("Cannot read property 'Name' of null", javaScriptException.Message);
            EqualIgnoringNewLineDifferences(@"   at recursive (folderInstance) <anonymous>:6:44
   at recursive (folderInstance) <anonymous>:8:32
   at recursive (folderInstance) <anonymous>:8:32
   at recursive (folderInstance) <anonymous>:8:32
   at <anonymous>:12:17", javaScriptException.StackTrace);

            var expected = new List<string>
            {
                "SubFolder2", "SubFolder1", "Root"
            };
            Assert.Equal(expected, recordedFolderTraversalOrder);
        }

        [Fact]
        public void StackTraceCollectedOnThreeLevels()
        {
            var engine = new Engine();
            const string script = @"var a = function(v) {
    return v.xxx.yyy;
}

var b = function(v) {
    return a(v);
}

var x = b(7);";

            var ex = Assert.Throws<JavaScriptException>(() => engine.Execute(script));

            const string expected = @"Jint.Runtime.JavaScriptException: Cannot read property 'yyy' of undefined
   at a (v) <anonymous>:2:18
   at b (v) <anonymous>:6:12
   at <anonymous>:9:9";

            EqualIgnoringNewLineDifferences(expected, ex.ToString());
        }

        [Fact]
        public void StackTraceCollectedForImmediatelyInvokedFunctionExpression()
        {
            var engine = new Engine();
            const string script = @"function getItem(items, itemIndex) {
    var item = items[itemIndex];

    return item;
}

(function (getItem) {
    var items = null,
        item = getItem(items, 5)
        ;

    return item;
})(getItem);";

            var parserOptions = new ParserOptions("get-item.js")
            {
                AdaptRegexp = true,
                Tolerant = true
            };
            var ex = Assert.Throws<JavaScriptException>(() => engine.Execute(script, parserOptions));

            const string expected = @"Jint.Runtime.JavaScriptException: Cannot read property '5' of null
   at getItem (items, itemIndex) get-item.js:2:22
   at (anonymous) (getItem) get-item.js:9:16
   at get-item.js:13:2";

            EqualIgnoringNewLineDifferences(expected, ex.ToString());
        }

        [Fact]
        public void StackTraceIsForOriginalException()
        {
            var engine = new Engine();
            engine.SetValue("HelloWorld", new HelloWorld());
            const string script = @"HelloWorld.ThrowException();";

            var ex = Assert.Throws<DivideByZeroException>(() => engine.Execute(script));

            const string expected = "HelloWorld";

            ContainsIgnoringNewLineDifferences(expected, ex.ToString());
        }

        [Theory]
        [InlineData("Error")]
        [InlineData("EvalError")]
        [InlineData("RangeError")]
        [InlineData("SyntaxError")]
        [InlineData("TypeError")]
        [InlineData("ReferenceError")]
        public void ErrorsHaveCorrectConstructor(string type)
        {
            var engine = new Engine();
            engine.Execute($"const o = new {type}();");
            Assert.True(engine.Evaluate($"o.constructor === {type}").AsBoolean());
            Assert.Equal(type, engine.Evaluate("o.constructor.name").AsString());
        }

        private static void EqualIgnoringNewLineDifferences(string expected, string actual)
        {
            expected = expected.Replace("\r\n", "\n");
            actual = actual.Replace("\r\n", "\n");
            Assert.Equal(expected, actual);
        }

        private static void ContainsIgnoringNewLineDifferences(string expectedSubstring, string actualString)
        {
            expectedSubstring = expectedSubstring.Replace("\r\n", "\n");
            actualString = actualString.Replace("\r\n", "\n");
            Assert.Contains(expectedSubstring, actualString);
        }

        [Fact]
        public void CustomException()
        {
            var engine = new Engine();
            const string filename = "someFile.js";
            JintJsException jsException = Assert.Throws<JintJsException>(() =>
            {
                try
                {
                    const string script = @"
                        var test = 42; // just adding a line for a non zero line offset
                        throw new Error('blah');
                    ";

                    engine.Execute(script);
                }
                catch (JavaScriptException ex)
                {
                    throw new JintJsException(filename, ex);
                }
            });

            Assert.Equal(24, jsException.Column);
            Assert.Equal(3, jsException.LineNumber);
            Assert.Equal(filename, jsException.Module);
        }

        [Fact]
        public void CustomExceptionUsesCopyConstructor()
        {
            var engine = new Engine();
            const string filename = "someFile.js";
            JintJsException2 jsException = Assert.Throws<JintJsException2>(() =>
            {
                try
                {
                    const string script = @"
                        var test = 42; // just adding a line for a non zero line offset
                        throw new Error('blah');
                    ";

                    engine.Execute(script);
                }
                catch (JavaScriptException ex)
                {
                    throw new JintJsException2(filename, ex);
                }
            });

            Assert.Equal(24, jsException.Column);
            Assert.Equal(3, jsException.LineNumber);
            Assert.Equal(filename, jsException.Module);
        }

    }

    public class JintJsException : JavaScriptException
    {
        public string Module
        {
            get;
            private set;
        }
        private JavaScriptException _jsException;

        public JintJsException(string moduleName, JavaScriptException jsException) : base(jsException.Error)
        {
            Module = moduleName;
            _jsException = jsException;
            Location = jsException.Location;
        }

        public override string Message
        {
            get
            {
                var scriptFilename = (Module != null) ? "Filepath: " + Module + " " : "";
                var errorMsg = $"{scriptFilename}{_jsException.Message}";
                return errorMsg;
            }
        }

        public override string? StackTrace
        {
            get { return _jsException.StackTrace; }
        }
    }

    public class JintJsException2 : JavaScriptException
    {
        public string Module
		{
            get;
            private set;
		}
	
        public JintJsException2(string moduleName, JavaScriptException jsException) : base(jsException)
        {
            Module = moduleName;
        }
    }
}
