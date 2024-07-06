using System.Linq;

namespace Strazh.Domain
{
    public abstract class Node
    {
        public abstract string Label { get; }

        public virtual string FullName { get; }

        public virtual string Name { get; }

        /// <summary>
        /// Primary Key used to compare Matching of nodes on MERGE operation
        /// </summary>
        public virtual string Pk { get; protected set; }

        public Node(string fullName, string name)
        {
            FullName = fullName;
            Name = name;
            SetPrimaryKey();
        }

        protected virtual void SetPrimaryKey()
        {
            Pk = FullName.GetHashCode().ToString();
        }

        public virtual string Set(string node)
            => $"{node}.pk = \"{Pk}\", {node}.fullName = \"{FullName}\", {node}.name = \"{Name}\"";
    }

    // Code

    public abstract class CodeNode : Node
    {
        public CodeNode(string fullName, string name, string[] modifiers = null)
            : base(fullName, name)
        {

            Modifiers = modifiers == null ? "" : string.Join(", ", modifiers);
        }

        public string Modifiers { get; }

        override public string Set(string node)
            => $"{base.Set(node)}{(string.IsNullOrEmpty(Modifiers) ? "" : $", {node}.modifiers = \"{Modifiers}\"")}";
    }

    public abstract class TypeNode : CodeNode
    {
        public TypeNode(string fullName, string name, string[] modifiers = null)
            : base(fullName, name, modifiers)
        {
        }
    }

    public class ClassNode : TypeNode
    {
        public ClassNode(string fullName, string name, string[] modifiers = null)
            : base(fullName, name, modifiers)
        {
        }

        override public string Label { get; } = "Class";
    }

    public class InterfaceNode : TypeNode
    {
        public InterfaceNode(string fullName, string name, string[] modifiers = null)
            : base(fullName, name, modifiers)
        {
        }

        override public string Label { get; } = "Interface";
    }

    public class MethodNode : CodeNode
    {
        public MethodNode(string fullName, string name, (string name, string type)[] args, string returnType, string[] modifiers = null)
            : base(fullName, name, modifiers)
        {
            Arguments = string.Join(", ", args.Select(x => $"{x.type} {x.name}"));
            ReturnType = returnType;
            SetPrimaryKey();
        }

        override public string Label { get; } = "Method";

        public string Arguments { get; }

        public string ReturnType { get; }

        override public string Set(string node)
            => $"{base.Set(node)}, {node}.returnType = \"{ReturnType}\", {node}.arguments = \"{Arguments}\"";

        protected override void SetPrimaryKey()
        {
            Pk = $"{FullName}{Arguments}{ReturnType}".GetHashCode().ToString();
        }
    }

    // Structure

    public class FileNode : Node
    {
        public FileNode(string fullName, string name)
            : base(fullName, name) { }

        override public string Label { get; } = "File";
    }

    public class FolderNode : Node
    {
        public FolderNode(string fullName, string name)
            : base(fullName, name) { }

        override public string Label { get; } = "Folder";
    }

    public class ProjectNode : Node
    {
        public ProjectNode(string name)
            : this(name, name) { }

        public ProjectNode(string fullName, string name)
            : base(fullName, name) { }

        override public string Label { get; } = "Project";
    }

    public class PackageNode : Node
    {
        public PackageNode(string fullName, string name, string version)
            : base(fullName, name)
        {
            Version = version;
            SetPrimaryKey();
        }

        override public string Label { get; } = "Package";

        public string Version { get; }

        override public string Set(string node)
            => $"{base.Set(node)}, {node}.version = \"{Version}\"";

        protected override void SetPrimaryKey()
        {
            Pk = $"{FullName}{Version}".GetHashCode().ToString();
        }
    }
}