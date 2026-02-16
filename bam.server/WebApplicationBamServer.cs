using System.Text.Json;
using Bam.Configuration;
using Bam.Logging;
using Bam.Protocol;
using Bam.Protocol.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Bam.Server;

/// <summary>
/// A BAM server implementation backed by ASP.NET Core's <see cref="WebApplication"/>.
/// Handles HTTP request routing through the BAM request pipeline and supports async start/stop lifecycle.
/// </summary>
public class WebApplicationBamServer : Loggable, IAsyncManagedServer, IConfigurable, IDisposable
{
    private WebApplication? _app;
    private Task? _runTask;
    private CancellationTokenSource? _cts;
    private readonly BamRequestPipeline _pipeline;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebApplicationBamServer"/> class with the specified options.
    /// </summary>
    /// <param name="options">The server options including host binding, server name, and event handlers.</param>
    public WebApplicationBamServer(BamServerOptions options)
    {
        Options = options;
        ServerName = options.ServerName;
        HttpHostBinding = options.HttpHostBinding;
        Options.SubscribeEventHandlers(this);
        _pipeline = new BamRequestPipeline(options);
    }

    protected BamServerOptions Options { get; private set; }

    /// <summary>
    /// Gets the logical name of this server instance.
    /// </summary>
    public string ServerName { get; private set; }

    /// <summary>
    /// Gets the host binding (host and port) that this server listens on.
    /// </summary>
    public HostBinding HttpHostBinding { get; private set; }

    /// <summary>
    /// Gets the port number from the HTTP host binding, or 0 if not set.
    /// </summary>
    public int Port => HttpHostBinding?.Port ?? 0;

    /// <summary>
    /// Gets or sets the message from the most recent exception.
    /// </summary>
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

    /// <summary>
    /// Starts the web application server, binding to the configured host and port and beginning to accept requests.
    /// </summary>
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

    /// <summary>
    /// Starts the server asynchronously on a background thread.
    /// </summary>
    /// <returns>A task representing the asynchronous start operation.</returns>
    public Task StartAsync()
    {
        return Task.Run(Start);
    }

    /// <summary>
    /// Stops the web application server and releases associated resources.
    /// </summary>
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

    /// <summary>
    /// Stops the server asynchronously on a background thread.
    /// </summary>
    /// <returns>A task representing the asynchronous stop operation.</returns>
    public Task StopAsync()
    {
        return Task.Run(Stop);
    }

    /// <summary>
    /// Attempts to stop the server, swallowing any exceptions that occur during shutdown.
    /// </summary>
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

    /// <summary>
    /// Attempts to stop the server asynchronously, swallowing any exceptions that occur during shutdown.
    /// </summary>
    /// <returns>A task representing the asynchronous stop attempt.</returns>
    public Task TryStopAsync()
    {
        return Task.Run(TryStop);
    }

    /// <summary>
    /// Disposes the server by attempting to stop it.
    /// </summary>
    public void Dispose()
    {
        TryStop();
    }

    /// <summary>
    /// Gets the names of properties required for configuration. Returns an empty array.
    /// </summary>
    public string[] RequiredProperties => Array.Empty<string>();

    /// <summary>
    /// Configures this server using the specified configurer.
    /// </summary>
    /// <param name="configurer">The configurer to apply.</param>
    public void Configure(IConfigurer configurer)
    {
        configurer.Configure(this);
        this.CheckRequiredProperties();
    }

    /// <summary>
    /// Configures this server by copying properties from the specified configuration object.
    /// </summary>
    /// <param name="configuration">The configuration object whose properties are copied to this instance.</param>
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
