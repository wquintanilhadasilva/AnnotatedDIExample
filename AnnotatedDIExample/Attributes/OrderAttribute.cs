namespace AnnotatedDIExample.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class OrderAttribute : Attribute
    {
        public int Value { get; }
        public OrderAttribute(int value) => Value = value;
    }
}
