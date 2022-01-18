using Jint.Native.Json;
using Jint.Runtime;
using Xunit;

namespace Jint.Tests.Runtime
{
    public class JsonTests
    {
        [Fact]
        public void CanParseTabsInProperties()
        {
             var engine = new Engine();
             const string script = @"JSON.parse(""{\""abc\\tdef\"": \""42\""}"");";
             var obj = engine.Evaluate(script).AsObject();
             Assert.True(obj.HasOwnProperty("abc\tdef"));
        }

        [Theory]
        [InlineData("{\"a\":1", "Unexpected end of JSON input at position 6")]
        [InlineData("{\"a\":1},", "Unexpected token ',' in JSON at position 7")]
        [InlineData("{1}", "Unexpected number in JSON at position 1")]
        [InlineData("{\"a\" \"a\"}", "Unexpected string in JSON at position 5")]
        [InlineData("{true}", "Unexpected token 'true' in JSON at position 1")]
        [InlineData("{null}", "Unexpected token 'null' in JSON at position 1")]
        [InlineData("{:}", "Unexpected token ':' in JSON at position 1")]
        [InlineData("\"\\xah\"", "Expected hexadecimal digit in JSON at position 4")]
        [InlineData("0123", "Unexpected token '1' in JSON at position 1")]  // leading 0 (octal number) not allowed
        [InlineData("1e+A", "Unexpected token 'A' in JSON at position 3")]
        [InlineData("truE", "Unexpected token 'tru' in JSON at position 0")]
        [InlineData("nul", "Unexpected token 'nul' in JSON at position 0")]
        [InlineData("\"ab\t\"", "Invalid character in JSON at position 3")] // invalid char in string literal
        [InlineData("\"ab", "Unexpected end of JSON input at position 3")] // unterminated string literal
        [InlineData("alpha", "Unexpected token 'a' in JSON at position 0")]
        [InlineData("[1,\na]", "Unexpected token 'a' in JSON at position 4")] // multiline
        [InlineData("\x06", "Unexpected token '\x06' in JSON at position 0")] // control char
        public void ShouldReportHelpfulSyntaxErrorForInvalidJson(string json, string expectedMessage)
        {
            var engine = new Engine();
            var parser = new JsonParser(engine);
            var ex = Assert.ThrowsAny<JavaScriptException>(() =>
            {
                parser.Parse(json);
            });

            Assert.Equal(expectedMessage, ex.Message);

            var error = ex.Error as Native.Error.ErrorInstance;
            Assert.NotNull(error);
            Assert.Equal("SyntaxError", error.Get("name"));
        }
    }
}