﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Jint.Runtime;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Jint.Tests.Ecma
{
    public class Chapter6 : EcmaTest
    {
        [Theory(DisplayName = "Ecma Chapter 6")]
        [MemberData(nameof(SourceFiles), parameters: new object[] {"ch06", false })]
        [MemberData(nameof(SourceFiles), parameters: new object[] {"ch06", true }, Skip = "Skipped")]
        protected void RunTest(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }

    public class Chapter7 : EcmaTest
    {
        [Theory(DisplayName = "Ecma Chapter 7")]
        [MemberData(nameof(SourceFiles), parameters: new object[] {"ch07", false })]
        [MemberData(nameof(SourceFiles), parameters: new object[] {"ch07", true }, Skip = "Skipped")]
        protected void RunTest(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }

    public class Chapter8 : EcmaTest
    {
        [Theory(DisplayName = "Ecma Chapter 8")]
        [MemberData(nameof(SourceFiles), parameters: new object[] {"ch08", false })]
        [MemberData(nameof(SourceFiles), parameters: new object[] {"ch08", true }, Skip = "Skipped")]
        protected void RunTest(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }

    public class Chapter9 : EcmaTest
    {
        [Theory(DisplayName = "Ecma Chapter 9")]
        [MemberData(nameof(SourceFiles), parameters: new object[] {"ch09", false })]
        [MemberData(nameof(SourceFiles), parameters: new object[] {"ch09", true }, Skip = "Skipped")]
        protected void RunTest(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }

    public class Chapter10 : EcmaTest
    {
        [Theory(DisplayName = "Ecma Chapter 10")]
        [MemberData(nameof(SourceFiles), parameters: new object[] {"ch10", false })]
        [MemberData(nameof(SourceFiles), parameters: new object[] {"ch10", true }, Skip = "Skipped")]
        protected void RunTest(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }

    public class Chapter11 : EcmaTest
    {
        [Theory(DisplayName = "Ecma Chapter 11")]
        [MemberData(nameof(SourceFiles), parameters: new object[] {"ch11", false })]
        [MemberData(nameof(SourceFiles), parameters: new object[] {"ch11", true }, Skip = "Skipped")]
        protected void RunTest(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }

    public class Chapter12 : EcmaTest
    {
        [Theory(DisplayName = "Ecma Chapter 12")]
        [MemberData(nameof(SourceFiles), parameters: new object[] {"ch12", false })]
        [MemberData(nameof(SourceFiles), parameters: new object[] {"ch12", true }, Skip = "Skipped")]
        protected void RunTest(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }

    public class Chapter13 : EcmaTest
    {
        [Theory(DisplayName = "Ecma Chapter 13")]
        [MemberData(nameof(SourceFiles), parameters: new object[] {"ch13", false })]
        [MemberData(nameof(SourceFiles), parameters: new object[] {"ch13", true }, Skip = "Skipped")]
        protected void RunTest(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }

    public class Chapter14 : EcmaTest
    {
        [Theory(DisplayName = "Ecma Chapter 14")]
        [MemberData(nameof(SourceFiles), parameters: new object[] {"ch14", false })]
        [MemberData(nameof(SourceFiles), parameters: new object[] {"ch14", true }, Skip = "Skipped")]
        protected void RunTest(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }

    public class Chapter15 : EcmaTest
    {
        [Theory(DisplayName = "Ecma Chapter 15")]
        [MemberData(nameof(SourceFiles), parameters: new object[] {"ch15", false })]
        [MemberData(nameof(SourceFiles), parameters: new object[] {"ch15", true }, Skip = "Skipped")]
        protected void RunTest(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }

    public abstract class EcmaTest
    {
        private static string _lastError;
        private static string staSource;
        protected Action<string> Error = s => { _lastError = s; };
        protected string BasePath;

        public EcmaTest()
        {
            var assemblyPath = new Uri(typeof(EcmaTest).GetTypeInfo().Assembly.CodeBase).LocalPath;
            var assemblyDirectory = new FileInfo(assemblyPath).Directory;

            BasePath = assemblyDirectory.Parent.Parent.Parent.FullName;
        }

        protected void RunTestInternal(SourceFile sourceFile)
        {
            var fullName = Path.Combine(BasePath, sourceFile.BasePath, sourceFile.Source);
            if (!File.Exists(fullName))
            {
                throw new ArgumentException("Could not find source file: " + fullName);
            }

            string code = File.ReadAllText(fullName);
            var negative = code.Contains("@negative");

            RunTestCode(code, negative);
        }

        protected void RunTestCode(string code, bool negative)
        {
            _lastError = null;

            //NOTE: The Date tests in test262 assume the local timezone is Pacific Standard Time
            var pacificTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
            var engine = new Engine(cfg => cfg.LocalTimeZone(pacificTimeZone));

            // loading driver
            if (staSource == null)
            {
                var driverFilename = Path.Combine(BasePath, "TestCases\\sta.js");
                staSource = File.ReadAllText(driverFilename);
            }

            engine.Execute(staSource);

            if (negative)
            {
                try
                {
                    engine.Execute(code);
                    Assert.True(_lastError != null);
                    Assert.False(true);
                }
                catch
                {
                    // exception is expected
                }

            }
            else
            {
                try
                {
                    engine.Execute(code);
                }
                catch (JavaScriptException j)
                {
                    _lastError = TypeConverter.ToString(j.Error);
                }
                catch (Exception e)
                {
                    _lastError = e.ToString();
                }

                Assert.Null(_lastError);
            }
        }

        public static IEnumerable<object[]> SourceFiles(string prefix, bool skipped)
        {
            var assemblyPath = new Uri(typeof(EcmaTest).GetTypeInfo().Assembly.CodeBase).LocalPath;
            var assemblyDirectory = new FileInfo(assemblyPath).Directory;

            var localPath = assemblyDirectory.Parent.Parent.Parent.FullName;

            var fixturesPath = Path.Combine(localPath, @"TestCases\alltests.json");

            var content = File.ReadAllText(fixturesPath);
            var doc = JArray.Parse(content);
            var results = new List<object[]>();
            var path = Path.Combine(localPath, "TestCases");

            foreach(JObject entry in doc)
            {
                var sourceFile = new SourceFile(entry, path);

                if (prefix != null && !sourceFile.Source.StartsWith(prefix))
                {
                    continue;
                }

                if (skipped == sourceFile.Skip)
                {
                    results.Add(new object [] { sourceFile });
                }
            }

            return results;
        }

        public class SourceFile
        {
            public SourceFile(JObject node, string basePath)
            {
                Skip = node["skip"].Value<bool>();
                Source = node["source"].ToString();
                Reason = node["reason"].ToString();
                BasePath = basePath;
            }

            public string Source { get; set; }
            public bool Skip { get; set; }
            public string Reason { get; set; }
            public string BasePath { get; }

            public override string ToString()
            {
                return Source;
            }
        }

    }
}
