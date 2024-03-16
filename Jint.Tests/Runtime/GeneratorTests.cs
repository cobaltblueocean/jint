namespace Jint.Tests.Runtime;

public class GeneratorTests
{
    [Fact]
    public void LoopYield()
    {
        const string Script = """
          const foo = function*() {
            yield 'a';
            yield 'b';
            yield 'c';
          };

          let str = '';
          for (const val of foo()) {
            str += val;
          }
          return str;
      """;

        var engine = new Engine();
        Assert.Equal("abc", engine.Evaluate(Script));
    }

    [Fact]
    public void ReturnDuringYield()
    {
        const string Script = """
          const foo = function*() {
            yield 'a';
            return;
            yield 'c';
          };

          let str = '';
          for (const val of foo()) {
            str += val;
          }
          return str;
      """;

        var engine = new Engine();
        Assert.Equal("a", engine.Evaluate(Script));
    }

    [Fact]
    public void LoneReturnInYield()
    {
        const string Script = """
          const foo = function*() {
            return;
          };

          let str = '';
          for (const val of foo()) {
            str += val;
          }
          return str;
      """;

        var engine = new Engine();
        Assert.Equal("", engine.Evaluate(Script));
    }

    [Fact]
    public void LoneReturnValueInYield()
    {
        const string Script = """
          const foo = function*() {
            return 'a';
          };

          let str = '';
          for (const val of foo()) {
            str += val;
          }
          return str;
      """;

        var engine = new Engine();
        Assert.Equal("", engine.Evaluate(Script));
    }

    [Fact]
    public void YieldUndefined()
    {
        const string Script = """
          const foo = function*() {
            yield undefined;
          };

          let str = '';
          for (const val of foo()) {
            str += val;
          }
          return str;
      """;

        var engine = new Engine();
        Assert.Equal("undefined", engine.Evaluate(Script));
    }

    [Fact]
    public void ReturnUndefined()
    {
        const string Script = """
          const foo = function*() {
            return undefined;
          };

          let str = '';
          for (const val of foo()) {
            str += val;
          }
          return str;
      """;

        var engine = new Engine();
        Assert.Equal("", engine.Evaluate(Script));
    }

    [Fact]
    public void Basic()
    {
        var engine = new Engine();
        engine.Execute("function * generator() { yield 5; yield 6; };");
        engine.Execute("var iterator = generator(); var item = iterator.next();");
        Assert.Equal(5, engine.Evaluate("item.value"));
        Assert.False(engine.Evaluate("item.done").AsBoolean());
        engine.Execute("item = iterator.next();");
        Assert.Equal(6, engine.Evaluate("item.value"));
        Assert.False(engine.Evaluate("item.done").AsBoolean());
        engine.Execute("item = iterator.next();");
        Assert.True(engine.Evaluate("item.value === void undefined").AsBoolean());
        Assert.True(engine.Evaluate("item.done").AsBoolean());
    }

    [Fact]
    public void FunctionExpressions()
    {
        var engine = new Engine();
        engine.Execute("var generator = function * () { yield 5; yield 6; };");
        engine.Execute("var iterator = generator(); var item = iterator.next();");
        Assert.Equal(5, engine.Evaluate("item.value"));
        Assert.False(engine.Evaluate("item.done").AsBoolean());
        engine.Execute("item = iterator.next();");
        Assert.Equal(6, engine.Evaluate("item.value"));
        Assert.False(engine.Evaluate("item.done").AsBoolean());
        engine.Execute("item = iterator.next();");
        Assert.True(engine.Evaluate("item.value === void undefined").AsBoolean());
        Assert.True(engine.Evaluate("item.done").AsBoolean());
    }

    [Fact]
    public void CorrectThisBinding()
    {
        var engine = new Engine();
        engine.Execute("var generator = function * () { yield 5; yield 6; };");
        engine.Execute("var iterator = { g: generator, x: 5, y: 6 }.g(); var item = iterator.next();");
        Assert.Equal(5, engine.Evaluate("item.value"));
        Assert.False(engine.Evaluate("item.done").AsBoolean());
        engine.Execute("item = iterator.next();");
        Assert.Equal(6, engine.Evaluate("item.value"));
        Assert.False(engine.Evaluate("item.done").AsBoolean());
        engine.Execute("item = iterator.next();");
        Assert.True(engine.Evaluate("item.value === void undefined").AsBoolean());
        Assert.True(engine.Evaluate("item.done").AsBoolean());
    }

    [Fact(Skip = "TODO es6-generators")]
    public void Sending()
    {
        const string Script = """
          var sent;
          function * generator() {
            sent = [yield 5, yield 6];
          };
          var iterator = generator();
          iterator.next();
          iterator.next("foo");
          iterator.next("bar");
        """;

        var engine = new Engine();
        engine.Execute(Script);

        Assert.Equal("foo", engine.Evaluate("sent[0]"));
        Assert.Equal("bar", engine.Evaluate("sent[1]"));
    }

    [Fact(Skip = "TODO es6-generators")]
    public void Sending2()
    {
        const string Script = """
        function* counter(value) {
          while (true) {
            const step = yield value++;
        
            if (step) {
              value += step;
            }
          }
        }
        
        const generatorFunc = counter(0);
        """;

        var engine = new Engine();
        engine.Execute(Script);

        Assert.Equal(0, engine.Evaluate("generatorFunc.next().value")); // 0
        Assert.Equal(1, engine.Evaluate("generatorFunc.next().value")); // 1
        Assert.Equal(2, engine.Evaluate("generatorFunc.next().value")); // 2
        Assert.Equal(3, engine.Evaluate("generatorFunc.next().value")); // 3
        Assert.Equal(14, engine.Evaluate("generatorFunc.next(10).value")); // 14
        Assert.Equal(15, engine.Evaluate("generatorFunc.next().value")); // 15
        Assert.Equal(26, engine.Evaluate("generatorFunc.next(10).value")); // 26
    }

    [Fact(Skip = "TODO es6-generators")]
    public void Fibonacci()
    {
        const string Script = """
            function* fibonacci() {
              let current = 0;
              let next = 1;
              while (true) {
                const reset = yield current;
                [current, next] = [next, next + current];
                if (reset) {
                  current = 0;
                  next = 1;
                }
              }
            }
            
            const sequence = fibonacci();
        """;

        var engine = new Engine();
        engine.Execute(Script);

        Assert.Equal(0, engine.Evaluate("sequence.next().value"));
        Assert.Equal(1, engine.Evaluate("sequence.next().value"));
        Assert.Equal(1, engine.Evaluate("sequence.next().value"));
        Assert.Equal(2, engine.Evaluate("sequence.next().value"));
        Assert.Equal(3, engine.Evaluate("sequence.next().value"));
        Assert.Equal(5, engine.Evaluate("sequence.next().value"));
        Assert.Equal(9, engine.Evaluate("sequence.next().value"));
        Assert.Equal(0, engine.Evaluate("sequence.next(true).value"));
        Assert.Equal(1, engine.Evaluate("sequence.next().value)"));
        Assert.Equal(1, engine.Evaluate("sequence.next().value)"));
        Assert.Equal(2, engine.Evaluate("sequence.next().value)"));
    }
}
