namespace Bam.Server;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class RoutePrefixAttribute : Attribute
{
    public RoutePrefixAttribute(string prefix)
    {
        Prefix = prefix.TrimEnd('/');
    }

    public string Prefix { get; }
}
