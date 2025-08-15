using AnnotatedDI.Attributes;

namespace AnnotatedDIExample.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class RepositoryAttribute : ServiceAttribute
    {
        public RepositoryAttribute(ServiceLifetime lifetime = ServiceLifetime.Scoped)
            : base(lifetime) { }
    }
}
