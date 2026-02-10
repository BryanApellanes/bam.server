using System.Net;
using System.Text;
using Bam.Protocol.Server;
using Microsoft.AspNetCore.Http;
using BamHttpMethods = Bam.Protocol.HttpMethods;

namespace Bam.Server;

public class AspNetCoreBamRequest : IBamRequest
{
    private readonly HttpContext _httpContext;

    public AspNetCoreBamRequest(HttpContext httpContext)
    {
        _httpContext = httpContext;
        HttpRequest request = httpContext.Request;

        Headers = new Dictionary<string, string>();
        foreach (var header in request.Headers)
        {
            Headers[header.Key] = header.Value.ToString();
        }

        QueryString = new Dictionary<string, string>();
        foreach (var query in request.Query)
        {
            QueryString[query.Key] = query.Value.ToString();
        }

        Cookies = new CookieCollection();
        foreach (var cookie in request.Cookies)
        {
            Cookies.Add(new Cookie(cookie.Key, cookie.Value));
        }

        if (Enum.TryParse<BamHttpMethods>(request.Method, true, out var method))
        {
            HttpMethod = method;
        }

        string url = $"{request.Scheme}://{request.Host}{request.Path}{request.QueryString}";
        Url = new Uri(url);
        RawUrl = $"{request.Path}{request.QueryString}";

        ContentType = request.ContentType ?? string.Empty;
        ContentEncoding = Encoding.UTF8;
        AcceptTypes = Headers.ContainsKey("Accept") ? Headers["Accept"].Split(',') : Array.Empty<string>();
        UserHostAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
        UserHostName = request.Host.Value ?? string.Empty;
        UserLanguages = Headers.ContainsKey("Accept-Language")
            ? Headers["Accept-Language"].Split(',')
            : Array.Empty<string>();
    }

    public async Task ReadContentAsync()
    {
        using var reader = new StreamReader(_httpContext.Request.Body, Encoding.UTF8);
        Content = await reader.ReadToEndAsync();
        ContentLength64 = Content.Length;
    }

    public string ProtocolVersion => _httpContext.Request.Protocol;
    public string Content { get; set; } = string.Empty;
    public string[] AcceptTypes { get; set; }
    public Encoding ContentEncoding { get; set; }
    public long ContentLength64 { get; private set; }
    public Dictionary<string, string> QueryString { get; }
    public string ContentType { get; }
    public CookieCollection Cookies { get; }
    public Dictionary<string, string> Headers { get; }
    public BamHttpMethods HttpMethod { get; }
    public Uri Url { get; }
    public Uri UrlReferrer => Headers.ContainsKey("Referer") ? new Uri(Headers["Referer"]) : null!;
    public string UserAgent => Headers.ContainsKey("User-Agent") ? Headers["User-Agent"] : string.Empty;
    public string UserHostAddress { get; }
    public string UserHostName { get; }
    public string[] UserLanguages { get; }
    public string RawUrl { get; }
}
