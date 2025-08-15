using System;

namespace AnnotatedDI.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class ConditionalOnPropertyAttribute : Attribute
{
    public string Name { get; }
    public string HavingValue { get; }
    public bool MatchIfMissing { get; }

    public ConditionalOnPropertyAttribute(string name, string havingValue, bool matchIfMissing = false)
    {
        Name = name;
        HavingValue = havingValue;
        MatchIfMissing = matchIfMissing;
    }

}
