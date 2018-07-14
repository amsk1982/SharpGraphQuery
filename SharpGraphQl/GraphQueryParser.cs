using System;
using System.Collections.Generic;
using System.ComponentModel.Design;

namespace SharpGraphQl
{
    public class GraphQueryParser 
    {
        private readonly IGraphQueryLexer _lexer;
        private const int MaxSelectionSetDepth = 100;

        public GraphQueryParser(IGraphQueryLexer graphQueryLexer)
        {
            _lexer = graphQueryLexer;
        }

        public RootNode Parse()
        {
            RootNode node = new RootNode();
            if (!NextToken())
                return node;

            while (_lexer.TokenType != TokenType.EOF)
            {
                switch(_lexer.TokenType)
                {
                    case TokenType.Name:
                        node.Definitions.Add(ParseExecutableDefinition());
                        break;

                    case TokenType.OpenBrace:
                        node.Definitions.Add(ParseAnonomousDefinition());
                        break;

                    default:
                        throw UnexpectedToken(TokenType.Name, TokenType.OpenBrace);
                }
            }

            return node;
        }

        private IDefinitionNode ParseExecutableDefinition()
        {
            if (_lexer.TokenType != TokenType.Name)
                throw new InvalidOperationException("Executable definition should start with a name");

            string name = _lexer.StringValue;
            if (string.Equals(name, "fragment", StringComparison.OrdinalIgnoreCase))
                return ParseFragmentDefinition();

            return ParseOperationDefinition();
        }

        private FragmentDefinitionNode ParseFragmentDefinition()
        {
            NextTokenRequired();
            if (_lexer.TokenType != TokenType.Name)
                UnexpectedToken(TokenType.Name);
            string name = _lexer.StringValue;
            NextTokenRequired();

            if (_lexer.TokenType != TokenType.Name)
                UnexpectedToken(TokenType.Name);
            if (!string.Equals(_lexer.StringValue, "on", StringComparison.Ordinal))
                throw ParseError("Expeced 'on' got: " + _lexer.StringValue);
            NextTokenRequired();

            if (_lexer.TokenType != TokenType.Name)
                UnexpectedToken(TokenType.Name);
            string typeName = _lexer.StringValue;
            NextTokenRequired();

            List<DirectiveNode> directives = null;
            if (_lexer.TokenType == TokenType.AtSign)
            {
                directives = ParseDirectives(1);
            }

            if (_lexer.TokenType != TokenType.OpenBrace)
                throw UnexpectedToken(TokenType.OpenBrace, TokenType.AtSign);

            SelectionSetNode selectionSet = ParseSelectionSet(1);

            return new FragmentDefinitionNode
            {
                Name = name,
                OnType = typeName,
                Directives = directives ?? new List<DirectiveNode>(),
                SelectionSet = selectionSet

            };
        }

        private OperationDefinitionNode ParseOperationDefinition()
        {
            OperationType operationType = GetOperationType(_lexer.StringValue);
            string operationName = null;
            List<VariableDefinitionNode> variablesList = null;
            List<DirectiveNode> directives = null;

            NextTokenRequired();
            if (_lexer.TokenType == TokenType.Name)
            {
                operationName = _lexer.StringValue;
                NextTokenRequired();
            }

            if (_lexer.TokenType == TokenType.OpenParen)
            {
                variablesList = ParseVariables(1);
            }

            if (_lexer.TokenType == TokenType.AtSign)
            {
                directives = ParseDirectives(1);
            }

            if (_lexer.TokenType != TokenType.OpenBrace)
                throw UnexpectedToken(TokenType.OpenBrace);

            SelectionSetNode selectionSetNode = ParseSelectionSet(1);

            OperationDefinitionNode node = new OperationDefinitionNode
            {
                OperationType = operationType,
                SelectionSet = selectionSetNode,
                Name = operationName,
                Directives = directives ?? new List<DirectiveNode>(),
                VariableDefinitions = variablesList ?? new List<VariableDefinitionNode>()
            };

            return node;
        }

        private List<DirectiveNode> ParseDirectives(int depth)
        {
            if (_lexer.TokenType != TokenType.AtSign)
                throw new InvalidOperationException("Expected AtSign");

            List<DirectiveNode> directives = new List<DirectiveNode>();
            do
            {
                NextTokenRequired();
                if (_lexer.TokenType != TokenType.Name)
                    throw UnexpectedToken(TokenType.Name);

                string directiveName = _lexer.StringValue;
                List<ArgumentNode> argumentNodes = null;

                if (NextToken() && _lexer.TokenType == TokenType.OpenParen)
                {
                    argumentNodes = ParseArguments(depth);
                }

                DirectiveNode directive = new DirectiveNode
                {
                    Name = directiveName,
                    Arguments = argumentNodes ?? new List<ArgumentNode>()
                };

                directives.Add(directive);

            } while(_lexer.TokenType == TokenType.AtSign);

            return directives;
        }

