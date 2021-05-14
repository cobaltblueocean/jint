﻿using Jint.Native;
using Jint.Tests.Runtime.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Jint.Tests.Runtime.ExtensionMethods
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

        [Fact]
        public void ShouldPrioritizingNonGenericMethod()
        {
            var options = new Options();
            options.AddExtensionMethods(typeof(CustomStringExtensions));

            var engine = new Engine(options);
            var result = engine.Execute("\"{'name':'Mickey'}\".DeserializeObject()").GetCompletionValue().ToObject() as dynamic;

            Assert.Equal("Mickey", result.name);
        }

        [Fact]
        public void PrototypeFunctionsShouldNotBeOverridden()
        {
            var engine = new Engine(opts =>
            {
                opts.AddExtensionMethods(typeof(CustomStringExtensions));
            });

            //uses split function from StringPrototype
            var arr = engine.Execute("'yes,no'.split(',')").GetCompletionValue().AsArray();
            Assert.Equal("yes", arr[0]);
            Assert.Equal("no", arr[1]);

            //uses split function from CustomStringExtensions
            var arr2 = engine.Execute("'yes,no'.split(2)").GetCompletionValue().AsArray();
            Assert.Equal("ye", arr2[0]);
            Assert.Equal("s,no", arr2[1]);
        }

        [Fact]
        public void OverridePrototypeFunctions()
        {
            var engine = new Engine(opts =>
            {
                opts.AddExtensionMethods(typeof(OverrideStringPrototypeExtensions));
            });

            //uses the overridden split function from OverrideStringPrototypeExtensions
            var arr = engine.Execute("'yes,no'.split(',')").GetCompletionValue().AsArray();
            Assert.Equal("YES", arr[0]);
            Assert.Equal("NO", arr[1]);
        }

        [Fact]
        public void HasOwnPropertyShouldWorkCorrectlyInPresenceOfExtensionMethods()
        {
            var person = new Person();

            var options = new Options();
            options.AddExtensionMethods(typeof(PersonExtensions));

            var engine = new Engine(options);
            engine.SetValue("person", person);

            var isBogusInPerson = engine.Execute("'bogus' in person").GetCompletionValue().AsBoolean();
            Assert.False(isBogusInPerson);

            var propertyValue = engine.Execute("person.bogus").GetCompletionValue();
            Assert.Equal(JsValue.Undefined, propertyValue);
        }

        private Engine GetLinqEngine()
        {
            return new Engine(opts =>
            {
                opts.AddExtensionMethods(typeof(Enumerable));
            });
        }

        [Fact]
        public void LinqExtensionMethodWithoutGenericParameter()
        {
            var engine = GetLinqEngine();
            var intList = new List<int>() { 0, 1, 2, 3 };

            engine.SetValue("intList", intList);
            var intSumRes = engine.Execute("intList.Sum()").GetCompletionValue().AsNumber();
            Assert.Equal(6, intSumRes);
        }

        [Fact]
        public void LinqExtensionMethodWithSingleGenericParameter()
        {
            var engine = GetLinqEngine();
            var stringList = new List<string>() { "working", "linq" };
            engine.SetValue("stringList", stringList);

            var stringSumRes = engine.Execute("stringList.Sum(x => x.length)").GetCompletionValue().AsNumber();
            Assert.Equal(11, stringSumRes);
        }

        [Fact]
        public void LinqExtensionMethodWithMultipleGenericParameters()
        {
            var engine = GetLinqEngine();
            var stringList = new List<string>() { "working", "linq" };
            engine.SetValue("stringList", stringList);

            var stringRes = engine.Execute("stringList.Select((x) => x + 'a').ToArray().join()").GetCompletionValue().AsString();
            Assert.Equal("workinga,linqa", stringRes);

            // The method ambiguity resolver is not so smart to choose the Select method with the correct number of parameters
            // Thus, the following script will not work as expected.
            // stringList.Select((x, i) => x + i).ToArray().join()
        }
    }
}
