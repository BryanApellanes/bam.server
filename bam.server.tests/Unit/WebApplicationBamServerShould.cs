using Bam.Console;
using Bam.Net;
using Bam.Protocol.Server;
using Bam.Server;
using Bam.Test;

namespace Bam.Server.Tests.Unit;

[UnitTestMenu("WebApplicationBamServer should", Selector = "wabs")]
public class WebApplicationBamServerShould : UnitTestMenuContainer
{
    [UnitTest]
    [ConsoleCommand("Be Created Via Factory Method", "Create server via BamPlatform factory and verify registration")]
    public void BeCreatedViaFactoryMethod()
    {
        When.A<WebApplicationBamServer>("is created via factory method",
            () => BamPlatform.CreateWebApplicationServerAsync("factory-test", 0).GetAwaiter().GetResult(),
            (server) => new object?[] { server.ServerName, server.HttpHostBinding, BamPlatform.Servers.Contains(server) })
        .TheTest
        .ShouldPass(because =>
        {
            object?[] r = (object?[])because.Result;
            because.ItsTrue("ServerName equals expected", "factory-test".Equals(r[0]));
            because.ItsTrue("HttpHostBinding is not null", r[1] != null);
            because.ItsTrue("Server is registered in BamPlatform.Servers", (bool)r[2]!);
        })
        .SoBeHappy()
        .UnlessItFailed();
    }

    [UnitTest]
    [ConsoleCommand("Stop Cleanly", "Start and stop server, verify events and port release")]
    public void StopCleanly()
    {
        int port = RandomNumber.Between(10000, 60000);
        bool stoppingFired = false;
        bool stoppedFired = false;

        var options = new BamServerOptions();
        options.ServerName = "stop-test";
        options.HttpHostBinding = new HostBinding(port);

        When.A<WebApplicationBamServer>("stops cleanly",
            () =>
            {
                var server = new WebApplicationBamServer(options);
                server.Stopping += (_, _) => stoppingFired = true;
                server.Stopped += (_, _) => stoppedFired = true;
                return server;
            },
            (server) =>
            {
                server.Start();
                server.Stop();
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(2);
                bool connectionRefused = false;
                try
                {
                    client.GetAsync($"http://localhost:{port}/test").GetAwaiter().GetResult();
                }
                catch
                {
                    connectionRefused = true;
                }
                return connectionRefused;
            })
        .TheTest
        .ShouldPass(because =>
        {
            because.ItsTrue("Stopping event was fired", stoppingFired);
            because.ItsTrue("Stopped event was fired", stoppedFired);
            because.ItsTrue("Server does not accept connections after stop", (bool)because.Result);
        })
        .SoBeHappy()
        .UnlessItFailed();
    }
}
