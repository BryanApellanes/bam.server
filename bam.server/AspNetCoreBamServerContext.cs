using System.Net;
using Bam.Protocol;
using Bam.Protocol.Server;

namespace Bam.Server;

public class AspNetCoreBamServerContext : IBamServerContext
{
    public AspNetCoreBamServerContext(string requestId, IBamRequest bamRequest)
    {
        RequestId = requestId;
        BamRequest = bamRequest;
    }

    public RequestType RequestType { get; set; }
    public HttpListenerContext? HttpContext { get; set; }
    public string RequestId { get; }
    public IBamRequest BamRequest { get; }
    public IBamResponse BamResponse { get; set; }
    public Stream? OutputStream { get; set; }
    public IServerSessionState ServerSessionState { get; private set; }
    public IActor Actor { get; private set; }
    public BamAuthentication Authentication { get; private set; }
    public ICommand Command { get; private set; }
    public IAuthorizationCalculation AuthorizationCalculation { get; private set; }

    public bool SetSessionState(IServerSessionState sessionState)
    {
        ServerSessionState = sessionState;
        return sessionState?.SessionId != null;
    }

    public bool SetActor(IActor actor)
    {
        Actor = actor;
        return actor != null;
    }

    public bool SetAuthentication(BamAuthentication authentication)
    {
        Authentication = authentication;
        return authentication?.Success == true;
    }

    public bool SetCommand(ICommand command)
    {
        Command = command;
        return command != null;
    }

    public bool SetAuthorizationCalculation(IAuthorizationCalculation authorizationCalculation)
    {
        AuthorizationCalculation = authorizationCalculation;
        return authorizationCalculation != null;
    }

    public void SetInitializationException(Exception exception)
    {
        InitializationException = exception;
    }

    protected Exception? InitializationException { get; private set; }
}