        private List<VariableDefinitionNode> ParseVariables(int depth)
        {
            if (_lexer.TokenType != TokenType.OpenParen)
                throw new InvalidOperationException("Variables should start at open paren");

            NextTokenRequired();

            List<VariableDefinitionNode> variableNodes = new List<VariableDefinitionNode>();
            while(_lexer.TokenType != TokenType.CloseParen)
            {
                if (_lexer.TokenType != TokenType.Dollar)
                    throw UnexpectedToken(TokenType.Dollar, TokenType.CloseParen);

                NextTokenRequired();
                if (_lexer.TokenType != TokenType.Name)
                    throw UnexpectedToken(TokenType.Name);

                string name = _lexer.StringValue;

                NextTokenRequired();
                if (_lexer.TokenType != TokenType.Colon)
                    throw UnexpectedToken(TokenType.Colon);

                NextTokenRequired();
                ITypeNode typeNode = ParseType(depth);

                IValueNode defaultValue = null;
                if (_lexer.TokenType == TokenType.Eq)
                    defaultValue = ParseValue(depth);

                VariableDefinitionNode node = new VariableDefinitionNode()
                {
                    Name = name,
                    Type = typeNode,
                    DefaultValue = defaultValue
                };

                variableNodes.Add(node);
            }

            NextTokenRequired();
            return variableNodes;
        }

        private ITypeNode ParseType(int depth)
        {
            if (depth > MaxSelectionSetDepth)
                throw ParseError("Max recursion depth exceeded");

            var nullableType = ParseNullableType(depth);
            if (_lexer.TokenType == TokenType.Bang)
            {
                NextTokenRequired();
                return new NotNullTypeNode(nullableType);
            }

            return nullableType;
        }

        private ITypeNode ParseNullableType(int depth)
        {
            switch(_lexer.TokenType)
            {
                case TokenType.Name:
                    return ParseNamedType();

                case TokenType.OpenBracket:
                    return ParseListType(depth);

                default:
                    throw UnexpectedToken(TokenType.Name, TokenType.OpenBracket);
            }
        }

        private ITypeNode ParseNamedType()
        {
            var namedType = new NamedTypeNode(_lexer.StringValue);
            NextTokenRequired();
            return namedType;
        }

        private ITypeNode ParseListType(int depth)
        {
            var listType = new ListTypeNode
            {
                Inner = ParseType(depth + 1)
            };

            if (_lexer.TokenType != TokenType.CloseBracket)
                throw UnexpectedToken(TokenType.CloseBracket);
            NextTokenRequired();

            return listType;
        }

        private OperationType GetOperationType(string name)
        {
            if (string.Equals(name, "query", StringComparison.Ordinal))
                return OperationType.Query;
            if (string.Equals(name, "mutation", StringComparison.Ordinal))
                return OperationType.Mutation;
            if (string.Equals(name, "subscription", StringComparison.Ordinal))
                return OperationType.Subscription;

            throw ParseError("Invalid operation type: " + name);
        }

        private OperationDefinitionNode ParseAnonomousDefinition()
        {
            OperationDefinitionNode node = new OperationDefinitionNode
            {
                OperationType = OperationType.Query,
                SelectionSet = ParseSelectionSet(1),
                Directives = new List<DirectiveNode>(),
                VariableDefinitions = new List<VariableDefinitionNode>()
            };

            return node;
        }

        private SelectionSetNode ParseSelectionSet(int depth)
        {
            if (depth > MaxSelectionSetDepth)
                throw ParseError("Cannot parse selection sets more than " + MaxSelectionSetDepth + " levels deep");
            if (_lexer.TokenType != TokenType.OpenBrace)
                throw new InvalidOperationException("Selection set must start with opening brace");

            NextTokenRequired();
            SelectionSetNode selectionSetNode = new SelectionSetNode();
            selectionSetNode.Items = new List<ISelectionItemNode>();

            while(true)
            {
                switch(_lexer.TokenType)
                {
                    case TokenType.Name:
                        var fieldNode = ParseField(depth);
                        selectionSetNode.Items.Add(fieldNode);
                        break;

                    case TokenType.Ellipsis:
                        var fragment = ParseFragmentInSelectionSet(depth);
                        selectionSetNode.Items.Add(fragment);
                        break;

                    case TokenType.CloseBrace:
                        NextToken();
                        return selectionSetNode;

                    default:
                        throw UnexpectedToken(TokenType.CloseBrace, TokenType.Name, TokenType.Ellipsis);
                }
            } 
        }

