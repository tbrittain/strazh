namespace Strazh.Domain
{
    public abstract class Relationship
    {
        public abstract string Type { get; }
    }

    public class HaveRelationship : Relationship
    {
        override public string Type => "HAVE";
    }

    public class InvokeRelationship : Relationship
    {
        override public string Type => "INVOKE";
    }

    public class ConstructRelationship : Relationship
    {
        override public string Type => "CONSTRUCT";
    }

    public class OfTypeRelationship : Relationship
    {
        override public string Type => "OF_TYPE";
    }

    public class DeclaredAtRelationship : Relationship
    {
        override public string Type => "DECLARED_AT";
    }

    public class IncludedInRelationship : Relationship
    {
        override public string Type => "INCLUDED_IN";
    }

    public class DependsOnRelationship : Relationship
    {
        override public string Type => "DEPENDS_ON";
    }
}