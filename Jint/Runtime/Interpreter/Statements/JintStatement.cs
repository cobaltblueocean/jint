﻿using Esprima;
using Esprima.Ast;

namespace Jint.Runtime.Interpreter.Statements
{
    internal abstract class JintStatement<T> : JintStatement where T : Statement
    {
        protected readonly Engine _engine;
        protected readonly T _statement;

        protected JintStatement(Engine engine, T statement)
        {
            _engine = engine;
            _statement = statement;
        }

        public override Location Location => _statement.Location;
    }

    internal abstract class JintStatement
    {
        public abstract Location Location { get; }

        public abstract Completion Execute();

        public static JintStatement Build(Engine engine, IFunction function)
        {
            if (function is Statement s)
            {
                return Build(engine, s);
            }

            if (function is Expression e)
            {
                return Build(engine, new ExpressionStatement(e));
            }

            return ExceptionHelper.ThrowArgumentOutOfRangeException<JintStatement>();
        }

        protected internal static JintStatement Build(Engine engine, Statement statement)
        {
            switch (statement.Type)
            {
                case Nodes.BlockStatement:
                    var statementListItems = ((BlockStatement) statement).Body;
                    return new JintBlockStatement(engine, new JintStatementList(engine, statementListItems));

                case Nodes.ReturnStatement:
                    return new JintReturnStatement(engine, (ReturnStatement) statement);

                case Nodes.VariableDeclaration:
                    return new JintVariableDeclaration(engine, (VariableDeclaration) statement);

                case Nodes.BreakStatement:
                    return new JintBreakStatement(engine, (BreakStatement) statement);

                case Nodes.ContinueStatement:
                    return new JintContinueStatement(engine, (ContinueStatement) statement);

                case Nodes.DoWhileStatement:
                    return new JintDoWhileStatement(engine, (DoWhileStatement) statement);

                case Nodes.EmptyStatement:
                    return new JintEmptyStatement(engine, (EmptyStatement) statement);

                case Nodes.ExpressionStatement:
                    return new JintExpressionStatement(engine, (ExpressionStatement) statement);

                case Nodes.ForStatement:
                    return new JintForStatement(engine, (ForStatement) statement);

                case Nodes.ForInStatement:
                    return new JintForInStatement(engine, (ForInStatement) statement);

                case Nodes.IfStatement:
                    return new JintIfStatement(engine, (IfStatement) statement);

                case Nodes.LabeledStatement:
                    return new JintLabeledStatement(engine, (LabeledStatement) statement);

                case Nodes.SwitchStatement:
                    return new JintSwitchStatement(engine, (SwitchStatement) statement);

                case Nodes.FunctionDeclaration:
                    return new JintFunctionDeclarationStatement(engine, (FunctionDeclaration) statement);

                case Nodes.ThrowStatement:
                    return new JintThrowStatement(engine, (ThrowStatement) statement);

                case Nodes.TryStatement:
                    return new JintTryStatement(engine, (TryStatement) statement);

                case Nodes.WhileStatement:
                    return new JintWhileStatement(engine, (WhileStatement) statement);

                case Nodes.WithStatement:
                    return new JintWithStatement(engine, (WithStatement) statement);

                case Nodes.DebuggerStatement:
                    return new JintDebuggerStatement(engine, (DebuggerStatement) statement);

                case Nodes.Program:
                    return new JintProgram(engine, (Program) statement);

                default:
                    return ExceptionHelper.ThrowArgumentOutOfRangeException<JintStatement>();
            }
        }
    }
}