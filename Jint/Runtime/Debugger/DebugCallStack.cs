﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Esprima;
using Esprima.Ast;
using Jint.Native;
using Jint.Runtime.CallStack;
using Jint.Runtime.Environments;

namespace Jint.Runtime.Debugger
{
    public class CallFrame
    {
        private readonly ExecutionContext _context;
        private readonly CallStackElement? _element;
        private readonly Lazy<DebugScopes> _scopeChain;

        private LexicalEnvironment Environment => _context.LexicalEnvironment;

        // TODO: CallFrameId
        // e.g. Chromium uses "(anonymous)" for call from global scope - we do the same:
        public string FunctionName => _element?.ToString() ?? "(anonymous)";
        public Location? FunctionLocation => _element?.Function._functionDefinition?.Function.Body.Location;
        // Location includes both "location" and "url" for Chrome Devtools Protocol
        public Location Location { get; }
        public DebugScopes ScopeChain => _scopeChain.Value;
        public JsValue This => GetThis();
        public JsValue ReturnValue { get; }

        public NodeList<Expression>? Arguments { get; }

        internal CallFrame(CallStackElement? element, ExecutionContext context, Location location, JsValue returnValue)
        {
            _element = element;
            _context = context;
            Location = location;
            ReturnValue = returnValue;

            _scopeChain = new Lazy<DebugScopes>(() => new DebugScopes(Environment));
        }

        private JsValue GetThis()
        {
            var environment = Environment;

            while (environment?._record != null)
            {
                if (environment._record.HasThisBinding())
                {
                    return environment._record.GetThisBinding();
                }
                environment = environment._outer;
            }

            return null;
        }
    }

    public class DebugCallStack : IReadOnlyList<CallFrame>
    {
        private List<CallFrame> _stack;
        public CallFrame this[int index] => _stack[index];

        public int Count => _stack.Count;

        internal DebugCallStack(Engine engine, Location location, JintCallStack callStack, JsValue returnValue)
        {
            _stack = new List<CallFrame>();
            var executionContext = engine.ExecutionContext;
            foreach (var element in callStack.Stack)
            {
                _stack.Add(new CallFrame(element, executionContext, location, returnValue));
                location = element.Location;
                returnValue = null;
                executionContext = element.CallingExecutionContext;
            }
            // Add root location
            _stack.Add(new CallFrame(null, executionContext, location, returnValue: null));
        }

        public IEnumerator<CallFrame> GetEnumerator()
        {
            return _stack.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
