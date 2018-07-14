using System;
using System.Collections.Generic;
using System.Linq;

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
                        node.Definitions.Add(ParseDefinitionWithLeadingName());
                        break;

                    case TokenType.OpenBrace:
                        node.Definitions.Add(ParseAnonomousDefinition());
                        break;

                    case TokenType.StringValue:
                        node.Definitions.Add(ParseTypeDefintionWithDocumenation());
                        break;

                    default:
                        throw BadDefinitionStart();
                }
            }

            return node;
        }

        private IDefinitionNode ParseDefinitionWithLeadingName()
        {
            string name = _lexer.StringValue;
            if (string.Equals(name, "query", StringComparison.Ordinal))
                return ParseOperationDefinition(OperationType.Query);
            if (string.Equals(name, "mutation", StringComparison.Ordinal))
                return ParseOperationDefinition(OperationType.Mutation);
            if (string.Equals(name, "subscription", StringComparison.Ordinal))
                return ParseOperationDefinition(OperationType.Subscription);
            if (string.Equals(name, "fragment", StringComparison.Ordinal))
                return ParseFragmentDefinition();
            if (string.Equals(name, "schema", StringComparison.Ordinal))
                throw ParseError("Schema definitions aren't supported");
            if (string.Equals(name, "scalar", StringComparison.Ordinal))
                throw ParseError("Scalar definitions aren't supported");
            if (string.Equals(name, "type", StringComparison.Ordinal))
                throw ParseError("Type definitions aren't supported");
            if (string.Equals(name, "interface", StringComparison.Ordinal))
                throw ParseError("Interface definitions aren't supported");
            if (string.Equals(name, "union", StringComparison.Ordinal))
                throw ParseError("Union definitions aren't supported");
            if (string.Equals(name, "enum", StringComparison.Ordinal))
                throw ParseError("Enum definitions aren't supported");
            if (string.Equals(name, "input", StringComparison.Ordinal))
                throw ParseError("Input object definitions aren't supported");
            if (string.Equals(name, "directive", StringComparison.Ordinal))
                throw ParseError("Directive definitions aren't supported");
            if (string.Equals(name, "extension", StringComparison.Ordinal))
                throw ParseError("Type system extensions aren't supported");

            throw BadDefinitionStart();
        }

        private Exception BadDefinitionStart()
        {
            return UnexpectedToken(
                new [] { "query", "mutation", "subscription", "fragment",
                         "schema", "scalar", "type", "interface", "union", "enum",
                         "input", "directive", "extension" },
                new [] {TokenType.StringValue, TokenType.OpenBrace});
        }

        private IDefinitionNode ParseTypeDefintionWithDocumenation()
        {
            string description = _lexer.StringValue;

            NextToken();
            if (_lexer.TokenType == TokenType.Name)
            {
                string name = _lexer.StringValue;

                if (string.Equals(name, "schema", StringComparison.Ordinal))
                    throw ParseError("Schema definitions aren't supported");
                if (string.Equals(name, "scalar", StringComparison.Ordinal))
                    throw ParseError("Scalar definitions aren't supported");
                if (string.Equals(name, "type", StringComparison.Ordinal))
                    throw ParseError("Type definitions aren't supported");
                if (string.Equals(name, "interface", StringComparison.Ordinal))
                    throw ParseError("Interface definitions aren't supported");
                if (string.Equals(name, "union", StringComparison.Ordinal))
                    throw ParseError("Union definitions aren't supported");
                if (string.Equals(name, "enum", StringComparison.Ordinal))
                    throw ParseError("Enum definitions aren't supported");
                if (string.Equals(name, "input", StringComparison.Ordinal))
                    throw ParseError("Input object definitions aren't supported");
                if (string.Equals(name, "directive", StringComparison.Ordinal))
                    throw ParseError("Directive definitions aren't supported");
                if (string.Equals(name, "extension", StringComparison.Ordinal))
                    throw ParseError("Type system extensions aren't supported");
            }

            throw UnexpectedToken(
                "schema", "scalar", "type", "interface", "union", "enum",
                "input", "directive", "extension"
            );
        }

        private FragmentDefinitionNode ParseFragmentDefinition()
        {
            NextToken();
            if (_lexer.TokenType != TokenType.Name)
                UnexpectedToken(TokenType.Name);
            string name = _lexer.StringValue;

            NextToken();
            if (_lexer.TokenType != TokenType.Name &&
                !string.Equals(_lexer.StringValue, "on", StringComparison.Ordinal))
                throw UnexpectedToken("on");

            NextToken();
            if (_lexer.TokenType != TokenType.Name)
                UnexpectedToken(TokenType.Name);
            string typeName = _lexer.StringValue;

            NextToken();

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

        private OperationDefinitionNode ParseOperationDefinition(OperationType operationType)
        {
            string operationName = null;
            List<VariableDefinitionNode> variablesList = null;
            List<DirectiveNode> directives = null;

            NextToken();
            if (_lexer.TokenType == TokenType.Name)
            {
                operationName = _lexer.StringValue;
                NextToken();
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
                NextToken();
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

            NextToken();

            List<VariableDefinitionNode> variableNodes = new List<VariableDefinitionNode>();
            while(_lexer.TokenType != TokenType.CloseParen)
            {
                if (_lexer.TokenType != TokenType.Dollar)
                    throw UnexpectedToken(TokenType.Dollar, TokenType.CloseParen);

                NextToken();
                if (_lexer.TokenType != TokenType.Name)
                    throw UnexpectedToken(TokenType.Name);

                string name = _lexer.StringValue;

                NextToken();
                if (_lexer.TokenType != TokenType.Colon)
                    throw UnexpectedToken(TokenType.Colon);

                NextToken();
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

            NextToken();
            return variableNodes;
        }

        private ITypeNode ParseType(int depth)
        {
            if (depth > MaxSelectionSetDepth)
                throw ParseError("Max recursion depth exceeded");

            var nullableType = ParseNullableType(depth);
            if (_lexer.TokenType == TokenType.Bang)
            {
                NextToken();
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
            NextToken();
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
            NextToken();

            return listType;
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

            NextToken();
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
            NextToken();
            
            string name;
            string alias;
            List<ArgumentNode> arguments = null;
            List<DirectiveNode> directives = null;
            SelectionSetNode selectionSet = null;

            if (_lexer.TokenType == TokenType.Colon)
            {
                NextToken();
                if (_lexer.TokenType != TokenType.Name)
                    throw UnexpectedToken(TokenType.Name);
                
                alias = firstName;
                name = _lexer.StringValue;
                NextToken();
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

            NextToken();
            while(_lexer.TokenType != TokenType.CloseParen)
            {
                if (_lexer.TokenType != TokenType.Name)
                    throw UnexpectedToken(TokenType.Name);

                string name = _lexer.StringValue;

                NextToken();
                if (_lexer.TokenType != TokenType.Colon)
                    throw UnexpectedToken(TokenType.Colon);

                NextToken();
                var value = ParseValue(depth);

                ArgumentNode node = new ArgumentNode
                {
                    Name = name,
                    Value = value
                };
                argumentNodes.Add(node);
            }

            NextToken();
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
                    NextToken();
                    return new IntValueNode() { Value = intValue };

                case TokenType.FloatValue:
                    double floatValue = _lexer.DoubleValue ??
                                        throw new InvalidOperationException("Tokenizer should have an float value");
                    NextToken();
                    return new FloatValueNode()
                    {
                        Value = floatValue
                    };

                case TokenType.StringValue:
                    string stringValue = _lexer.StringValue;
                    NextToken();
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

            NextToken();
            List<ObjectValueFieldNode> objectFields = new List<ObjectValueFieldNode>();
            while(_lexer.TokenType != TokenType.CloseBrace)
            {
                if (_lexer.TokenType != TokenType.Name)
                    throw UnexpectedToken(TokenType.Name, TokenType.CloseBrace);

                string name = _lexer.StringValue;
                NextToken();
                IValueNode value = ParseValue(depth + 1);

                ObjectValueFieldNode fieldNode = new ObjectValueFieldNode()
                {
                    Name = name,
                    Value = value
                };

                objectFields.Add(fieldNode);
            }

            NextToken();
            return new ObjectValueNode(objectFields);
        }

        private IValueNode ParseListValue(int depth)
        {
            if (_lexer.TokenType != TokenType.OpenBracket)
                throw new InvalidOperationException("List should start with open bracket");

            NextToken();

            List<IValueNode> listContents = new List<IValueNode>();
            while(_lexer.TokenType != TokenType.CloseBracket)
            {
                IValueNode value = ParseValue(depth + 1);
                listContents.Add(value);
            }

            NextToken();
            return new ListValueNode(listContents);
        }

        private IValueNode ParseVariable()
        {
            if (_lexer.TokenType != TokenType.Dollar)
                throw new InvalidOperationException("Fragment should start with dollar");

            NextToken();
            if (_lexer.TokenType != TokenType.Name)
                throw UnexpectedToken(TokenType.Name);

            VariableValueNode variable = new VariableValueNode(_lexer.StringValue);
            NextToken();
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
 
            NextToken();
            return new EnumValueNode(name);
        }

        private ISelectionItemNode ParseFragmentInSelectionSet(int depth)
        {
            if (_lexer.TokenType != TokenType.Ellipsis)
                throw new InvalidOperationException("Fragment should start with ellipsis");

            NextToken();
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

            NextToken();
            if (_lexer.TokenType != TokenType.Name)
                throw UnexpectedToken(TokenType.Name);

            string typeName = _lexer.StringValue;
            List<DirectiveNode> directives = null;

            NextToken();
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
            throw new GraphQueryParseException(message, _lexer.StartPosition);
        }

        private Exception UnexpectedToken(params TokenType[] tokensExpected)
        {
            return UnexpectedToken(new string[0], tokensExpected);
        }

        private Exception UnexpectedToken(params string[] pseudoKeywordsExpected)
        {
            return UnexpectedToken(pseudoKeywordsExpected, new TokenType[0]);
        }

        private Exception UnexpectedToken(string[] pseudoKeywordsExpected, TokenType[] tokensExpected)
        {
            string expectedString = string.Join(", ",
                pseudoKeywordsExpected.Select(x => "'" + x + "'").Union(
                    tokensExpected.Select(y => NameToken(y) ?? y.ToString())
                ));

            throw new GraphQueryParseException("Invalid token '" + CurrentToken()
                                                   + "'. Expected: " + expectedString,
                _lexer.StartPosition);
        }

        private string CurrentToken()
        {
            string name = NameToken(_lexer.TokenType);
            if (name != null)
                return name;

            if (_lexer.TokenType == TokenType.Name)
                return _lexer.StringValue;

            return _lexer.TokenType.ToString();
        }

        private string NameToken(TokenType tokenType)
        {
            switch (tokenType)
            {
                case TokenType.OpenBrace:
                    return "{";
                case TokenType.CloseBrace:
                    return "}";
                case TokenType.AtSign:
                    return "@";
                case TokenType.Bang:
                    return "!";
                case TokenType.OpenBracket:
                    return "[" ;
                case TokenType.CloseBracket:
                    return "]" ;
                case TokenType.OpenParen:
                    return "(";
                case TokenType.CloseParen:
                    return ")";
                case TokenType.Colon:
                    return ":";
                case TokenType.Comma:
                    return ",";
                case TokenType.Dollar:
                    return "$";
                case TokenType.Ellipsis:
                    return "...";
                case TokenType.Pipe:
                    return "|";
                case TokenType.Eq:
                    return "=";
                default:
                    return null;
            }
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
        public GraphQueryParseException(string message, LexerPosition position)
            : base(message)
        {
            ErrorPosition = position;   
        }

        public LexerPosition ErrorPosition { get; }
    }
}