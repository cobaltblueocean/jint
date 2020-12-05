﻿using Jint.Tests.Runtime.Domain;
using Jint.Tests.Runtime.ExtensionMethods;
using Xunit;

namespace Jint.Tests.Runtime
{
    public class ExtensionMethodsTest
    {
        [Fact]
        public void ShouldInvokeObjectExtensionMethod()
        {
            var person = new Person();
            person.Name = "Mickey Mouse";
            person.Age = 35;

            var options = new Options();
            options.AddExtensionMethods(typeof(PersonExtensions));

            var engine = new Engine(options);
            engine.SetValue("person", person);
            var age = engine.Execute("person.MultiplyAge(2)").GetCompletionValue().AsInteger();

            Assert.Equal(70, age);
        }

        [Fact]
        public void ShouldInvokeStringExtensionMethod()
        {
            var options = new Options();
            options.AddExtensionMethods(typeof(CustomStringExtensions));

            var engine = new Engine(options);
            var result = engine.Execute("\"Hello World!\".Backwards()").GetCompletionValue().AsString();

            Assert.Equal("!dlroW olleH", result);
        }

        [Fact]
        public void ShouldInvokeNumberExtensionMethod()
        {
            var options = new Options();
            options.AddExtensionMethods(typeof(DoubleExtensions));

            var engine = new Engine(options);
            var result = engine.Execute("let numb = 27; numb.Add(13)").GetCompletionValue().AsInteger();

            Assert.Equal(40, result);
        }
    }
}
