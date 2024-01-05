using Jint.Native;
using Jint.Runtime;

namespace Jint.Tests.Runtime;

public class ModuleTests
{
    private readonly Engine _engine;

    public ModuleTests()
    {
        _engine = new Engine();
    }

    [Fact]
    public void ShouldExportNamed()
    {
        _engine.Modules.Add("my-module", "export const value = 'exported value';");
        var ns = _engine.Modules.Import("my-module");

        Assert.Equal("exported value", ns.Get("value").AsString());
    }

    [Fact]
    public void ShouldExportNamedListRenamed()
    {
        _engine.Modules.Add("my-module", "const value1 = 1; const value2 = 2; export { value1 as renamed1, value2 as renamed2 }");
        var ns =  _engine.Modules.Import("my-module");

        Assert.Equal(1, ns.Get("renamed1").AsInteger());
        Assert.Equal(2, ns.Get("renamed2").AsInteger());
    }

    [Fact]
    public void ShouldExportDefault()
    {
        _engine.Modules.Add("my-module", "export default 'exported value';");
        var ns =  _engine.Modules.Import("my-module");

        Assert.Equal("exported value", ns.Get("default").AsString());
    }

    [Fact]
    public void ShouldExportAll()
    {
        _engine.Modules.Add("module1", "export const value = 'exported value';");
        _engine.Modules.Add("module2", "export * from 'module1';");
        var ns =  _engine.Modules.Import("module2");

        Assert.Equal("exported value", ns.Get("value").AsString());
    }

    [Fact]
    public void ShouldImportNamed()
    {
        _engine.Modules.Add("imported-module", "export const value = 'exported value';");
        _engine.Modules.Add("my-module", "import { value } from 'imported-module'; export const exported = value;");
        var ns =  _engine.Modules.Import("my-module");

        Assert.Equal("exported value", ns.Get("exported").AsString());
    }

    [Fact]
    public void ShouldImportRenamed()
    {
        _engine.Modules.Add("imported-module", "export const value = 'exported value';");
        _engine.Modules.Add("my-module", "import { value as renamed } from 'imported-module'; export const exported = renamed;");
        var ns =  _engine.Modules.Import("my-module");

        Assert.Equal("exported value", ns.Get("exported").AsString());
    }

    [Fact]
    public void ShouldImportDefault()
    {
        _engine.Modules.Add("imported-module", "export default 'exported value';");
        _engine.Modules.Add("my-module", "import imported from 'imported-module'; export const exported = imported;");
        var ns =  _engine.Modules.Import("my-module");

        Assert.Equal("exported value", ns.Get("exported").AsString());
    }

    [Fact]
    public void ShouldImportAll()
    {
        _engine.Modules.Add("imported-module", "export const value = 'exported value';");
        _engine.Modules.Add("my-module", "import * as imported from 'imported-module'; export const exported = imported.value;");
        var ns =  _engine.Modules.Import("my-module");

        Assert.Equal("exported value", ns.Get("exported").AsString());
    }

    [Fact]
    public void ShouldImportDynamically()
    {
        var received = false;
        _engine.Modules.Add("imported-module", builder => builder.ExportFunction("signal", () => received = true));
        _engine.Modules.Add("my-module", "import('imported-module').then(ns => { ns.signal(); });");

         _engine.Modules.Import("my-module");

        Assert.True(received);
    }

    [Fact]
    public void ShouldPropagateParseError()
    {
        _engine.Modules.Add("imported", "export const invalid;");
        _engine.Modules.Add("my-module", "import { invalid } from 'imported';");

        var exc = Assert.Throws<JavaScriptException>(() =>  _engine.Modules.Import("my-module"));
        Assert.Equal("Error while loading module: error in module 'imported': Line 1: Missing initializer in const declaration", exc.Message);
        Assert.Equal("imported", exc.Location.Source);
    }

    [Fact]
    public void ShouldPropagateLinkError()
    {
        _engine.Modules.Add("imported", "export invalid;");
        _engine.Modules.Add("my-module", "import { value } from 'imported';");

        var exc = Assert.Throws<JavaScriptException>(() =>  _engine.Modules.Import("my-module"));
        Assert.Equal("Error while loading module: error in module 'imported': Line 1: Unexpected identifier", exc.Message);
        Assert.Equal("imported", exc.Location.Source);
    }

    [Fact]
    public void ShouldPropagateExecuteError()
    {
        _engine.Modules.Add("my-module", "throw new Error('imported successfully');");

        var exc = Assert.Throws<JavaScriptException>(() =>  _engine.Modules.Import("my-module"));
        Assert.Equal("imported successfully", exc.Message);
        Assert.Equal("my-module", exc.Location.Source);
    }

