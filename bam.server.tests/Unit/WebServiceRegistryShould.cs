using Bam.DependencyInjection;
using Bam.Test;

namespace Bam.Server.Tests.Unit;

[UnitTestMenu("WebServiceRegistry should", Selector = "wsr")]
public class WebServiceRegistryShould : UnitTestMenuContainer
{
    public WebServiceRegistryShould(ServiceRegistry serviceRegistry) : base(serviceRegistry)
    {
    }

    [WebService]
    private class ValidWebService
    {
        public string Name => "valid";
    }

    private class NotAWebService
    {
        public string Name => "invalid";
    }

    private interface ITestService
    {
        string Name { get; }
    }

    [WebService]
    private class ValidTestServiceImpl : ITestService
    {
        public string Name => "valid-impl";
    }

    private class InvalidTestServiceImpl : ITestService
    {
        public string Name => "invalid-impl";
    }

    [UnitTest]
    public void ResolveClassAdornedWithWebServiceAttribute()
    {
        var registry = new WebServiceRegistry();
        registry.For<ValidWebService>().Use<ValidWebService>();

        When.A<WebServiceRegistry>(
            "resolves a class adorned with [WebService]",
            registry,
            (reg) => reg.Get<ValidWebService>())
        .TheTest
        .ShouldPass(because =>
        {
            because.TheResult.IsNotNull()
                .As<ValidWebService>("resolved instance is ValidWebService", r => r.Name == "valid");
        })
        .SoBeHappy()
        .UnlessItFailed();
    }

    [UnitTest]
    public void ThrowClassNotAWebServiceExceptionForUnadornedClass()
    {
        var registry = new WebServiceRegistry();
        registry.For<NotAWebService>().Use<NotAWebService>();

        ClassNotAWebServiceException? caught = null;

        When.A<WebServiceRegistry>(
            "attempts to resolve a class without [WebService]",
            registry,
            (reg) =>
            {
                try
                {
                    reg.Get<NotAWebService>();
                }
                catch (ClassNotAWebServiceException ex)
                {
                    caught = ex;
                }
                return caught!;
            })
        .TheTest
        .ShouldPass(because =>
        {
            because.ItsTrue("ClassNotAWebServiceException was thrown", caught != null);
            because.ItsTrue("OffendingType is NotAWebService",
                caught?.OffendingType == typeof(NotAWebService));
        })
        .SoBeHappy()
        .UnlessItFailed();
    }

    [UnitTest]
    public void ResolveInterfaceToAdornedImplementation()
    {
        var registry = new WebServiceRegistry();
        registry.For<ITestService>().Use<ValidTestServiceImpl>();

        When.A<WebServiceRegistry>(
            "resolves an interface to a [WebService] implementation",
            registry,
            (reg) => reg.Get<ITestService>())
        .TheTest
        .ShouldPass(because =>
        {
            because.TheResult.IsNotNull()
                .As<ITestService>("resolved instance has expected name", r => r.Name == "valid-impl");
        })
        .SoBeHappy()
        .UnlessItFailed();
    }

    [UnitTest]
    public void ThrowWhenInterfaceResolvesToUnadornedImplementation()
    {
        var registry = new WebServiceRegistry();
        registry.For<ITestService>().Use<InvalidTestServiceImpl>();

        ClassNotAWebServiceException? caught = null;

        When.A<WebServiceRegistry>(
            "resolves an interface to an unadorned implementation",
            registry,
            (reg) =>
            {
                try
                {
                    reg.Get<ITestService>();
                }
                catch (ClassNotAWebServiceException ex)
                {
                    caught = ex;
                }
                return caught!;
            })
        .TheTest
        .ShouldPass(because =>
        {
            because.ItsTrue("ClassNotAWebServiceException was thrown", caught != null);
            because.ItsTrue("OffendingType is InvalidTestServiceImpl",
                caught?.OffendingType == typeof(InvalidTestServiceImpl));
        })
        .SoBeHappy()
        .UnlessItFailed();
    }

    [UnitTest]
    public void ResolveWithCtorParamsWhenAdorned()
    {
        var registry = new WebServiceRegistry();
        registry.For<ValidWebService>().Use<ValidWebService>();

        When.A<WebServiceRegistry>(
            "resolves with ctor params for adorned class",
            registry,
            (reg) => reg.Get<ValidWebService>(Array.Empty<object>()))
        .TheTest
        .ShouldPass(because =>
        {
            because.TheResult.IsNotNull();
        })
        .SoBeHappy()
        .UnlessItFailed();
    }

    [UnitTest]
    public void ThrowWithCtorParamsWhenNotAdorned()
    {
        var registry = new WebServiceRegistry();
        registry.For<NotAWebService>().Use<NotAWebService>();

        ClassNotAWebServiceException? caught = null;

        When.A<WebServiceRegistry>(
            "resolves with ctor params for unadorned class",
            registry,
            (reg) =>
            {
                try
                {
                    reg.Get<NotAWebService>(Array.Empty<object>());
                }
                catch (ClassNotAWebServiceException ex)
                {
                    caught = ex;
                }
                return caught!;
            })
        .TheTest
        .ShouldPass(because =>
        {
            because.ItsTrue("ClassNotAWebServiceException was thrown", caught != null);
        })
        .SoBeHappy()
        .UnlessItFailed();
    }

    [UnitTest]
    public void ResolveByTypeWhenAdorned()
    {
        var registry = new WebServiceRegistry();
        registry.For<ValidWebService>().Use<ValidWebService>();

        When.A<WebServiceRegistry>(
            "resolves by Type for adorned class",
            registry,
            (reg) => reg.Get(typeof(ValidWebService)))
        .TheTest
        .ShouldPass(because =>
        {
            because.TheResult.IsNotNull()
                .IsOfType<ValidWebService>();
        })
        .SoBeHappy()
        .UnlessItFailed();
    }

    [UnitTest]
    public void ThrowByTypeWhenNotAdorned()
    {
        var registry = new WebServiceRegistry();
        registry.For<NotAWebService>().Use<NotAWebService>();

        ClassNotAWebServiceException? caught = null;

        When.A<WebServiceRegistry>(
            "resolves by Type for unadorned class",
            registry,
            (reg) =>
            {
                try
                {
                    reg.Get(typeof(NotAWebService));
                }
                catch (ClassNotAWebServiceException ex)
                {
                    caught = ex;
                }
                return caught!;
            })
        .TheTest
        .ShouldPass(because =>
        {
            because.ItsTrue("ClassNotAWebServiceException was thrown", caught != null);
        })
        .SoBeHappy()
        .UnlessItFailed();
    }
}
