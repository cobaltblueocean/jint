using Esprima;
using Esprima.Ast;
using Esprima.Utils;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Native.Function;

internal sealed class ClassDefinition
{
    private static readonly MethodDefinition _superConstructor;
    internal static CallExpression _defaultSuperCall;

    internal static readonly MethodDefinition _emptyConstructor;

    internal readonly string? _className;
    private readonly Expression? _superClass;
    private readonly ClassBody _body;

    static ClassDefinition()
    {
        // generate missing constructor AST only once
        static MethodDefinition CreateConstructorMethodDefinition(string source)
        {
            var script = new JavaScriptParser().ParseScript(source);
            var classDeclaration = (ClassDeclaration) script.Body[0];
            return (MethodDefinition) classDeclaration.Body.Body[0];
        }

        _superConstructor = CreateConstructorMethodDefinition("class temp { constructor(...args) { super(...args); } }");
        _defaultSuperCall = (CallExpression) ((ExpressionStatement) _superConstructor.Value.Body.Body[0]).Expression;
        _emptyConstructor = CreateConstructorMethodDefinition("class temp { constructor() {} }");
    }

    public ClassDefinition(
        string? className,
        Expression? superClass,
        ClassBody body)
    {
        _className = className;
        _superClass = superClass;
        _body = body;
    }

