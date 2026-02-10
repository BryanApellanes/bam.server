using System.Text.Json;
using Bam.Console;
using Bam.Net;
using Bam.Server;
using Bam.Test;

namespace Bam.Server.Tests.Unit;

[UnitTestMenu("WebApplicationManagedServer should", Selector = "wams")]
public class WebApplicationManagedServerShould : UnitTestMenuContainer
{
    [UnitTest]
    [ConsoleCommand("Start And Respond To Http Request", "Start server, make HTTP request, verify JSON echo")]
    public void StartAndRespondToHttpRequest()
    {
        int port = RandomNumber.Between(10000, 60000);
        bool startingFired = false;
        bool startedFired = false;
        bool httpRequestReceivedFired = false;

        When.A<WebApplicationManagedServer>("starts and responds to HTTP request",
            () =>
            {
                var server = new WebApplicationManagedServer("test-server", new HostBinding(port));
                server.Starting += (_, _) => startingFired = true;
                server.Started += (_, _) => startedFired = true;
                server.HttpRequestReceived += (_, _) => httpRequestReceivedFired = true;
                return server;
            },
            (server) =>
            {
                server.Start();
                try
                {
                    using var client = new HttpClient();
                    string url = $"http://localhost:{port}/test/path?foo=bar";
                    HttpResponseMessage response = client.GetAsync(url).GetAwaiter().GetResult();
                    string json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    using JsonDocument doc = JsonDocument.Parse(json);
                    JsonElement root = doc.RootElement;
                    return new object?[]
                    {
                        response.IsSuccessStatusCode,
                        root.GetProperty("serverName").GetString(),
                        root.GetProperty("method").GetString(),
                        root.GetProperty("url").GetString(),
                        root.GetProperty("queryString").GetProperty("foo").GetString(),
                        root.TryGetProperty("requestId", out JsonElement reqId) ? reqId.GetString() : null,
                        root.TryGetProperty("timestamp", out _)
                    };
                }
                finally
                {
                    server.TryStop();
                }
            })
        .TheTest
        .ShouldPass(because =>
        {
            because.TheResult.IsNotNull();
            if (because.Result is object?[] r)
            {
                because.ItsTrue("Starting event was fired", startingFired);
                because.ItsTrue("Started event was fired", startedFired);
                because.ItsTrue("HttpRequestReceived event was fired", httpRequestReceivedFired);
                because.ItsTrue("response is success status code", (bool)r[0]!);
                because.ItsTrue("serverName equals expected", "test-server".Equals(r[1]));
                because.ItsTrue("method equals GET", "GET".Equals(r[2]));
                because.ItsTrue("url contains expected path", ((string?)r[3])?.Contains($"localhost:{port}/test/path") == true);
                because.ItsTrue("queryString foo equals bar", "bar".Equals(r[4]));
                because.ItsTrue("requestId is not empty", !string.IsNullOrEmpty((string?)r[5]));
                because.ItsTrue("timestamp property exists", (bool)r[6]!);
            }
        })
        .SoBeHappy()
        .UnlessItFailed();
    }

    [UnitTest]
    [ConsoleCommand("Be Created Via Factory Method", "Create server via BamPlatform factory and verify registration")]
    public void BeCreatedViaFactoryMethod()
    {
        When.A<WebApplicationManagedServer>("is created via factory method",
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

        When.A<WebApplicationManagedServer>("stops cleanly",
            () =>
            {
                var server = new WebApplicationManagedServer("stop-test", new HostBinding(port));
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
