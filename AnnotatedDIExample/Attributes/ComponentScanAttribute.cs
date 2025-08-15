namespace AnnotatedDIExample.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class ComponentScanAttribute : Attribute
    {
        public Type[] Include { get; }
        public Type[]? Exclude { get; set; }

        public ComponentScanAttribute(params Type[] include)
        {
            Include = include;
        }
    }
}
