using System.Reflection;
using Bam.DependencyInjection;

namespace Bam.Server;

/// <summary>
/// A service registry that enforces the <see cref="WebServiceAttribute"/> on resolved types.
/// Only classes adorned with <see cref="WebServiceAttribute"/> can be instantiated through this registry;
/// attempts to resolve unadored classes throw <see cref="ClassNotAWebServiceException"/>.
/// </summary>
public class WebServiceRegistry : ServiceRegistry
{
    public override T Get<T>()
    {
        T instance = base.Get<T>();
        EnsureWebService(instance);
        return instance;
    }

    public override object Get(Type type)
    {
        object instance = base.Get(type);
        EnsureWebService(instance);
        return instance;
    }

    public override T Get<T>(params object[] ctorParams)
    {
        T instance = base.Get<T>(ctorParams);
        EnsureWebService(instance);
        return instance;
    }

    public override object Get(Type type, params object[] ctorParams)
    {
        object instance = base.Get(type, ctorParams);
        EnsureWebService(instance);
        return instance;
    }

    private static void EnsureWebService(object? instance)
    {
        if (instance == null)
        {
            return;
        }

        Type concreteType = instance.GetType();
        if (!Attribute.IsDefined(concreteType, typeof(WebServiceAttribute)))
        {
            throw new ClassNotAWebServiceException(concreteType);
        }
    }
}
