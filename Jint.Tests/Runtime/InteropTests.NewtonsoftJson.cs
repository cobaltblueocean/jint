using Jint.Native.Date;
using Jint.Runtime;
using Newtonsoft.Json.Linq;

namespace Jint.Tests.Runtime
{
    public partial class InteropTests
    {
        [Fact]
        public void AccessingJObjectShouldWork()
        {
            var o = new JObject
            {
                new JProperty("name", "test-name")
            };
            _engine.SetValue("o", o);
            Assert.True(_engine.Evaluate("return o.name == 'test-name'").AsBoolean());
        }

        [Fact]
        public void AccessingJArrayViaIntegerIndexShouldWork()
        {
            var o = new JArray("item1", "item2");
            _engine.SetValue("o", o);
            Assert.True(_engine.Evaluate("return o[0] == 'item1'").AsBoolean());
            Assert.True(_engine.Evaluate("return o[1] == 'item2'").AsBoolean());
        }

        [Fact]
        public void DictionaryLikeShouldCheckIndexerAndFallBackToProperty()
        {
            const string json = @"{ ""Type"": ""Cat"" }";
            var jObjectWithTypeProperty = JObject.Parse(json);

            _engine.SetValue("o", jObjectWithTypeProperty);

            var typeResult = _engine.Evaluate("o.Type");

            // JToken requires conversion
            Assert.Equal("Cat", TypeConverter.ToString(typeResult));

            // weak equality does conversions from native types
            Assert.True(_engine.Evaluate("o.Type == 'Cat'").AsBoolean());
        }

        [Fact]
        public void ShouldBeAbleToIndexJObjectWithStrings()
        {
            var engine = new Engine();

            const string json = @"
            {
                'Properties': {
                    'expirationDate': {
                        'Value': '2021-10-09T00:00:00Z'
                    }
                }
            }";

            var obj = JObject.Parse(json);
            engine.SetValue("o", obj);
            var value = engine.Evaluate("o.Properties.expirationDate.Value");
            var dateInstance = Assert.IsAssignableFrom<DateInstance>(value);
            Assert.Equal(DateTime.Parse("2021-10-09T00:00:00Z").ToUniversalTime(), dateInstance.ToDateTime());
        }

        // https://github.com/OrchardCMS/OrchardCore/issues/10648
        [Fact]
        public void EngineShouldStringifyAnJObjectListWithValuesCorrectly()
        {
            var engine = new Engine();
            var queryResults = new List<dynamic>
            {
                new { Text = "Text1", Value = 1 },
                new { Text = "Text2", Value = 2 }
            };

            engine.SetValue("testSubject", queryResults.Select(x => JObject.FromObject(x)));
            var fromEngine = engine.Evaluate("return JSON.stringify(testSubject);");
            var result = fromEngine.ToString();

            // currently we do not materialize LINQ enumerables
            // Assert.Equal("[{\"Text\":\"Text1\",\"Value\":1},{\"Text\":\"Text2\",\"Value\":2}]", result);

            Assert.Equal("{\"Current\":null}", result);
        }

        [Fact]
        public void EngineShouldStringifyJObjectFromObjectListWithValuesCorrectly()
        {
            var engine = new Engine();

            var source = new dynamic[]
            {
                new { Text = "Text1", Value = 1 },
                new { Text = "Text2", Value = 2, Null = (object) null, Date = new DateTime(2015, 6, 25, 0, 0, 0, DateTimeKind.Utc) }
            };

            engine.SetValue("testSubject", source.Select(x => JObject.FromObject(x)).ToList());
            var fromEngine = engine.Evaluate("return JSON.stringify(testSubject);");
            var result = fromEngine.ToString();

            Assert.Equal("[{\"Text\":\"Text1\",\"Value\":1},{\"Text\":\"Text2\",\"Value\":2,\"Null\":null,\"Date\":\"2015-06-25T00:00:00.000Z\"}]", result);
        }

        [Fact]
        public void EngineShouldMapJObjectFromObjectArrayWithValuesCorrectly()
        {
            var engine = new Engine();

            var source = new dynamic[]
            {
                new { Text = "Text1" },
                new { Text = "Text2" }
            };

            engine.SetValue("testSubject", source.Select(x => JObject.FromObject(x)).ToArray());

            var fromEngine = engine.Evaluate(@"
                var testArray = testSubject.map(x=> x.Text);  
                return testArray.toString();");
            var result = fromEngine.ToString();

            Assert.Equal("Text1,Text2", result);
        }

        [Fact]
        public void EngineShouldMapJObjectFromObjectListWithValuesCorrectly()
        {
            var engine = new Engine();

            var source = new dynamic[]
            {
                new { Text = "Text1" },
                new { Text = "Text2" }
            };

            engine.SetValue("testSubject", source.Select(x => JObject.FromObject(x)).ToList());
            
            var fromEngine = engine.Evaluate(@"
                var testArray = testSubject.map(x=> x.Text);  
                return testArray.toString();");
            var result = fromEngine.ToString();

            Assert.Equal("Text1,Text2", result);
        }
    }
}
