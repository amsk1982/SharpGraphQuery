using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using JetBrains.Annotations;

namespace SharpGraphQl
{
    public interface IGraphQueryNode
    {

    }

    public class RootNode : IGraphQueryNode
    {
        public List<IDefinitionNode> Definitions { get; }
            = new List<IDefinitionNode>();
    }

    public interface IDefinitionNode
    {
        string Name { get; }
    }

    public class OperationDefinitionNode : IGraphQueryNode, IDefinitionNode
    {
        public OperationType OperationType { get; set; }
        [CanBeNull]
        public string Name { get; set; }
        public List<VariableDefinitionNode> VariableDefinitions { get; set; }
        public List<DirectiveNode> Directives { get; set; }
        public SelectionSetNode SelectionSet { get; set; }
    }

    public class FragmentDefinitionNode : IGraphQueryNode, IDefinitionNode
    {
        public string Name { get; set; }
        public string OnType { get; set; }
        public List<DirectiveNode> Directives { get; set; }
        public SelectionSetNode SelectionSet { get; set; }
    }

    public class VariableDefinitionNode
    {
        public ITypeNode Type { get; set; }
        public string Name { get; set; }
        [CanBeNull]
        public IValueNode DefaultValue { get; set; }
    }

    public enum OperationType
    {
        Query,
        Mutation,
        Subscription
    }

    public class SelectionSetNode : IGraphQueryNode
    {
        public List<ISelectionItemNode> Items { get; set; }
    }

    public interface ISelectionItemNode
    {

    }

    public interface IValueNode
    {
        bool IsConstant { get; }
    }

    public class VariableValueNode : IValueNode
    {
        public bool IsConstant { get; } = false;
        public string Name { get; set; }

        public VariableValueNode(string name)
        {
            Name = name;
        }
    }

    public abstract class ConstValueNode<T> : IValueNode
    {
        public bool IsConstant => true;
        public T Value { get; set; }
    }

    public class IntValueNode : ConstValueNode<int> {}
    public class FloatValueNode : ConstValueNode<double> {}
    public class StringValueNode : ConstValueNode<string> {}
    public class BooleanValueNode : ConstValueNode<bool> {}

    public class NullValueNode : IValueNode
    {
        public bool IsConstant => true;
    }

    public class EnumValueNode : IValueNode
    {
        public string Name { get; }
        public bool IsConstant => true;

        public EnumValueNode(string name)
        {
            Name = name;
        }
    }

    public class ListValueNode : IValueNode
    {
        public ListValueNode(IList<IValueNode> wrapped)
        {
            Wrapped = new ReadOnlyCollection<IValueNode>(wrapped);
            IsConstant = Wrapped.All(x => x.IsConstant);
        }

        public IList<IValueNode> Wrapped { get; }
        public bool IsConstant { get; }
    }

    public class ObjectValueNode : IValueNode
    {
        public ObjectValueNode(IList<ObjectValueFieldNode> fields)
        {
            Fields = new ReadOnlyCollection<ObjectValueFieldNode>(fields);
            IsConstant = Fields.All(x => x.Value.IsConstant);
        }

        public IList<ObjectValueFieldNode> Fields { get; }
        public bool IsConstant { get; }
    }

    public class ObjectValueFieldNode
    {
        public string Name { get; set; }
        public IValueNode Value { get; set; }
    }

    public class FieldNode : ISelectionItemNode, IGraphQueryNode
    {
        [CanBeNull]
        public string Alias { get; set; }
        public string Name { get; set; }
        public List<ArgumentNode> Arguments { get; set; }
        public List<DirectiveNode> Directives { get; set; }

        [CanBeNull]
        public SelectionSetNode SelectionSet { get; set; }

        public FieldNode() { }
        public FieldNode(string name)
        {
            Name = name;
        }
    }

    public class FragmentSpreadNode : ISelectionItemNode, IGraphQueryNode
    {
        public string Name { get; set; }
        public List<DirectiveNode> Directives { get; set; }
    }

    public class InlineFragmentNode : ISelectionItemNode, IGraphQueryNode
    {
        [CanBeNull]
        public string TypeCondition { get; set; }
        public List<DirectiveNode> Directives { get; set; }
        public SelectionSetNode SelectionSet { get; set; }
    }

    public class ArgumentNode
    {
        public string Name { get; set; }
        public IValueNode Value { get; set; }
        public bool IsConstant => Value.IsConstant;
    }

    public class DirectiveNode
    {
        public string Name { get; set; }
        public List<ArgumentNode> Arguments { get; set; }
    }

    public interface ITypeNode
    {

    }

    public class NamedTypeNode : ITypeNode
    {
        public string Name { get; set; }

        //public NamedTypeNode() { }

        public NamedTypeNode(string name)
        {
            Name = name;
        }
    }

    public class ListTypeNode : ITypeNode
    {
        public ITypeNode Inner { get; set; }
    }

    public class NotNullTypeNode : ITypeNode
    {
        public ITypeNode Inner { get; set; }

        public NotNullTypeNode(ITypeNode inner)
        {
            Inner = inner;
        }
    }
}
