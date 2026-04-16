namespace Bam.Server;

/// <summary>
/// Thrown when a <see cref="WebServiceRegistry"/> attempts to resolve a type
/// that is not adorned with the <see cref="WebServiceAttribute"/>.
/// </summary>
public class ClassNotAWebServiceException : Exception
{
    public ClassNotAWebServiceException(Type type)
        : base($"The class '{type.FullName}' is not adorned with [{nameof(WebServiceAttribute)}] and cannot be instantiated as a web service.")
    {
        OffendingType = type;
    }

    public Type OffendingType { get; }
}
