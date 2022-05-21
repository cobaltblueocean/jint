﻿using System;
using System.IO;
using Esprima;
using Jint.Native;
using Jint.Native.ArrayBuffer;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;
using Test262Harness;

namespace Jint.Tests.Test262;

public abstract partial class Test262Test
{
    private Engine BuildTestExecutor(Test262File file)
    {
        var engine = new Engine(cfg =>
        {
            var relativePath = Path.GetDirectoryName(file.FileName);
            cfg.EnableModules(new Test262ModuleLoader(State.Test262Stream.Options.FileSystem, relativePath));
        });

        engine.Execute(State.Sources["assert.js"]);
        engine.Execute(State.Sources["sta.js"]);

        engine.SetValue("print", new ClrFunctionInstance(engine, "print", (_, args) => TypeConverter.ToString(args.At(0))));

        var o = engine.Realm.Intrinsics.Object.Construct(Arguments.Empty);
        o.FastSetProperty("evalScript", new PropertyDescriptor(new ClrFunctionInstance(engine, "evalScript",
            (_, args) =>
            {
                if (args.Length > 1)
                {
                    throw new Exception("only script parsing supported");
                }

                var options = new ParserOptions { AdaptRegexp = true, Tolerant = false };
                var parser = new JavaScriptParser(args.At(0).AsString(), options);
                var script = parser.ParseScript();

                return engine.Evaluate(script);
            }), true, true, true));

        o.FastSetProperty("createRealm", new PropertyDescriptor(new ClrFunctionInstance(engine, "createRealm",
            (_, args) =>
            {
                var realm = engine._host.CreateRealm();
                realm.GlobalObject.Set("global", realm.GlobalObject);
                return realm.GlobalObject;
            }), true, true, true));

        o.FastSetProperty("detachArrayBuffer", new PropertyDescriptor(new ClrFunctionInstance(engine, "detachArrayBuffer",
            (_, args) =>
            {
                var buffer = (ArrayBufferInstance) args.At(0);
                buffer.DetachArrayBuffer();
                return JsValue.Undefined;
            }), true, true, true));

        engine.SetValue("$262", o);

        foreach (var include in file.Includes)
        {
            engine.Execute(State.Sources[include]);
        }

        return engine;
    }

    private static void ExecuteTest(Engine engine, Test262File file)
    {
        if (file.Type == ProgramType.Module)
        {
            engine.AddModule(file.FileName, builder => builder.AddSource(file.Program));
            engine.ImportModule(file.FileName);
        }
        else
        {
            engine.Execute(new JavaScriptParser(file.Program, new ParserOptions(file.FileName)).ParseScript());
        }
    }
    
    private partial bool ShouldThrow(Test262File testCase, bool strict)
    {
        return testCase.Negative;
    }
}
