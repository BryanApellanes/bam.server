namespace Bam.Server;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class RoutePathAttribute : Attribute
{
    public RoutePathAttribute(string path, string httpMethod = "POST")
    {
        Path = path.StartsWith("/") ? path : "/" + path;
        HttpMethod = httpMethod.ToUpperInvariant();
    }

    public string Path { get; }
    public string HttpMethod { get; }
}
