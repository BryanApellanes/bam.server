using System.Text.Json;
using Bam.Configuration;
using Bam.Logging;
using Bam.Protocol;
using Bam.Protocol.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Bam.Server;

public class WebApplicationBamServer : Loggable, IAsyncManagedServer, IConfigurable, IDisposable
{
    private WebApplication? _app;
    private Task? _runTask;
    private CancellationTokenSource? _cts;
    private readonly BamRequestPipeline _pipeline;

    public WebApplicationBamServer(BamServerOptions options)
    {
        Options = options;
        ServerName = options.ServerName;
        HttpHostBinding = options.HttpHostBinding;
        Options.SubscribeEventHandlers(this);
        _pipeline = new BamRequestPipeline(options);
    }

    protected BamServerOptions Options { get; private set; }

    public string ServerName { get; private set; }
    public HostBinding HttpHostBinding { get; private set; }
    public int Port => HttpHostBinding?.Port ?? 0;

    public string LastExceptionMessage { get; set; }

    [Verbosity(VerbosityLevel.Information, SenderMessageFormat = "WebApplicationBamServer={ServerName};Port={Port};Starting")]
    public event EventHandler<BamServerEventArgs> Starting;

    [Verbosity(VerbosityLevel.Information, SenderMessageFormat = "WebApplicationBamServer={ServerName};Port={Port};Started")]
    public event EventHandler<BamServerEventArgs> Started;

    [Verbosity(VerbosityLevel.Information, SenderMessageFormat = "WebApplicationBamServer={ServerName};Port={Port};Stopping")]
    public event EventHandler<BamServerEventArgs> Stopping;

    [Verbosity(VerbosityLevel.Information, SenderMessageFormat = "WebApplicationBamServer={ServerName};Port={Port};Stopped")]
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

    public void Dispose()
    {
        TryStop();
    }

    public string[] RequiredProperties => Array.Empty<string>();

    public void Configure(IConfigurer configurer)
    {
        configurer.Configure(this);
        this.CheckRequiredProperties();
    }

    public void Configure(object configuration)
    {
        this.CopyProperties(configuration);
        this.CheckRequiredProperties();
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
                RequestType = RequestType.Http,
                OutputStream = httpContext.Response.Body
            };
            FireEvent(CreateContextComplete, new BamServerEventArgs(serverContext));

            // Run initialization pipeline
            FireEvent(InitializeContextStarted, new BamServerEventArgs(serverContext));
            BamServerEventArgs args = new BamServerEventArgs(serverContext);
            BamServerInitializationContext initialization = _pipeline.RunPipeline(serverContext, args);
            FireEvent(InitializeContextComplete, new BamServerEventArgs(serverContext));

            // Handle response
            await WriteResponseAsync(httpContext, initialization, serverContext);
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

    private async Task WriteResponseAsync(HttpContext httpContext, BamServerInitializationContext initialization, IBamServerContext serverContext)
    {
        httpContext.Response.ContentType = "application/json";

        if (serverContext.BamResponse != null)
        {
            if (serverContext.BamResponse is StartSessionResponse sessionResponse)
            {
                var responseData = new
                {
                    sessionResponse.SessionId,
                    sessionResponse.Nonce,
                    ServerPublicKey = sessionResponse.ServerPublicKey?.Pem
                };
                httpContext.Response.StatusCode = sessionResponse.StatusCode;
                await httpContext.Response.WriteAsync(JsonSerializer.Serialize(responseData));
            }
            else
            {
                serverContext.BamResponse.Send();
            }
            return;
        }

        if (initialization.Status == InitializationStatus.Success)
        {
            ICommunicationHandler? handler = Options.GetCommunicationHandler();
            object result = handler!.RequestProcessor!.ProcessRequestContext(serverContext);
            IObjectEncoderDecoder? encoder = handler.ObjectEncoderDecoder;
            httpContext.Response.StatusCode = 200;
            await httpContext.Response.WriteAsync(encoder!.Stringify(result));
            return;
        }

        // Initialization failed — return failure info
        httpContext.Response.StatusCode = DefaultBamResponseProvider.GetStatusCode(initialization.Status);
        var failure = new InitializationFailure
        {
            Status = initialization.Status,
            Message = initialization.Message
        };
        ICommunicationHandler? failHandler = Options.GetCommunicationHandler();
        await httpContext.Response.WriteAsync(failHandler!.ObjectEncoderDecoder!.Stringify(failure));
    }
}