        private FieldNode ParseField(int depth)
        {
            if (_lexer.TokenType != TokenType.Name)
                throw new InvalidOperationException("Field should start with a name");

            string firstName = _lexer.StringValue;
            NextTokenRequired();
            
            string name;
            string alias;
            List<ArgumentNode> arguments = null;
            List<DirectiveNode> directives = null;
            SelectionSetNode selectionSet = null;

            if (_lexer.TokenType == TokenType.Colon)
            {
                NextTokenRequired();
                if (_lexer.TokenType != TokenType.Name)
                    throw UnexpectedToken(TokenType.Name);
                
                alias = firstName;
                name = _lexer.StringValue;
                NextTokenRequired();
            }
            else
            {
                alias = null;
                name = firstName;
            }

            if (_lexer.TokenType == TokenType.OpenParen)
            {
                arguments = ParseArguments(depth);
            }

            if (_lexer.TokenType == TokenType.AtSign)
            {
                directives = ParseDirectives(depth);
            }

            if (_lexer.TokenType == TokenType.OpenBrace)
            {
                selectionSet = ParseSelectionSet(depth + 1);
            }

            return new FieldNode
            {
                Alias = alias,
                Name = name,
                Arguments = arguments,
                Directives = directives,
                SelectionSet = selectionSet
            };
        }

        private List<ArgumentNode> ParseArguments(int depth)
        {
            if (_lexer.TokenType != TokenType.OpenParen)
                throw new InvalidOperationException("Arguments should start with a OpenParen");

            List<ArgumentNode> argumentNodes = new List<ArgumentNode>();

            NextTokenRequired();
            while(_lexer.TokenType != TokenType.CloseParen)
            {
                if (_lexer.TokenType != TokenType.Name)
                    throw UnexpectedToken(TokenType.Name);

                string name = _lexer.StringValue;

                NextTokenRequired();
                if (_lexer.TokenType != TokenType.Colon)
                    throw UnexpectedToken(TokenType.Colon);

                NextTokenRequired();
                var value = ParseValue(depth);

                ArgumentNode node = new ArgumentNode
                {
                    Name = name,
                    Value = value
                };
                argumentNodes.Add(node);
            }

            NextTokenRequired();
            return argumentNodes;
        }

        private IValueNode ParseValue(int depth)
        {
            if (depth > MaxSelectionSetDepth)
                throw ParseError("Cannot parse values more than " + MaxSelectionSetDepth + " levels deep");

            switch(_lexer.TokenType)
            {
                case TokenType.Dollar:
                    return ParseVariable();
                case TokenType.IntValue:
                    int intValue = _lexer.IntValue ??
                                throw new InvalidOperationException("Tokenizer should have an int value");
                    NextTokenRequired();
                    return new IntValueNode() { Value = intValue };

                case TokenType.FloatValue:
                    double floatValue = _lexer.DoubleValue ??
                                        throw new InvalidOperationException("Tokenizer should have an float value");
                    NextTokenRequired();
                    return new FloatValueNode()
                    {
                        Value = floatValue
                    };

                case TokenType.StringValue:
                    string stringValue = _lexer.StringValue;
                    NextTokenRequired();
                    return new StringValueNode()
                    {
                        Value = stringValue
                    };

                case TokenType.Name:
                    return ParseNameValue();

                case TokenType.OpenBracket:
                    return ParseListValue(depth);

                case TokenType.OpenBrace:
                    return ParseObjectValue(depth);

                default:
                    throw UnexpectedToken(TokenType.Dollar, TokenType.IntValue, TokenType.FloatValue, TokenType.StringValue, TokenType.Name,
                        TokenType.OpenBracket, TokenType.OpenBrace);
            }
        }

        private IValueNode ParseObjectValue(int depth)
        {
            if (_lexer.TokenType != TokenType.OpenBrace)
                throw new InvalidOperationException("Object should start with open brace");

            NextTokenRequired();
            List<ObjectValueFieldNode> objectFields = new List<ObjectValueFieldNode>();
            while(_lexer.TokenType != TokenType.CloseBrace)
            {
                if (_lexer.TokenType != TokenType.Name)
                    throw UnexpectedToken(TokenType.Name, TokenType.CloseBrace);

                string name = _lexer.StringValue;
                NextTokenRequired();
                IValueNode value = ParseValue(depth + 1);

                ObjectValueFieldNode fieldNode = new ObjectValueFieldNode()
                {
                    Name = name,
                    Value = value
                };

                objectFields.Add(fieldNode);
            }

            NextTokenRequired();
            return new ObjectValueNode(objectFields);
        }

        private IValueNode ParseListValue(int depth)
        {
            if (_lexer.TokenType != TokenType.OpenBracket)
                throw new InvalidOperationException("List should start with open bracket");

            NextTokenRequired();

            List<IValueNode> listContents = new List<IValueNode>();
            while(_lexer.TokenType != TokenType.CloseBracket)
            {
                IValueNode value = ParseValue(depth + 1);
                listContents.Add(value);
            }

            NextTokenRequired();
            return new ListValueNode(listContents);
        }

