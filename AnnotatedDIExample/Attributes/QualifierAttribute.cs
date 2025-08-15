namespace AnnotatedDIExample.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class QualifierAttribute : Attribute
    {
        public string Name { get; }
        public QualifierAttribute(string name) => Name = name;
    }
}
