using System.Net.Http.Json;
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
        StartAndRespondToHttpRequestAsync().GetAwaiter().GetResult();
    }

    private async Task StartAndRespondToHttpRequestAsync()
    {
        int port = RandomNumber.Between(10000, 60000);
        var server = new WebApplicationManagedServer("test-server", new HostBinding(port));

        bool startingFired = false;
        bool startedFired = false;
        bool httpRequestReceivedFired = false;

        server.Starting += (_, _) => startingFired = true;
        server.Started += (_, _) => startedFired = true;
        server.HttpRequestReceived += (_, _) => httpRequestReceivedFired = true;

        server.Start();

        try
        {
            using var client = new HttpClient();
            string url = $"http://localhost:{port}/test/path?foo=bar";
            HttpResponseMessage response = await client.GetAsync(url);
            string json = await response.Content.ReadAsStringAsync();
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            startingFired.IsTrue("Starting event was not fired");
            startedFired.IsTrue("Started event was not fired");
            httpRequestReceivedFired.IsTrue("HttpRequestReceived event was not fired");

            response.IsSuccessStatusCode
                .IsTrue($"Expected success status code but got {(int)response.StatusCode}");

            root.GetProperty("serverName").GetString()
                .ShouldBeEqualTo("test-server", "serverName mismatch");

            root.GetProperty("method").GetString()
                .ShouldBeEqualTo("GET", "method mismatch");

            root.GetProperty("url").GetString()!
                .Contains($"localhost:{port}/test/path")
                .IsTrue("url does not contain expected path");

            root.GetProperty("queryString").GetProperty("foo").GetString()
                .ShouldBeEqualTo("bar", "queryString foo mismatch");

            root.TryGetProperty("requestId", out JsonElement requestIdElement)
                .IsTrue("requestId property missing");

            string? requestId = requestIdElement.GetString();
            (requestId != null && requestId.Length > 0)
                .IsTrue("requestId should not be empty");

            root.TryGetProperty("timestamp", out _)
                .IsTrue("timestamp property missing");
        }
        finally
        {
            server.TryStop();
        }
    }

    [UnitTest]
    [ConsoleCommand("Be Created Via Factory Method", "Create server via BamPlatform factory and verify registration")]
    public void BeCreatedViaFactoryMethod()
    {
        BeCreatedViaFactoryMethodAsync().GetAwaiter().GetResult();
    }

    private async Task BeCreatedViaFactoryMethodAsync()
    {
        WebApplicationManagedServer server = await BamPlatform.CreateWebApplicationServerAsync("factory-test", 0);

        server.ServerName.ShouldBeEqualTo("factory-test", "ServerName mismatch");
        server.HttpHostBinding.ShouldNotBeNull("HttpHostBinding should not be null");
        BamPlatform.Servers.Contains(server).IsTrue("Server should be registered in BamPlatform.Servers");
    }

    [UnitTest]
    [ConsoleCommand("Stop Cleanly", "Start and stop server, verify events and port release")]
    public void StopCleanly()
    {
        StopCleanlyAsync().GetAwaiter().GetResult();
    }

    private async Task StopCleanlyAsync()
    {
        int port = RandomNumber.Between(10000, 60000);
        var server = new WebApplicationManagedServer("stop-test", new HostBinding(port));

        bool stoppingFired = false;
        bool stoppedFired = false;

        server.Stopping += (_, _) => stoppingFired = true;
        server.Stopped += (_, _) => stoppedFired = true;

        server.Start();
        server.Stop();

        stoppingFired.IsTrue("Stopping event was not fired");
        stoppedFired.IsTrue("Stopped event was not fired");

        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(2);
        bool connectionRefused = false;
        try
        {
            await client.GetAsync($"http://localhost:{port}/test");
        }
        catch
        {
            connectionRefused = true;
        }

        connectionRefused.IsTrue("Server should not accept connections after stop");
    }
}
