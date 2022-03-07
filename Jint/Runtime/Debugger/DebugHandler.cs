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

    public class DebugEvaluationException : Exception
    {
        public DebugEvaluationException(string message) : base(message)
        {
        }

        public DebugEvaluationException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class DebugHandler
    {
        public delegate StepMode DebugStepDelegate(object sender, DebugInformation e);
        public delegate StepMode BreakDelegate(object sender, DebugInformation e);

        private readonly Engine _engine;
        private bool _paused;
        private int _steppingDepth;

        public event DebugStepDelegate Step;
        public event BreakDelegate Break;

        internal DebugHandler(Engine engine)
        {
            _engine = engine;
            _steppingDepth = int.MaxValue;
        }

        public BreakPointCollection BreakPoints { get; } = new BreakPointCollection();

        public JsValue Evaluate(string source, ParserOptions options = null)
        {
            // TODO: Default options should probably be retrieved from engine
            options ??= new ParserOptions("evaluation") { AdaptRegexp = true, Tolerant = true };
            var parser = new JavaScriptParser(source, options);
            var script = parser.ParseScript();

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
                throw new DebugEvaluationException($"Evaluation of debug expression threw an Error: {result.GetValueOrDefault()}");
            }

            return result.GetValueOrDefault();
        }

        internal void OnStep(Statement statement)
        {
            // Don't reenter if we're already paused (e.g. when evaluating a getter in a Break/Step handler)
            if (_paused)
            {
                return;
            }

            BreakPoint breakpoint = BreakPoints.FindMatch(this, new BreakLocation(statement.Location.Source, statement.Location.Start));

            if (breakpoint != null)
            {
                Pause(PauseType.Break, statement);
            }
            else if (_engine.CallStack.Count <= _steppingDepth)
            {
                Pause(PauseType.Step, statement);
            }
        }

        internal void OnReturnPoint(Node functionBody, JsValue returnValue)
        {
            // Don't reenter if we're already paused (e.g. when evaluating a getter in a Break/Step handler)
            if (_paused)
            {
                return;
            }

            var bodyLocation = functionBody.Location;
            var functionBodyEnd = bodyLocation.End;
            var location = new Location(functionBodyEnd, functionBodyEnd, bodyLocation.Source);

            BreakPoint breakpoint = BreakPoints.FindMatch(this, new BreakLocation(bodyLocation.Source, bodyLocation.End));

            if (breakpoint != null)
            {
                Pause(PauseType.Break, statement: null, location, returnValue);
            }
            else if (_engine.CallStack.Count <= _steppingDepth)
            {
                Pause(PauseType.Step, statement: null, location, returnValue);
            }
        }

        internal void OnDebuggerStatement(Statement statement)
        {
            // Don't reenter if we're already paused
            if (_paused)
            {
                return;
            }

            Pause(PauseType.DebuggerStatement, statement);
        }

        private void Pause(PauseType type, Statement statement = null, Location? location = null, JsValue returnValue = null)
        {
            _paused = true;
            
            DebugInformation info = CreateDebugInformation(statement, location ?? statement.Location, returnValue, type);
            
            StepMode? result = type switch
            {
                // Conventionally, sender should be DebugHandler - but Engine is more useful
                PauseType.Step => Step?.Invoke(_engine, info),
                PauseType.Break => Break?.Invoke(_engine, info),
                PauseType.DebuggerStatement => Break?.Invoke(_engine, info),
                _ => throw new ArgumentException("Invalid pause type", nameof(type))
            };
            
            _paused = false;
            
            HandleNewStepMode(result);
        }

        private void HandleNewStepMode(StepMode? newStepMode)
        {
            if (newStepMode != null)
            {
                _steppingDepth = newStepMode switch
                {
                    StepMode.Over => _engine.CallStack.Count,// Resume stepping when we're back at this level of the call stack
                    StepMode.Out => _engine.CallStack.Count - 1,// Resume stepping when we've popped the call stack
                    StepMode.None => int.MinValue,// Never step
                    _ => int.MaxValue,// Always step
                };
            }
        }

        private DebugInformation CreateDebugInformation(Statement statement, Location? currentLocation, JsValue returnValue, PauseType pauseType)
        {
            return new DebugInformation(
                statement,
                new DebugCallStack(_engine, currentLocation ?? statement.Location, _engine.CallStack, returnValue),
                _engine.CurrentMemoryUsage,
                pauseType
            );
        }
    }
}