        private IValueNode ParseVariable()
        {
            if (_lexer.TokenType != TokenType.Dollar)
                throw new InvalidOperationException("Fragment should start with dollar");

            NextTokenRequired();
            if (_lexer.TokenType != TokenType.Name)
                throw UnexpectedToken(TokenType.Name);

            VariableValueNode variable = new VariableValueNode(_lexer.StringValue);
            NextTokenRequired();
            return variable;
        }

        private IValueNode ParseNameValue()
        {
            string name = _lexer.StringValue;
            if (string.Equals(name, "true", StringComparison.Ordinal))
                return new BooleanValueNode() { Value = true };
            if (string.Equals(name, "false", StringComparison.Ordinal))
                return new BooleanValueNode() { Value = false };
            if (string.Equals(name, "null", StringComparison.Ordinal))
                return new NullValueNode();
 
            NextTokenRequired();
            return new EnumValueNode(name);
        }

        private ISelectionItemNode ParseFragmentInSelectionSet(int depth)
        {
            if (_lexer.TokenType != TokenType.Ellipsis)
                throw new InvalidOperationException("Fragment should start with ellipsis");

            NextTokenRequired();
            switch (_lexer.TokenType)
            {
                case TokenType.Name:
                    if (_lexer.StringValue.Equals("on", StringComparison.Ordinal))
                        return ParseInlineFragmentWithTypeCondition(depth);

                    return ParseFragmentSpread(depth);

                case TokenType.OpenBrace:
                    return ParseInlineFragment(depth);

                default:
                    throw UnexpectedToken(TokenType.Name, TokenType.OpenBrace);
            }
        }

        private ISelectionItemNode ParseInlineFragmentWithTypeCondition(int depth)
        {
            if (_lexer.TokenType != TokenType.Name)
                throw new InvalidOperationException("Expected a name");

            NextTokenRequired();
            if (_lexer.TokenType != TokenType.Name)
                throw UnexpectedToken(TokenType.Name);

            string typeName = _lexer.StringValue;
            List<DirectiveNode> directives = null;

            NextTokenRequired();
            if (_lexer.TokenType == TokenType.AtSign)
            {
                directives = ParseDirectives(depth);
            }

            if (_lexer.TokenType != TokenType.OpenBrace)
            {
                throw UnexpectedToken(TokenType.OpenBrace);
            }

            SelectionSetNode selectionSet = ParseSelectionSet(depth + 1);
            
            return new InlineFragmentNode
            {
                TypeCondition = typeName,
                Directives = directives ?? new List<DirectiveNode>(),
                SelectionSet = selectionSet
            };
        }

        private ISelectionItemNode ParseInlineFragment(int depth)
        {
            if (_lexer.TokenType != TokenType.OpenBrace)
                throw new InvalidOperationException("Expected a OpenBrace");

            SelectionSetNode selectionSet = ParseSelectionSet(depth + 1);
            return new InlineFragmentNode
            {
                TypeCondition = null,
                Directives = new List<DirectiveNode>(),
                SelectionSet = selectionSet
            };
        }

        private ISelectionItemNode ParseFragmentSpread(int depth)
        {
            if (_lexer.TokenType != TokenType.Name)
                throw new InvalidOperationException("Expected a name");

            string name = _lexer.StringValue;
            List<DirectiveNode> directiveNodes = null;
            if (NextToken())
            {
                if (_lexer.TokenType == TokenType.AtSign)
                    directiveNodes = ParseDirectives(depth);
            }

            return new FragmentSpreadNode
            {
                Name = name,
                Directives = directiveNodes ?? new List<DirectiveNode>()
            };
        }


        private Exception ParseError(string message)
        {
            throw new GraphQueryParseException(message);
        }

        private Exception UnexpectedToken(params TokenType[] tokensExpected)
        {
            throw new GraphQueryParseException("Invalid token '" + _lexer.TokenType
                                                   + "'. Expected: " + string.Join(", ", tokensExpected));
        }

        private void NextTokenRequired()
        {
            if (!NextToken())
                throw ParseError("Unexpected end of document");
        }

        private bool NextToken()
        {
            while (true)
            {
                bool ok = _lexer.Next();
                if (!ok)
                    return false;

                var token = _lexer.TokenType;
                switch (token)
                {
                    case TokenType.Whitespace: 
                    case TokenType.Comma:
                    case TokenType.LineTerminator:
                    case TokenType.Comment:
                        break;

                    default: 
                        return true;
                }
            }
        }
    }

    public class GraphQueryParseException : Exception
    {
        public GraphQueryParseException(string message)
            : base(message)
        {}
    }
}