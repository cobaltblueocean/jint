using System.Reflection;
using System.Text.Json.Nodes;
using Jint.Native;
using Jint.Runtime.Interop;
using System.Text.Json;

namespace Jint.Tests.PublicInterface;

public sealed class SystemTextJsonValueConverter : IObjectConverter
{
    public bool TryConvert(Engine engine, object value, out JsValue result)
    {
        if (value is JsonValue jsonValue)
        {
            var valueKind = jsonValue.GetValueKind();
            switch (valueKind)
            {
                case JsonValueKind.Object:
                case JsonValueKind.Array:
                    result = JsValue.FromObject(engine, jsonValue);
                    break;
                case JsonValueKind.String:
                    result = jsonValue.ToString();
                    break;
                case JsonValueKind.Number:
                    if (jsonValue.TryGetValue<double>(out var doubleValue))
                    {
                        result = JsNumber.Create(doubleValue);
                    }
                    else
                    {
                        result = JsValue.Undefined;
                    }
                    break;
                case JsonValueKind.True:
                    result = JsBoolean.True;
                    break;
                case JsonValueKind.False:
                    result = JsBoolean.False;
                    break;
                case JsonValueKind.Undefined:
                    result = JsValue.Undefined;
                    break;
                case JsonValueKind.Null:
                    result = JsValue.Null;
                    break;
                default:
                    result = JsValue.Undefined;
                    break;
            }
            return true;
        }
        result = JsValue.Undefined;
        return false;

    }
}
public partial class InteropTests
{
    [Fact]
    public void AccessingJsonNodeShouldWork()
    {
        const string Json = """
        {
            "falseValue": false,
            "employees": {
                "trueValue": true,
                "falseValue": false,
                "number": 123.456,
                "zeroNumber": 0,
                "emptyString":"",
                "nullValue":null,
                "other": "abc",
                "type": "array",
                "value": [
                    {
                        "firstName": "John",
                        "lastName": "Doe"
                    },
                    {
                        "firstName": "Jane",
                        "lastName": "Doe"
                    }
                ]
            }
        }
        """;

        var variables = JsonNode.Parse(Json);

        var engine = new Engine(options =>
        {
            // make JsonArray behave like JS array
            options.Interop.WrapObjectHandler = static (e, target, type) =>
            {
                if (target is JsonArray)
                {
                    var wrapped = ObjectWrapper.Create(e, target);
                    wrapped.Prototype = e.Intrinsics.Array.PrototypeObject;
                    return wrapped;
                }

                return ObjectWrapper.Create(e, target);
            };

            options.AddObjectConverter(new SystemTextJsonValueConverter());
            // we cannot access this[string] with anything else than JsonObject, otherwise itw will throw
            options.Interop.TypeResolver = new TypeResolver
            {
                MemberFilter = static info =>
                {
                    if (info.ReflectedType != typeof(JsonObject) && info.Name == "Item" && info is PropertyInfo p)
                    {
                        var parameters = p.GetIndexParameters();
                        return parameters.Length != 1 || parameters[0].ParameterType != typeof(string);
                    }

                    return true;
                }
            };
        });

        engine
            .SetValue("falseValue", false)
            .SetValue("variables", variables)
            .Execute("""
                 function populateFullName() {
                     return variables['employees'].value.map(item => {
                         var newItem =
                         {
                             "firstName": item.firstName,
                             "lastName": item.lastName,
                             "fullName": item.firstName + ' ' + item.lastName
                         };

                         return newItem;
                     });
                 }
             """);

        // reading data
        var result = engine.Evaluate("populateFullName()").AsArray();
        Assert.Equal((uint) 2, result.Length);
        Assert.Equal("John Doe", result[0].AsObject()["fullName"]);
        Assert.Equal("Jane Doe", result[1].AsObject()["fullName"]);
        Assert.True(engine.Evaluate("variables.employees.trueValue == true").AsBoolean());
        Assert.True(engine.Evaluate("variables.employees.number == 123.456").AsBoolean());
        Assert.True(engine.Evaluate("variables.employees.other == 'abc'").AsBoolean());

        // mutating data via JS
        engine.Evaluate("variables.employees.type = 'array2'");
        engine.Evaluate("variables.employees.value[0].firstName = 'Jake'");

        //Assert.Equal("array2", engine.Evaluate("variables['employees']['type']").ToString());

        result = engine.Evaluate("populateFullName()").AsArray();
        Assert.Equal((uint) 2, result.Length);
        Assert.Equal("Jake Doe", result[0].AsObject()["fullName"]);

        // Validate boolean value in the if condition.
        Assert.Equal(1, engine.Evaluate("if(!falseValue){ return 1 ;} else {return 0;}").AsNumber());
        Assert.Equal(1, engine.Evaluate("if(falseValue===false){ return 1 ;} else {return 0;}").AsNumber());
        Assert.True(engine.Evaluate("!variables.zeroNumber").AsBoolean());
        Assert.True(engine.Evaluate("!variables.emptyString").AsBoolean());
        Assert.True(engine.Evaluate("!variables.nullValue").AsBoolean());
        var result2 = engine.Evaluate("!variables.falseValue");
        var result3 = engine.Evaluate("!falseValue");
        var result4 = engine.Evaluate("variables.falseValue");
        var result5 = engine.Evaluate("falseValue");
        Assert.NotNull(result2);

        Assert.Equal(1, engine.Evaluate("if(variables.falseValue===false){ return 1 ;} else {return 0;}").AsNumber());
        Assert.Equal(1, engine.Evaluate("if(falseValue===variables.falseValue){ return 1 ;} else {return 0;}").AsNumber());
        Assert.Equal(1, engine.Evaluate("if(!variables.falseValue){ return 1 ;} else {return 0;}").AsNumber());
        Assert.Equal(1, engine.Evaluate("if(!variables.employees.falseValue){ return 1 ;} else {return 0;}").AsNumber());
        Assert.Equal(0, engine.Evaluate("if(!variables.employees.trueValue) return 1 ; else return 0;").AsNumber());


        // mutating original object that is wrapped inside the engine
        variables["employees"]["trueValue"] = false;
        variables["employees"]["number"] = 456.789;
        variables["employees"]["other"] = "def";
        variables["employees"]["type"] = "array";
        variables["employees"]["value"][0]["firstName"] = "John";

        Assert.Equal("array", engine.Evaluate("variables['employees']['type']").ToString());

        result = engine.Evaluate("populateFullName()").AsArray();
        Assert.Equal((uint) 2, result.Length);
        Assert.Equal("John Doe", result[0].AsObject()["fullName"]);
        Assert.True(engine.Evaluate("variables.employees.trueValue == false").AsBoolean());
        Assert.True(engine.Evaluate("variables.employees.number == 456.789").AsBoolean());
        Assert.True(engine.Evaluate("variables.employees.other == 'def'").AsBoolean());
    }
}