    public void Initialize()
    {
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-runtime-semantics-classdefinitionevaluation
    /// </summary>
    public JsValue BuildConstructor(
        EvaluationContext context,
        EnvironmentRecord env)
    {
        // A class definition is always strict mode code.
        using var _ = new StrictModeScope(true, true);

        var engine = context.Engine;
        var classScope = JintEnvironment.NewDeclarativeEnvironment(engine, env);

        if (_className is not null)
        {
            classScope.CreateImmutableBinding(_className, true);
        }

        var outerPrivateEnvironment = engine.ExecutionContext.PrivateEnvironment;
        var classPrivateEnvironment = JintEnvironment.NewPrivateEnvironment(engine, outerPrivateEnvironment);

        ObjectInstance? protoParent = null;
        ObjectInstance? constructorParent = null;
        if (_superClass is null)
        {
            protoParent = engine.Realm.Intrinsics.Object.PrototypeObject;
            constructorParent = engine.Realm.Intrinsics.Function.PrototypeObject;
        }
        else
        {
            engine.UpdateLexicalEnvironment(classScope);
            var superclass = JintExpression.Build(_superClass).GetValue(context);
            engine.UpdateLexicalEnvironment(env);

            if (superclass.IsNull())
            {
                protoParent = null;
                constructorParent = engine.Realm.Intrinsics.Function.PrototypeObject;
            }
            else if (!superclass.IsConstructor)
            {
                ExceptionHelper.ThrowTypeError(engine.Realm, "super class is not a constructor");
            }
            else
            {
                var temp = superclass.Get("prototype");
                if (temp is ObjectInstance protoParentObject)
                {
                    protoParent = protoParentObject;
                }
                else if (temp.IsNull())
                {
                    // OK
                }
                else
                {
                    ExceptionHelper.ThrowTypeError(engine.Realm, "cannot resolve super class prototype chain");
                    return default;
                }

                constructorParent = (ObjectInstance) superclass;
            }
        }

        ObjectInstance proto = new JsObject(engine) { _prototype = protoParent };

        var privateBoundNames = new List<string>();
        MethodDefinition? constructor = null;
        var classBody = _body.Body;
        for (var i = 0; i < classBody.Count; ++i)
        {
            var element = classBody[i];
            if (element is MethodDefinition { Kind: PropertyKind.Constructor } c)
            {
                constructor = c;
            }

            privateBoundNames.Clear();
            element.GetBoundNames(privateBoundNames, privateIdentifiers: true);
            for (var j = 0; j < privateBoundNames.Count; j++)
            {
                var identifier = privateBoundNames[j];
                classPrivateEnvironment.Names.Add(new PrivateName(identifier));
            }
        }

        constructor ??= _superClass != null
            ? _superConstructor
            : _emptyConstructor;

        engine.UpdateLexicalEnvironment(classScope);

        ScriptFunctionInstance F;
        try
        {
            var constructorInfo = constructor.DefineMethod(proto, constructorParent);
            F = constructorInfo.Closure;

            var name = env is ModuleEnvironmentRecord ? _className : _className ?? "";
            if (name is not null)
            {
                F.SetFunctionName(name);
            }

            F.MakeConstructor(writableProperty: false, proto);
            F._constructorKind = _superClass is null ? ConstructorKind.Base : ConstructorKind.Derived;
            F.MakeClassConstructor();
            proto.CreateMethodProperty(CommonProperties.Constructor, F);

            var instancePrivateMethods = new List<PrivateElement>();
            var staticPrivateMethods = new List<PrivateElement>();
            var instanceFields = new List<object>();
            var staticElements = new List<object>();

            foreach (var e in _body.Body)
            {
                if (e is MethodDefinition { Kind: PropertyKind.Constructor })
                {
                    continue;
                }

                var isStatic = e is MethodDefinition { Static: true } or AccessorProperty { Static: true } or PropertyDefinition { Static: true } or StaticBlock;

                var target = !isStatic ? proto : F;
                var element = ClassElementEvaluation(engine, target, e);
                if (element is PrivateElement privateElement)
                {
                    var container = !isStatic ? instancePrivateMethods : staticPrivateMethods;
                    var index = container.FindIndex(x => x.Key == privateElement.Key);
                    if (index != -1)
                    {
                        var pe = container[index];
                        var combined = privateElement.Get is null
                            ? new PrivateElement { Key = privateElement.Key, Kind = PrivateElementKind.Accessor, Get = pe.Get, Set = privateElement.Set }
                            : new PrivateElement { Key = privateElement.Key, Kind = PrivateElementKind.Accessor, Get = privateElement.Get, Set = pe.Set };

                        container[index] = combined;
                    }
                    else
                    {
                        container.Add(privateElement);
                    }
                }
                else if (element is ClassFieldDefinition)
                {
                    if (!isStatic)
                    {
                        instanceFields.Add(element);
                    }
                    else
                    {
                        staticElements.Add(element);
                    }
                }
                else if (element is ClassStaticBlockDefinition)
                {
                    staticElements.Add(element);
                }
            }

            if (_className is not null)
            {
                classScope.InitializeBinding(_className, F);
            }

            F._privateMethods = instancePrivateMethods;
            F._fields = instanceFields;

            for (var i = 0; i < staticPrivateMethods.Count; i++)
            {
                F.PrivateMethodOrAccessorAdd(staticPrivateMethods[i]);
            }

            for (var i = 0; i < staticElements.Count; i++)
            {
                var elementRecord = staticElements[i];
                if (elementRecord is ClassFieldDefinition classFieldDefinition)
                {
                    ObjectInstance.DefineField(F, classFieldDefinition);
                }
                else
                {
                    engine.Call(((ClassStaticBlockDefinition) elementRecord).BodyFunction, F);
                }
            }
        }
        finally
        {
            engine.UpdateLexicalEnvironment(env);
            engine.UpdatePrivateEnvironment(outerPrivateEnvironment);
        }


        engine.UpdatePrivateEnvironment(outerPrivateEnvironment);

        return F;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-static-semantics-classelementevaluation
    /// </summary>
    private static object? ClassElementEvaluation(Engine engine, ObjectInstance target, ClassElement e)
    {
        return e switch
        {
            PropertyDefinition p => ClassFieldDefinitionEvaluation(engine, target, p),
            MethodDefinition m => MethodDefinitionEvaluation(engine, target, m, enumerable: false),
            StaticBlock s => ClassStaticBlockDefinitionEvaluation(engine, target, s),
            _ => null
        };
    }

    /// <summary>
    /// /https://tc39.es/ecma262/#sec-runtime-semantics-classfielddefinitionevaluation
    /// </summary>
    private static ClassFieldDefinition ClassFieldDefinitionEvaluation(Engine engine, ObjectInstance homeObject, PropertyDefinition fieldDefinition)
    {
        var name = fieldDefinition.GetKey(engine);

        JintExpression? initializer = null;
        if (fieldDefinition.Value is not null)
        {
            //var intrinsics = engine.Realm.Intrinsics;
            //var env = engine.ExecutionContext.LexicalEnvironment;
            //var privateEnv = engine.ExecutionContext.PrivateEnvironment;
            //
            //var definition = new JintFunctionDefinition((IFunction) fieldDefinition.Value);
            //initializer = intrinsics.Function.OrdinaryFunctionCreate(intrinsics.Function.PrototypeObject, definition, FunctionThisMode.Global, env, privateEnv);
            //
            //initializer.MakeMethod(homeObject);
            ////   g. Set initializer.[[ClassFieldInitializerName]] to name.
            initializer = JintExpression.Build(fieldDefinition.Value);
        }

        return new ClassFieldDefinition { Name = name, Initializer = initializer };
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-runtime-semantics-classstaticblockdefinitionevaluation
    /// </summary>
    private static ClassStaticBlockDefinition ClassStaticBlockDefinitionEvaluation(Engine engine, ObjectInstance homeObject, StaticBlock o)
    {
        var intrinsics = engine.Realm.Intrinsics;

        var definition = new JintFunctionDefinition(new ClassStaticBlockFunction(o));

        var lex = engine.ExecutionContext.LexicalEnvironment;
        var privateEnv = engine.ExecutionContext.PrivateEnvironment;

        var bodyFunction = intrinsics.Function.OrdinaryFunctionCreate(intrinsics.Function.PrototypeObject, definition, FunctionThisMode.Global, lex, privateEnv);

        bodyFunction.MakeMethod(homeObject);

        return new ClassStaticBlockDefinition { BodyFunction = bodyFunction };
    }

    private sealed class ClassStaticBlockFunction : Node, IFunction
    {
        private readonly BlockStatement _statement;
        private readonly NodeList<Node> _params;

        public ClassStaticBlockFunction(StaticBlock staticBlock) : base(Nodes.StaticBlock)
        {
            _statement = new BlockStatement(staticBlock.Body);
            _params = new NodeList<Node>();
        }

        protected override object? Accept(AstVisitor visitor)
        {
            throw new NotImplementedException();
        }

        public Identifier? Id => null;
        public ref readonly NodeList<Node> Params => ref _params;
        public StatementListItem Body => _statement;
        public bool Generator => false;
        public bool Expression => false;
        public bool Strict => false;
        public bool Async => false;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-runtime-semantics-methoddefinitionevaluation
    /// </summary>
    private static PrivateElement? MethodDefinitionEvaluation(
        Engine engine,
        ObjectInstance obj,
        MethodDefinition method,
        bool enumerable)
    {
        if (method.Kind != PropertyKind.Get && method.Kind != PropertyKind.Set)
        {
            var methodDef = method.DefineMethod(obj);
            methodDef.Closure.SetFunctionName(methodDef.Key);
            return DefineMethodProperty(obj, methodDef.Key, methodDef.Closure, enumerable);
        }

        var value = method.TryGetKey(engine);
        var propKey = TypeConverter.ToPropertyKey(value);
        var function = method.Value as IFunction;
        if (function is null)
        {
            ExceptionHelper.ThrowSyntaxError(obj.Engine.Realm);
        }

        var getter = method.Kind == PropertyKind.Get;

        var closure = new ScriptFunctionInstance(
            obj.Engine,
            function,
            obj.Engine.ExecutionContext.LexicalEnvironment,
            true);

        closure.MakeMethod(obj);
        closure.SetFunctionName(propKey, getter ? "get" : "set");

        if (method.Key is PrivateIdentifier privateIdentifier)
        {
            return new PrivateElement
            {
                Key = new PrivateName(privateIdentifier.Name),
                Kind = PrivateElementKind.Accessor,
                Get = getter ? closure : null,
                Set = !getter ? closure : null
            };
        }

        var propDesc = new GetSetPropertyDescriptor(
            getter ? closure : null,
            !getter ? closure : null,
            PropertyFlag.Configurable);

        obj.DefinePropertyOrThrow(propKey, propDesc);

        return null;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-definemethodproperty
    /// </summary>
    private static PrivateElement? DefineMethodProperty(ObjectInstance homeObject, JsValue key, ScriptFunctionInstance closure, bool enumerable)
    {
        if (key.IsPrivateName())
        {
            return new PrivateElement { Key = (PrivateName) key, Kind = PrivateElementKind.Method, Value = closure };
        }

        var desc = new PropertyDescriptor(closure, enumerable ? PropertyFlag.Enumerable : PropertyFlag.NonEnumerable);
        homeObject.DefinePropertyOrThrow(key, desc);
        return null;
    }
}