    [Fact]
    public void ShouldPropagateThrowStatementThroughJavaScriptImport()
    {
        _engine.Modules.Add("imported-module", "throw new Error('imported successfully');");
        _engine.Modules.Add("my-module", "import 'imported-module';");

        var exc = Assert.Throws<JavaScriptException>(() =>  _engine.Modules.Import("my-module"));
        Assert.Equal("imported successfully", exc.Message);
    }

    [Fact]
    public void ShouldAddModuleFromJsValue()
    {
        _engine.Modules.Add("my-module", builder => builder.ExportValue("value", JsString.Create("hello world")));
        var ns =  _engine.Modules.Import("my-module");

        Assert.Equal("hello world", ns.Get("value").AsString());
    }

    [Fact]
    public void ShouldAddModuleFromClrInstance()
    {
        _engine.Modules.Add("imported-module", builder => builder.ExportObject("value", new ImportedClass
        {
            Value = "instance value"
        }));
        _engine.Modules.Add("my-module", "import { value } from 'imported-module'; export const exported = value.value;");
        var ns =  _engine.Modules.Import("my-module");

        Assert.Equal("instance value", ns.Get("exported").AsString());
    }

    [Fact]
    public void ShouldAllowInvokeUserDefinedClass()
    {
        _engine.Modules.Add("user", "export class UserDefined { constructor(v) { this._v = v; } hello(c) { return `hello ${this._v}${c}`; } }");
        var ctor =  _engine.Modules.Import("user").Get("UserDefined");
        var instance = _engine.Construct(ctor, JsString.Create("world"));
        var result = instance.GetMethod("hello").Call(instance, JsString.Create("!"));

        Assert.Equal("hello world!", result);
    }

    [Fact]
    public void ShouldAddModuleFromClrType()
    {
        _engine.Modules.Add("imported-module", builder => builder.ExportType<ImportedClass>());
        _engine.Modules.Add("my-module", "import { ImportedClass } from 'imported-module'; export const exported = new ImportedClass().value;");
        var ns =  _engine.Modules.Import("my-module");

        Assert.Equal("hello world", ns.Get("exported").AsString());
    }

