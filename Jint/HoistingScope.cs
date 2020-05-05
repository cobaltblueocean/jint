using System.Collections.Generic;
using Esprima.Ast;

namespace Jint
{
    internal readonly struct HoistingScope
    {
        internal readonly List<FunctionDeclaration> _functionDeclarations;
        
        internal readonly List<VariableDeclaration> _variablesDeclarations;
        internal readonly List<string> _varNames;

        internal readonly List<VariableDeclaration> _lexicalDeclarations;
        internal readonly List<string> _lexicalNames;

        private HoistingScope(
            List<FunctionDeclaration> functionDeclarations,
            List<string> varNames,
            List<VariableDeclaration> variableDeclarations,
            List<VariableDeclaration> lexicalDeclarations,
            List<string> lexicalNames)
        {
            _functionDeclarations = functionDeclarations;
            _varNames = varNames;
            _variablesDeclarations = variableDeclarations;
            _lexicalDeclarations = lexicalDeclarations;
            _lexicalNames = lexicalNames;
        }

        public static HoistingScope GetProgramLevelDeclarations(Node node, bool collectVarNames = false, bool collectLexicalNames = false)
        {
            var treeWalker = new ScriptWalker(collectVarNames, collectLexicalNames);
            treeWalker.Visit(node, true);
            return new HoistingScope(
                treeWalker._functions,
                treeWalker._varNames,
                treeWalker._variableDeclarations,
                treeWalker._lexicalDeclarations,
                treeWalker._lexicalNames);
        }
        
        public static HoistingScope GetFunctionLevelDeclarations(IFunction node, bool collectVarNames = false, bool collectLexicalNames = false)
        {
            var treeWalker = new ScriptWalker(collectVarNames, collectLexicalNames);
            treeWalker.Visit(node.Body, true);
            
            return new HoistingScope(
                treeWalker._functions,
                treeWalker._varNames,
                treeWalker._variableDeclarations,
                treeWalker._lexicalDeclarations,
                treeWalker._lexicalNames);
        }

        public static List<VariableDeclaration> GetLexicalDeclarations(BlockStatement statement)
        {
            List<VariableDeclaration> lexicalDeclarations = null ;
            ref readonly var statementListItems = ref statement.Body;
            for (var i = 0; i < statementListItems.Count; i++)
            {
                var node = statementListItems[i];
                if (node is VariableDeclaration rootVariable)
                {
                    if (rootVariable.Kind != VariableDeclarationKind.Var)
                    {
                        lexicalDeclarations ??= new List<VariableDeclaration>();
                        lexicalDeclarations.Add(rootVariable);
                    }
                }
            }
            
            return lexicalDeclarations;
        }

        public static List<VariableDeclaration> GetLexicalDeclarations(SwitchCase statement)
        {
            List<VariableDeclaration> lexicalDeclarations = null ;
            ref readonly var statementListItems = ref statement.Consequent;
            for (var i = 0; i < statementListItems.Count; i++)
            {
                var node = statementListItems[i];
                if (node is VariableDeclaration rootVariable)
                {
                    if (rootVariable.Kind != VariableDeclarationKind.Var)
                    {
                        lexicalDeclarations ??= new List<VariableDeclaration>();
                        lexicalDeclarations.Add(rootVariable);
                    }
                }
            }

            return lexicalDeclarations;
        }

        private sealed class ScriptWalker
        {
            internal List<FunctionDeclaration> _functions;

            private readonly bool _collectVarNames;
            internal List<VariableDeclaration> _variableDeclarations;
            internal List<string> _varNames;

            private readonly bool _collectLexicalNames;
            internal List<VariableDeclaration> _lexicalDeclarations;
            internal List<string> _lexicalNames;

            public ScriptWalker(bool collectVarNames, bool collectLexicalNames)
            {
                _collectVarNames = collectVarNames;
                _collectLexicalNames = collectLexicalNames;
            }

            public void Visit(Node node, bool root)
            {
                foreach (var childNode in node.ChildNodes)
                {
                    if (childNode is null)
                    {
                        // array expression can push null nodes in Esprima
                        continue;
                    }

                    if (childNode is VariableDeclaration variableDeclaration)
                    {
                        if (variableDeclaration.Kind == VariableDeclarationKind.Var)
                        {
                            _variableDeclarations ??= new List<VariableDeclaration>();
                            _variableDeclarations.Add(variableDeclaration);
                            if (_collectVarNames)
                            {
                                _varNames ??= new List<string>();
                                ref readonly var nodeList = ref variableDeclaration.Declarations;
                                foreach (var declaration in nodeList)
                                {
                                    if (declaration.Id is Identifier identifier)
                                    {
                                        _varNames.Add(identifier.Name);
                                    }
                                }
                            }
                        }

                        if (root && variableDeclaration.Kind != VariableDeclarationKind.Var)
                        {
                            _lexicalDeclarations ??= new List<VariableDeclaration>();
                            _lexicalDeclarations.Add(variableDeclaration);
                            if (_collectLexicalNames)
                            {
                                _lexicalNames ??= new List<string>();
                                ref readonly var nodeList = ref variableDeclaration.Declarations;
                                foreach (var declaration in nodeList)
                                {
                                    if (declaration.Id is Identifier identifier)
                                    {
                                        _lexicalNames.Add(identifier.Name);
                                    }
                                }
                            }
                        }
                    }
                    else if (childNode is FunctionDeclaration functionDeclaration)
                    {
                        _functions ??= new List<FunctionDeclaration>();
                        _functions.Add(functionDeclaration);
                    }

                    if (childNode.Type != Nodes.FunctionDeclaration
                        && childNode.Type != Nodes.ArrowFunctionExpression
                        && childNode.Type != Nodes.ArrowParameterPlaceHolder
                        && childNode.Type != Nodes.FunctionExpression
                        && childNode.ChildNodes.Count > 0)
                    {
                        Visit(childNode, false);
                    }
                }
            }
        }
    }
}