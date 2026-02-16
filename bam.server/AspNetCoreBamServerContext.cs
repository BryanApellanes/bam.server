using System.Net;
using Bam.Protocol;
using Bam.Protocol.Server;

namespace Bam.Server;

/// <summary>
/// Implements <see cref="IBamServerContext"/> for ASP.NET Core, holding the per-request state
/// including session, actor, authentication, command, and authorization details.
/// </summary>
public class AspNetCoreBamServerContext : IBamServerContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AspNetCoreBamServerContext"/> class with the specified request ID and BAM request.
    /// </summary>
    /// <param name="requestId">The unique identifier for this request.</param>
    /// <param name="bamRequest">The BAM request associated with this context.</param>
    public AspNetCoreBamServerContext(string requestId, IBamRequest bamRequest)
    {
        RequestId = requestId;
        BamRequest = bamRequest;
    }

    /// <summary>
    /// Gets or sets the type of request (e.g., HTTP, TCP).
    /// </summary>
    public RequestType RequestType { get; set; }

    /// <summary>
    /// Gets or sets the underlying <see cref="HttpListenerContext"/>, if applicable.
    /// </summary>
    public HttpListenerContext? HttpContext { get; set; }

    /// <summary>
    /// Gets the unique identifier for this request.
    /// </summary>
    public string RequestId { get; }

    /// <summary>
    /// Gets the BAM request associated with this context.
    /// </summary>
    public IBamRequest BamRequest { get; }

    /// <summary>
    /// Gets or sets the BAM response to send back to the client.
    /// </summary>
    public IBamResponse BamResponse { get; set; }

    /// <summary>
    /// Gets or sets the output stream used for writing the response.
    /// </summary>
    public Stream? OutputStream { get; set; }

    /// <summary>
    /// Gets the server session state for the current request.
    /// </summary>
    public IServerSessionState ServerSessionState { get; private set; }

    /// <summary>
    /// Gets the actor (user/client) associated with the current request.
    /// </summary>
    public IActor Actor { get; private set; }

    /// <summary>
    /// Gets the authentication result for the current request.
    /// </summary>
    public BamAuthentication Authentication { get; private set; }

    /// <summary>
    /// Gets the command being executed in the current request.
    /// </summary>
    public ICommand Command { get; private set; }

    /// <summary>
    /// Gets the authorization calculation result for the current request.
    /// </summary>
    public IAuthorizationCalculation AuthorizationCalculation { get; private set; }

    /// <summary>
    /// Sets the server session state for the current request.
    /// </summary>
    /// <param name="sessionState">The session state to set.</param>
    /// <returns><c>true</c> if the session state has a valid session ID; otherwise, <c>false</c>.</returns>
    public bool SetSessionState(IServerSessionState sessionState)
    {
        ServerSessionState = sessionState;
        return sessionState?.SessionId != null;
    }

    /// <summary>
    /// Sets the actor (user/client) for the current request.
    /// </summary>
    /// <param name="actor">The actor to set.</param>
    /// <returns><c>true</c> if the actor is not null; otherwise, <c>false</c>.</returns>
    public bool SetActor(IActor actor)
    {
        Actor = actor;
        return actor != null;
    }

    /// <summary>
    /// Sets the authentication result for the current request.
    /// </summary>
    /// <param name="authentication">The authentication result to set.</param>
    /// <returns><c>true</c> if authentication was successful; otherwise, <c>false</c>.</returns>
    public bool SetAuthentication(BamAuthentication authentication)
    {
        Authentication = authentication;
        return authentication?.Success == true;
    }

    /// <summary>
    /// Sets the command to be executed for the current request.
    /// </summary>
    /// <param name="command">The command to set.</param>
    /// <returns><c>true</c> if the command is not null; otherwise, <c>false</c>.</returns>
    public bool SetCommand(ICommand command)
    {
        Command = command;
        return command != null;
    }

    /// <summary>
    /// Sets the authorization calculation result for the current request.
    /// </summary>
    /// <param name="authorizationCalculation">The authorization calculation to set.</param>
    /// <returns><c>true</c> if the authorization calculation is not null; otherwise, <c>false</c>.</returns>
    public bool SetAuthorizationCalculation(IAuthorizationCalculation authorizationCalculation)
    {
        AuthorizationCalculation = authorizationCalculation;
        return authorizationCalculation != null;
    }

    /// <summary>
    /// Records an exception that occurred during context initialization.
    /// </summary>
    /// <param name="exception">The exception that occurred.</param>
    public void SetInitializationException(Exception exception)
    {
        InitializationException = exception;
    }

    protected Exception? InitializationException { get; private set; }
}