    [Fact]
    public void ShouldAddModuleFromClrFunction()
    {
        var received = new List<string>();
        _engine.Modules.Add("imported-module", builder => builder
            .ExportFunction("act_noargs", () => received.Add("act_noargs"))
            .ExportFunction("act_args", args => received.Add($"act_args:{args[0].AsString()}"))
            .ExportFunction("fn_noargs", () =>
            {
                received.Add("fn_noargs");
                return "ret";
            })
            .ExportFunction("fn_args", args =>
            {
                received.Add($"fn_args:{args[0].AsString()}");
                return "ret";
            })
        );
        _engine.Modules.Add("my-module", @"
import * as fns from 'imported-module';
export const result = [fns.act_noargs(), fns.act_args('ok'), fns.fn_noargs(), fns.fn_args('ok')];");
        var ns =  _engine.Modules.Import("my-module");

        Assert.Equal(new[]
        {
            "act_noargs",
            "act_args:ok",
            "fn_noargs",
            "fn_args:ok"
        }, received.ToArray());
        Assert.Equal(new[]
        {
            "undefined",
            "undefined",
            "ret",
            "ret"
        }, ns.Get("result").AsArray().Select(x => x.ToString()).ToArray());
    }

    private class ImportedClass
    {
        public string Value { get; set; } = "hello world";
    }

    [Fact]
    public void ShouldAllowExportMultipleImports()
    {
        _engine.Modules.Add("@mine/import1", builder => builder.ExportValue("value1", JsNumber.Create(1)));
        _engine.Modules.Add("@mine/import2", builder => builder.ExportValue("value2", JsNumber.Create(2)));
        _engine.Modules.Add("@mine", "export * from '@mine/import1'; export * from '@mine/import2'");
        _engine.Modules.Add("app", "import { value1, value2 } from '@mine'; export const result = `${value1} ${value2}`");
        var ns =  _engine.Modules.Import("app");

        Assert.Equal("1 2", ns.Get("result").AsString());
    }

    [Fact]
    public void ShouldAllowNamedStarExport()
    {
        _engine.Modules.Add("imported-module", builder => builder.ExportValue("value1", 5));
        _engine.Modules.Add("my-module", "export * as ns from 'imported-module';");
        var ns =  _engine.Modules.Import("my-module");

        Assert.Equal(5, ns.Get("ns").Get("value1").AsNumber());
    }

    [Fact]
    public void ShouldAllowChaining()
    {
        _engine.Modules.Add("dependent-module", "export const dependency = 1;");
        _engine.Modules.Add("my-module", builder => builder
            .AddSource("import { dependency } from 'dependent-module';")
            .AddSource("export const output = dependency + 1;")
            .ExportValue("num", JsNumber.Create(-1))
        );
        var ns =  _engine.Modules.Import("my-module");

        Assert.Equal(2, ns.Get("output").AsInteger());
        Assert.Equal(-1, ns.Get("num").AsInteger());
    }

    [Fact]
    public void ShouldImportOnlyOnce()
    {
        var called = 0;
        _engine.Modules.Add("imported-module", builder => builder.ExportFunction("count", args => called++));
        _engine.Modules.Add("my-module", "import { count } from 'imported-module'; count();");
         _engine.Modules.Import("my-module");
         _engine.Modules.Import("my-module");

        Assert.Equal(1, called);
    }

    [Fact]
    public void ShouldAllowSelfImport()
    {
        _engine.Modules.Add("my-globals", "export const globals = { counter: 0 };");
        _engine.Modules.Add("my-module", @"
import { globals } from 'my-globals';
import {} from 'my-module';
globals.counter++;
export const count = globals.counter;
");
        var ns =  _engine.Modules.Import("my-module");

        Assert.Equal(1, ns.Get("count").AsInteger());
    }

    [Fact]
    public void ShouldAllowCyclicImport()
    {
        // https://tc39.es/ecma262/#sec-example-cyclic-module-record-graphs

        _engine.Modules.Add("B", "import { a } from 'A'; export const b = 'b';");
        _engine.Modules.Add("A", "import { b } from 'B'; export const a = 'a';");

        var nsA =  _engine.Modules.Import("A");
        var nsB =  _engine.Modules.Import("B");

        Assert.Equal("a", nsA.Get("a").AsString());
        Assert.Equal("b", nsB.Get("b").AsString());
    }

    [Fact]
    public void ShouldSupportConstraints()
    {
        var engine = new Engine(opts => opts.TimeoutInterval(TimeSpan.FromTicks(1)));

        engine.Modules.Add("sleep", builder => builder.ExportFunction("sleep", () => Thread.Sleep(100)));
        engine.Modules.Add("my-module", "import { sleep } from 'sleep'; for(var i = 0; i < 100; i++) { sleep(); } export const result = 'ok';");
        Assert.Throws<TimeoutException>(() => engine.Modules.Import("my-module"));
    }

    [Fact]
    public void CanLoadModuleImportsFromFiles()
    {
        var engine = new Engine(options => options.EnableModules(GetBasePath()));
        engine.Modules.Add("my-module", "import { User } from './modules/user.js'; export const user = new User('John', 'Doe');");
        var ns = engine.Modules.Import("my-module");

        Assert.Equal("John Doe", ns["user"].Get("name").AsString());
    }

    [Fact]
    public void CanImportFromFile()
    {
        var engine = new Engine(options => options.EnableModules(GetBasePath()));
        var ns = engine.Modules.Import("./modules/format-name.js");
        var result = engine.Invoke(ns.Get("formatName"), "John", "Doe").AsString();

        Assert.Equal("John Doe", result);
    }

    [Fact]
    public void CanImportFromFileWithSpacesInPath()
    {
        var engine = new Engine(options => options.EnableModules(GetBasePath()));
        var ns = engine.Modules.Import("./dir with spaces/format name.js");
        var result = engine.Invoke(ns.Get("formatName"), "John", "Doe").AsString();

        Assert.Equal("John Doe", result);
    }

    [Fact]
    public void CanReuseModule()
    {
        const string Code = "export function formatName(firstName, lastName) {\r\n    return `${firstName} ${lastName}`;\r\n}";
        var module = Engine.PrepareModule(Code);
        for (var i = 0; i < 5; i++)
        {
            var engine = new Engine();
            engine.Modules.Add("__main__", x => x.AddModule(module));
            var ns = engine.Modules.Import("__main__");
            var result = engine.Invoke(ns.Get("formatName"), "John" + i, "Doe").AsString();
            Assert.Equal($"John{i} Doe", result);
        }
    }

    private static string GetBasePath()
    {
        var assemblyDirectory = new DirectoryInfo(AppDomain.CurrentDomain.RelativeSearchPath ?? AppDomain.CurrentDomain.BaseDirectory);

        var current = assemblyDirectory;
        var binDirectory = $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}";
        while (current is not null)
        {
            if (current.FullName.Contains(binDirectory) || current.Name == "bin")
            {
                current = current.Parent;
                continue;
            }

            var testDirectory = current.GetDirectories("Jint.Tests").FirstOrDefault();
            if (testDirectory == null)
            {
                current = current.Parent;
                continue;
            }

            // found it
            current = testDirectory;
            break;
        }

        if (current is null)
        {
            throw new NullReferenceException($"Could not find tests base path, assemblyPath: {assemblyDirectory}");
        }

        return Path.Combine(current.FullName, "Runtime", "Scripts");
    }
}
