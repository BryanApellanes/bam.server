using System.Text.Json;
using Bam.Logging;
using Bam.Protocol;
using Bam.Protocol.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Bam.Server;

public class WebApplicationManagedServer : Loggable, IAsyncManagedServer
{
    private WebApplication? _app;
    private Task? _runTask;
    private CancellationTokenSource? _cts;

    public WebApplicationManagedServer()
    {
    }

    public WebApplicationManagedServer(string serverName, HostBinding hostBinding)
    {
        ServerName = serverName;
        HttpHostBinding = hostBinding;
    }

    public string ServerName { get; private set; }
    public HostBinding HttpHostBinding { get; private set; }
    public int Port => HttpHostBinding?.Port ?? 0;

    public string LastExceptionMessage { get; set; }

    [Verbosity(VerbosityLevel.Information, SenderMessageFormat = "BamHttpServer={ServerName};Port={Port};Starting")]
    public event EventHandler<BamServerEventArgs> Starting;

    [Verbosity(VerbosityLevel.Information, SenderMessageFormat = "BamHttpServer={ServerName};Port={Port};Started")]
    public event EventHandler<BamServerEventArgs> Started;

    [Verbosity(VerbosityLevel.Information, SenderMessageFormat = "BamHttpServer={ServerName};Port={Port};Stopping")]
    public event EventHandler<BamServerEventArgs> Stopping;

    [Verbosity(VerbosityLevel.Information, SenderMessageFormat = "BamHttpServer={ServerName};Port={Port};Stopped")]
    public event EventHandler<BamServerEventArgs> Stopped;

    [Verbosity(LogEventType.Error, SenderMessageFormat = "LastMessage: {LastExceptionMessage}")]
    public event EventHandler StartExceptionThrown;

    [Verbosity(LogEventType.Error, SenderMessageFormat = "LastMessage: {LastExceptionMessage}")]
    public event EventHandler RequestExceptionThrown;

    [Verbosity(LogEventType.Error, SenderMessageFormat = "LastMessage: {LastExceptionMessage}")]
    public event EventHandler ServerStartException;

    public event EventHandler HttpRequestReceived;

    [Verbosity(LogEventType.Information,
        SenderMessageFormat =
            "Client Connected: LocalEndpoint={LocalEndpoint}, RemoteEndpoint={RemoteEndpoint}")]
    public event EventHandler<BamServerEventArgs> TcpClientConnected;

    public event EventHandler<BamServerEventArgs> UdpDataReceived;

    public event EventHandler<BamServerEventArgs> CreateContextStarted;
    public event EventHandler<BamServerEventArgs> CreateContextComplete;

    public event EventHandler<BamServerEventArgs> InitializeContextStarted;
    public event EventHandler<BamServerEventArgs> InitializeContextComplete;

    public void Start()
    {
        try
        {
            FireEvent(Starting);

            try
            {
                _cts = new CancellationTokenSource();
                var builder = WebApplication.CreateBuilder();
                _app = builder.Build();
                _app.Urls.Add(HttpHostBinding.ToString());
                _app.Map("{**path}", HandleRequestAsync);
                _runTask = _app.StartAsync(_cts.Token);
            }
            catch (Exception ex)
            {
                LastExceptionMessage = ex.Message;
                FireEvent(StartExceptionThrown, new ErrorEventArgs(ex));
            }

            FireEvent(Started);
        }
        catch (Exception ex)
        {
            LastExceptionMessage = ex.Message;
            FireEvent(ServerStartException, new ErrorEventArgs(ex));
        }
    }

    public Task StartAsync()
    {
        return Task.Run(Start);
    }

    public void Stop()
    {
        FireEvent(Stopping);

        if (_app != null)
        {
            try
            {
                _app.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                // expected on cancellation
            }

            _app.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _app = null;
        }

        if (_cts != null)
        {
            _cts.Dispose();
            _cts = null;
        }

        _runTask = null;

        FireEvent(Stopped);
    }

    public Task StopAsync()
    {
        return Task.Run(Stop);
    }

    public void TryStop()
    {
        try
        {
            Stop();
        }
        catch (Exception ex)
        {
            LastExceptionMessage = ex.Message;
        }
    }

    public Task TryStopAsync()
    {
        return Task.Run(TryStop);
    }

    private async Task HandleRequestAsync(HttpContext httpContext)
    {
        try
        {
            string requestId = Cuid.Generate();

            FireEvent(HttpRequestReceived);

            FireEvent(CreateContextStarted);
            var bamRequest = new AspNetCoreBamRequest(httpContext);
            await bamRequest.ReadContentAsync();

            var serverContext = new AspNetCoreBamServerContext(requestId, bamRequest)
            {
                RequestType = RequestType.Http
            };
            FireEvent(CreateContextComplete, new BamServerEventArgs(serverContext));

            FireEvent(InitializeContextStarted, new BamServerEventArgs(serverContext));
            FireEvent(InitializeContextComplete, new BamServerEventArgs(serverContext));

            httpContext.Response.ContentType = "application/json";
            var responseBody = new
            {
                serverName = ServerName,
                requestId,
                method = bamRequest.HttpMethod.ToString(),
                url = bamRequest.Url.ToString(),
                headers = bamRequest.Headers,
                queryString = bamRequest.QueryString,
                timestamp = DateTimeOffset.UtcNow
            };
            await httpContext.Response.WriteAsync(JsonSerializer.Serialize(responseBody));
        }
        catch (Exception ex)
        {
            LastExceptionMessage = ex.Message;
            FireEvent(RequestExceptionThrown, new ErrorEventArgs(ex));
            try
            {
                httpContext.Response.StatusCode = 500;
                await httpContext.Response.WriteAsync(ex.Message);
            }
            catch
            {
                // best effort error response
            }
        }
    }
}
