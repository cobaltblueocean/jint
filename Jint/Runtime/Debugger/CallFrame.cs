﻿using System;
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
        /// <summary>
        /// Name of the function of this call frame. For global scope, this will be "(anonymous)".
        /// </summary>
        public string FunctionName => _element?.ToString() ?? "(anonymous)";

        /// <summary>
        /// Source location of function of this call frame.
        /// </summary>
        /// <remarks>For top level (global) call frames, as well as functions not defined in script, this will be null.</remarks>
        public Location? FunctionLocation => (_element?.Function._functionDefinition?.Function as Node)?.Location;

        /// <summary>
        /// Currently executing source location in this call frame.
        /// </summary>
        public Location Location { get; }

        /// <summary>
        /// The scope chain of this call frame.
        /// </summary>
        public DebugScopes ScopeChain => _scopeChain.Value;

        /// <summary>
        /// The value of <c>this</c> in this call frame.
        /// </summary>
        public JsValue This => GetThis();

        /// <summary>
        /// The return value of this call frame. Will be null for call frames that aren't at the top of the stack,
        /// as well as if execution is not at a return point.
        /// </summary>
        public JsValue ReturnValue { get; }

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
}
