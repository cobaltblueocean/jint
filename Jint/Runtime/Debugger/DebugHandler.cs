using System;
using Esprima;
using Esprima.Ast;
using Jint.Native;
using Jint.Runtime.Interpreter;

namespace Jint.Runtime.Debugger
{
    public enum PauseType
    {
        Step,
        Break,
        DebuggerStatement
    }

    public class DebugHandler
    {
        public delegate StepMode DebugEventHandler(object sender, DebugInformation e);

        private readonly Engine _engine;
        private bool _paused;
        private int _steppingDepth;

        /// <summary>
        /// The Step event is triggered before the engine executes a step-eligible AST node.
        /// </summary>
        /// <remarks>
        /// If the current step mode is <see cref="StepMode.None"/>, this event is never triggered. The script may
        /// still be paused by a debugger statement or breakpoint, but these will trigger the
        /// <see cref="Break"/> event.
        /// </remarks>
        public event DebugEventHandler Step;

        /// <summary>
        /// The Break event is triggered when a breakpoint or debugger statement is hit.
        /// </summary>
        /// <remarks>
        /// This is event is not triggered if the current script location was reached by stepping. In that case, only
        /// the <see cref="Step"/> event is triggered.
        /// </remarks>
        public event DebugEventHandler Break;

        internal DebugHandler(Engine engine, StepMode initialStepMode)
        {
            _engine = engine;
            HandleNewStepMode(initialStepMode);
        }

        /// <summary>
        /// The location of the current (step-eligible) AST node being executed.
        /// </summary>
        /// <remarks>
        /// The location is available as long as DebugMode is enabled - i.e. even when not stepping
        /// or hitting a breakpoint.
        /// </remarks>
        public Location? CurrentLocation { get; private set; }

        /// <summary>
        /// Collection of active breakpoints for the engine.
        /// </summary>
        public BreakPointCollection BreakPoints { get; } = new BreakPointCollection();

        /// <summary>
        /// Evaluates a script (expression) within the current execution context.
        /// </summary>
        /// <remarks>
        /// Internally, this is used for evaluating breakpoint conditions, but may also be used for e.g. watch lists
        /// in a debugger.
        /// </remarks>
        public JsValue Evaluate(Script script)
        {
            int callStackSize = _engine.CallStack.Count;

            var list = new JintStatementList(null, script.Body);
            Completion result;
            try
            {
                result = list.Execute(_engine._activeEvaluationContext);
            }
            catch (Exception ex)
            {
                throw new DebugEvaluationException("An error occurred during debug expression evaluation", ex);
            }
            finally
            {
                // Restore call stack
                while (_engine.CallStack.Count > callStackSize)
                {
                    _engine.CallStack.Pop();
                }
            }

            if (result.Type == CompletionType.Throw)
            {
                // TODO: Should we return an error here? (avoid exception overhead, since e.g. breakpoint
                // evaluation may be high volume.
                var error = result.GetValueOrDefault();
                throw new DebugEvaluationException(error.ToString());
            }

            return result.GetValueOrDefault();
        }

        /// <inheritdoc cref="Evaluate(Script)" />
        public JsValue Evaluate(string source, ParserOptions options = null)
        {
            // TODO: Default options should probably be retrieved from engine
            options ??= new ParserOptions("evaluation") { AdaptRegexp = true, Tolerant = true };
            var parser = new JavaScriptParser(source, options);
            var script = parser.ParseScript();
            return Evaluate(script);
        }

        internal void OnStep(Node node)
        {
            // Don't reenter if we're already paused (e.g. when evaluating a getter in a Break/Step handler)
            if (_paused)
            {
                return;
            }
            _paused = true;

            CheckBreakPointAndPause(
                new BreakLocation(node.Location.Source, node.Location.Start), 
                node: node, 
                location: null, 
                returnValue: null);
        }

        internal void OnReturnPoint(Node functionBody, JsValue returnValue)
        {
            // Don't reenter if we're already paused (e.g. when evaluating a getter in a Break/Step handler)
            if (_paused)
            {
                return;
            }
            _paused = true;

            var bodyLocation = functionBody.Location;
            var functionBodyEnd = bodyLocation.End;
            var location = new Location(functionBodyEnd, functionBodyEnd, bodyLocation.Source);

            CheckBreakPointAndPause(
                new BreakLocation(bodyLocation.Source, bodyLocation.End), 
                node: null, 
                location: location, 
                returnValue: returnValue);
        }

        internal void OnDebuggerStatement(Statement statement)
        {
            // Don't reenter if we're already paused
            if (_paused)
            {
                return;
            }
            _paused = true;

            bool isStepping = _engine.CallStack.Count <= _steppingDepth;

            // Even though we're at a debugger statement, if we're stepping, ignore the statement. OnStep already
            // takes care of pausing.
            if (!isStepping)
            {
                Pause(PauseType.DebuggerStatement, statement);
            }

            _paused = false;
        }

        private void CheckBreakPointAndPause(BreakLocation breakLocation, Node node = null, Location? location = null,
            JsValue returnValue = null)
        {
            CurrentLocation = location ?? node?.Location;
            BreakPoint breakpoint = BreakPoints.FindMatch(this, breakLocation);

            bool isStepping = _engine.CallStack.Count <= _steppingDepth;

            if (breakpoint != null || isStepping)
            {
                // Even if we matched a breakpoint, if we're stepping, the reason we're pausing is the step.
                // Still, we need to include the breakpoint at this location, in case the debugger UI needs to update
                // e.g. a hit count.
                Pause(isStepping ? PauseType.Step : PauseType.Break, node, location, returnValue, breakpoint);
            }

            _paused = false;
        }

        private void Pause(PauseType type, Node node = null, Location? location = null, JsValue returnValue = null,
            BreakPoint breakPoint = null)
        {
            var info = new DebugInformation(
                engine: _engine,
                currentNode: node,
                currentLocation: location ?? node.Location,
                returnValue: returnValue,
                currentMemoryUsage: _engine.CurrentMemoryUsage,
                pauseType: type,
                breakPoint: breakPoint
            );
            
            StepMode? result = type switch
            {
                // Conventionally, sender should be DebugHandler - but Engine is more useful
                PauseType.Step => Step?.Invoke(_engine, info),
                PauseType.Break => Break?.Invoke(_engine, info),
                PauseType.DebuggerStatement => Break?.Invoke(_engine, info),
                _ => throw new ArgumentException("Invalid pause type", nameof(type))
            };
            
            HandleNewStepMode(result);
        }

        private void HandleNewStepMode(StepMode? newStepMode)
        {
            if (newStepMode != null)
            {
                _steppingDepth = newStepMode switch
                {
                    StepMode.Over => _engine.CallStack.Count,// Resume stepping when back at this level of the stack
                    StepMode.Out => _engine.CallStack.Count - 1,// Resume stepping when we've popped the stack
                    StepMode.None => int.MinValue,// Never step
                    _ => int.MaxValue,// Always step
                };
            }
        }
    }
}
