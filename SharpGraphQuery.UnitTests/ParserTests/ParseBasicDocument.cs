using SharpGraphQl;
using System.Collections.Generic;
using System.Text;

namespace SharpGraphQuery.UnitTests.ParserTests
{
    public class ParseBasicDocument : ParserFixture
    {
        protected override RootNode GetAst()
        {
            return new RootNode()
            {
                Definitions =
                {
                    new OperationDefinitionNode()
                    {
                        OperationType = OperationType.Query,
                        Name = "getHobbits",
                        Directives = new List<DirectiveNode>(),
                        VariableDefinitions = new List<VariableDefinitionNode>()
                        {
                            new VariableDefinitionNode()
                            {
                                Name = "book",
                                Type = new NotNullTypeNode(new NamedTypeNode("Int"))
                            }
                        },
                        SelectionSet = new SelectionSetNode()
                        {
                            Items = new List<ISelectionItemNode>()
                            {
                                new FieldNode
                                {
                                    Name = "hobbits",
                                    Arguments = new List<ArgumentNode>()
                                    {
                                        new ArgumentNode()
                                        {
                                            Name = "book",
                                            Value = new VariableValueNode("book")
                                        }
                                    },
                                    SelectionSet = new SelectionSetNode()
                                    {
                                        Items = new List<ISelectionItemNode>()
                                        {
                                            new FieldNode("id"),
                                            new FieldNode("name"),
                                            new FieldNode
                                            {
                                                Name = "height",
                                                Alias = "heightInMeters",
                                                Directives = new List<DirectiveNode>(),
                                                Arguments = new List<ArgumentNode>()
                                                {
                                                    new ArgumentNode()
                                                    {
                                                        Name = "units",
                                                        Value = new EnumValueNode("METERS")
                                                    }
                                                }
                                            },
                                            new FieldNode
                                            {
                                                Name = "interactsWith",
                                                Directives = new List<DirectiveNode>(),
                                                SelectionSet = new SelectionSetNode()
                                                {
                                                    Items = new List<ISelectionItemNode>()
                                                    {
                                                        new FieldNode("id"),
                                                        new FieldNode("name"),
                                                        new FieldNode("lunch"),
                                                        new InlineFragmentNode()
                                                        {
                                                            Directives = new List<DirectiveNode>(),
                                                            TypeCondition = "Enemy",
                                                            SelectionSet = new SelectionSetNode()
                                                            {
                                                                Items = new List<ISelectionItemNode>()
                                                                {
                                                                    new FieldNode("specialPower")
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                },
                            }
                        }
                    },

                    new FragmentDefinitionNode()
                    {
                        Name = "FriendFragment",
                        OnType = "Hobbit",
                        Directives = new List<DirectiveNode>(),
                        SelectionSet = new SelectionSetNode()
                        {
                            Items = new List<ISelectionItemNode>()
                            {
                                new FieldNode("lunch"),
                                new FieldNode("starSign")
                            }
                        }
                    }
                }
            };
        }
    }
}
