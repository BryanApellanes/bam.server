namespace Bam.Server;

/// <summary>
/// Marks a class as eligible for remote execution via a web service endpoint.
/// Classes without this attribute cannot be instantiated through a <see cref="WebServiceRegistry"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class WebServiceAttribute : Attribute
{
}
