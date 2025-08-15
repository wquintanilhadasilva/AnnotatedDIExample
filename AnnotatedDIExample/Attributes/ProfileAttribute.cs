namespace AnnotatedDI.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class ProfileAttribute : Attribute
{
    public string[] Include { get; }
    public string[] Exclude { get; }
    public ProfileAttribute(string? include = null, string? exclude = null)
    {
        Include = ParseProfiles(include);
        Exclude = ParseProfiles(exclude);
    }

    private static string[] ParseProfiles(string? profiles)
    {
        if (profiles == null)
        {
            return Array.Empty<string>();
        }
        return profiles?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            ?? Array.Empty<string>();
    }
}
