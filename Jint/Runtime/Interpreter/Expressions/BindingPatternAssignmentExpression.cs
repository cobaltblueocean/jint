using System;
using System.Collections.Generic;
using Esprima.Ast;
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Function;
using Jint.Native.Iterator;
using Jint.Runtime.Environments;
using Jint.Runtime.References;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class BindingPatternAssignmentExpression : JintExpression
    {
        private readonly BindingPattern _pattern;
        private JintExpression _right;

        public BindingPatternAssignmentExpression(
            Engine engine, 
            AssignmentExpression expression) : base(engine, expression)
        {
            _pattern = (BindingPattern) expression.Left;
            _initialized = false;
        }

        protected override void Initialize()
        {
            _right = Build(_engine, ((AssignmentExpression) _expression).Right);
        }

        protected override object EvaluateInternal()
        {
            var rightValue = _right.GetValue();
            ProcessPatterns(_engine, _pattern, rightValue, true);
            return rightValue;
        }

        internal static void ProcessPatterns(Engine engine, BindingPattern pattern, JsValue argument, bool checkReference)
        {
            if (pattern is ArrayPattern ap)
            {
                HandleArrayPattern(engine, ap, argument, checkReference);
            }
            else if (pattern is ObjectPattern op)
            {
                HandleObjectPattern(engine, op, argument, checkReference);
            }
        }
        
        private static bool ConsumeFromIterator(IIterator it, out JsValue value, out bool done)
        {
            value = JsValue.Undefined;
            done = false;

            if (!it.TryIteratorStep(out var d))
            {
                done = true;
                return false;
            }

            d.TryGetValue(CommonProperties.Value, out value);
            return true;
        }
        
        private static void HandleArrayPattern(Engine engine, ArrayPattern pattern, JsValue argument, bool checkReference)
        {
            var obj = TypeConverter.ToObject(engine, argument);
            ArrayOperations arrayOperations = null;
            IIterator iterator = null;
            if (obj.IsArrayLike)
            {
                arrayOperations = ArrayOperations.For(obj);
            }
            else
            {
                if (!obj.TryGetIterator(engine, out iterator))
                {
                    ExceptionHelper.ThrowTypeError(engine);
                    return;
                }
            }

            var completionType = CompletionType.Normal;
            var close = false;
            var done = false;
            uint i = 0;
            try
            {
                for (; i < pattern.Elements.Count; i++)
                {
                    var left = pattern.Elements[(int) i];

                    if (left is null)
                    {
                        if (arrayOperations != null)
                        {
                            arrayOperations.TryGetValue(i, out _);
                        }
                        else
                        {
                            if (!ConsumeFromIterator(iterator, out _, out done))
                            {
                                break;
                            }
                        }
                        // skip assignment
                        continue;
                    }
                
                    if (left is Identifier identifier)
                    {
                        JsValue value;
                        if (arrayOperations != null)
                        {
                            arrayOperations.TryGetValue(i, out value);
                        }
                        else
                        {
                            if (!ConsumeFromIterator(iterator, out value, out done))
                            {
                                break;
                            }
                        }
                        AssignToIdentifier(engine, identifier.Name, value, checkReference);
                    }
                    else if (left is MemberExpression me)
                    {
                        close = true;
                        var reference = GetReferenceFromMember(engine, me);
                        JsValue value;
                        if (arrayOperations != null)
                        {
                            arrayOperations.TryGetValue(i, out value);
                        }
                        else
                        {
                            ConsumeFromIterator(iterator, out value, out done);
                        }

                        AssignToReference(engine, reference, value);
                    }
                    else if (left is BindingPattern bindingPattern)
                    {
                        JsValue value;
                        if (arrayOperations != null)
                        {
                            arrayOperations.TryGetValue(i, out value);
                        }
                        else
                        {
                            iterator.TryIteratorStep(out var temp);
                            value = temp;
                        }
                        ProcessPatterns(engine, bindingPattern, value, checkReference);
                    }
                    else if (left is RestElement restElement)
                    {
                        close = true;
                        Reference reference = null; 
                        if (restElement.Argument is MemberExpression memberExpression)
                        {
                            reference = GetReferenceFromMember(engine, memberExpression);
                        }
                    
                        ArrayInstance array;
                        if (arrayOperations != null)
                        {
                            var length = arrayOperations.GetLength();
                            array = engine.Array.ConstructFast(length - i);
                            for (uint j = i; j < length; ++j)
                            {
                                arrayOperations.TryGetValue(j, out var indexValue);
                                array.SetIndexValue(j - i, indexValue, updateLength: false);
                            }
                        }
                        else
                        {
                            array = engine.Array.ConstructFast(0);
                            uint index = 0;
                            done = true;
                            do
                            {
                                if (!iterator.TryIteratorStep(out var item))
                                {
                                    done = true;
                                    break;
                                }

                                item.TryGetValue(CommonProperties.Value, out var value);
                                array.SetIndexValue(index++, value, updateLength: false);
                            } while (true);

                            array.SetLength(index);
                        }

                        if (restElement.Argument is Identifier leftIdentifier)
                        {
                            AssignToIdentifier(engine, leftIdentifier.Name, array, checkReference);
                        }
                        else if (restElement.Argument is BindingPattern bp)
                        {
                            ProcessPatterns(engine, bp, array, checkReference);
                        }                    
                        else
                        {
                            AssignToReference(engine, reference,  array);
                        }
                    }
                    else if (left is AssignmentPattern assignmentPattern)
                    {
                        JsValue value;
                        if (arrayOperations != null)
                        {
                            arrayOperations.TryGetValue(i, out value);
                        }
                        else
                        {
                            ConsumeFromIterator(iterator, out value, out done);
                        }

                        if (value.IsUndefined()
                            && assignmentPattern.Right is Expression expression)
                        {
                            var jintExpression = Build(engine, expression);

                            value = jintExpression.GetValue();
                        }

                        if (assignmentPattern.Left is Identifier leftIdentifier)
                        {
                            if (assignmentPattern.Right.IsFunctionWithName())
                            {
                                ((FunctionInstance) value).SetFunctionName(new JsString(leftIdentifier.Name));
                            }

                            AssignToIdentifier(engine, leftIdentifier.Name, value, checkReference);
                        }
                        else if (assignmentPattern.Left is BindingPattern bp)
                        {
                            ProcessPatterns(engine, bp, value, checkReference);
                        }
                    }
                    else
                    {
                        ExceptionHelper.ThrowArgumentOutOfRangeException("pattern",
                            "Unable to determine how to handle array pattern element " + left);
                        break;
                    }
                }

                close = true;
            }
            catch
            {
                completionType = CompletionType.Throw;
                throw;
            }
            finally
            {
                if (close && !done)
                {
                    iterator?.Close(completionType);
                }
            }
        }

        private static void HandleObjectPattern(Engine engine, ObjectPattern pattern, JsValue argument, bool checkReference)
        {
            var processedProperties = pattern.Properties.Count > 0 && pattern.Properties[pattern.Properties.Count - 1] is RestElement
                ? new HashSet<JsValue>()
                : null;

            var source = TypeConverter.ToObject(engine, argument);
            for (var i = 0; i < pattern.Properties.Count; i++)
            {
                if (pattern.Properties[i] is Property p)
                {
                    JsValue sourceKey;
                    var identifier = p.Key as Identifier;
                    if (identifier == null || p.Computed)
                    {
                        var keyExpression = Build(engine, p.Key);
                        sourceKey = TypeConverter.ToPropertyKey(keyExpression.GetValue());
                    }
                    else
                    {
                        sourceKey = identifier.Name;
                    }

                    processedProperties?.Add(sourceKey.AsStringWithoutTypeCheck());
                    if (p.Value is AssignmentPattern assignmentPattern)
                    {
                        source.TryGetValue(sourceKey, out var value);
                        if (value.IsUndefined() && assignmentPattern.Right is Expression expression)
                        {
                            var jintExpression = Build(engine, expression);
                            value = jintExpression.GetValue();
                        }

                        if (assignmentPattern.Left is BindingPattern bp)
                        {
                            ProcessPatterns(engine, bp, value, checkReference);
                            continue;
                        }

                        var target = assignmentPattern.Left as Identifier ?? identifier;

                        if (assignmentPattern.Right.IsFunctionWithName())
                        {
                            ((FunctionInstance) value).SetFunctionName(target.Name);
                        }

                        AssignToIdentifier(engine, target.Name, value, checkReference);
                    }
                    else if (p.Value is BindingPattern bindingPattern)
                    {
                        source.TryGetValue(sourceKey, out var value);
                        ProcessPatterns(engine, bindingPattern, value, checkReference);
                    }
                    else if (p.Value is MemberExpression memberExpression)
                    {
                        var reference = GetReferenceFromMember(engine, memberExpression);
                        source.TryGetValue(sourceKey, out var value);
                        AssignToReference(engine, reference, value);
                    }
                    else
                    {
                        var identifierReference = p.Value as Identifier;
                        var target = identifierReference ?? identifier;
                        source.TryGetValue(sourceKey, out var value);
                        AssignToIdentifier(engine, target.Name, value, checkReference);
                    }
                }
                else
                {
                    var restElement = (RestElement) pattern.Properties[i];
                    if (restElement.Argument is Identifier leftIdentifier)
                    {
                        var count = Math.Max(0, source.Properties?.Count ?? 0) - processedProperties.Count;
                        var rest = engine.Object.Construct(count);
                        source.CopyDataProperties(rest, processedProperties);
                        AssignToIdentifier(engine, leftIdentifier.Name, rest, checkReference);
                    }
                    else if (restElement.Argument is BindingPattern bp)
                    {
                        ProcessPatterns(engine, bp, argument, checkReference);
                    }
                    else if (restElement.Argument is MemberExpression memberExpression)
                    {
                        var left = GetReferenceFromMember(engine, memberExpression);
                        var rest = engine.Object.Construct(0);
                        source.CopyDataProperties(rest, processedProperties);
                        AssignToReference(engine, left, rest);
                    }
                    else
                    {
                        ExceptionHelper.ThrowArgumentException("cannot handle parameter type " + restElement.Argument);
                    }
                }
            }
        }

        private static void AssignToReference(Engine engine,  Reference lref,  JsValue rval)
        {
            engine.PutValue(lref, rval);
            engine._referencePool.Return(lref);
        }

        private static Reference GetReferenceFromMember(Engine engine, MemberExpression memberExpression)
        {
            var expression = new JintMemberExpression(engine, memberExpression);
            var reference = expression.Evaluate() as Reference ?? ExceptionHelper.ThrowReferenceError<Reference>(engine);
            reference.AssertValid(engine);
            return reference;
        }

        private static void AssignToIdentifier(
            Engine engine,
            string name,
            JsValue rval,
            bool checkReference)
        {
            var env = engine.ExecutionContext.LexicalEnvironment;

            var strict = StrictModeScope.IsStrictModeCode;
            if (LexicalEnvironment.TryGetIdentifierEnvironmentWithBindingValue(
                env,
                name,
                strict,
                out var environmentRecord,
                out _))
            {
                environmentRecord.SetMutableBinding(name, rval, strict);
            }
            else
            {
                if (checkReference && strict)
                {
                    ExceptionHelper.ThrowReferenceError<Reference>(engine);
                }
                env._record.CreateMutableBinding(name, rval);
            }
        }
    }
}