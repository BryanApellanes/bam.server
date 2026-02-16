using System.Net;
using System.Text;
using Bam.Protocol.Server;
using Microsoft.AspNetCore.Http;
using BamHttpMethods = Bam.Protocol.HttpMethods;

namespace Bam.Server;

/// <summary>
/// Adapts an ASP.NET Core <see cref="HttpContext"/> into the BAM framework's <see cref="IBamRequest"/> interface,
/// extracting headers, query strings, cookies, and other HTTP request details.
/// </summary>
public class AspNetCoreBamRequest : IBamRequest
{
    private readonly HttpContext _httpContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="AspNetCoreBamRequest"/> class by extracting request data from the given <see cref="HttpContext"/>.
    /// </summary>
    /// <param name="httpContext">The ASP.NET Core HTTP context to adapt.</param>
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

    /// <summary>
    /// Reads the request body content asynchronously as a UTF-8 string and sets the <see cref="Content"/> and <see cref="ContentLength64"/> properties.
    /// </summary>
    /// <returns>A task representing the asynchronous read operation.</returns>
    public async Task ReadContentAsync()
    {
        using var reader = new StreamReader(_httpContext.Request.Body, Encoding.UTF8);
        Content = await reader.ReadToEndAsync();
        ContentLength64 = Content.Length;
    }

    /// <summary>
    /// Gets the HTTP protocol version (e.g., "HTTP/1.1").
    /// </summary>
    public string ProtocolVersion => _httpContext.Request.Protocol;

    /// <summary>
    /// Gets or sets the request body content as a string.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the MIME types accepted by the client from the Accept header.
    /// </summary>
    public string[] AcceptTypes { get; set; }

    /// <summary>
    /// Gets or sets the character encoding of the request content. Defaults to UTF-8.
    /// </summary>
    public Encoding ContentEncoding { get; set; }

    /// <summary>
    /// Gets the length of the request body content in characters.
    /// </summary>
    public long ContentLength64 { get; private set; }

    /// <summary>
    /// Gets the query string parameters as key-value pairs.
    /// </summary>
    public Dictionary<string, string> QueryString { get; }

    /// <summary>
    /// Gets the MIME content type of the request.
    /// </summary>
    public string ContentType { get; }

    /// <summary>
    /// Gets the cookies sent with the request.
    /// </summary>
    public CookieCollection Cookies { get; }

    /// <summary>
    /// Gets the request headers as key-value pairs.
    /// </summary>
    public Dictionary<string, string> Headers { get; }

    /// <summary>
    /// Gets the HTTP method (GET, POST, etc.) of the request.
    /// </summary>
    public BamHttpMethods HttpMethod { get; }

    /// <summary>
    /// Gets the full URL of the request including scheme, host, path, and query string.
    /// </summary>
    public Uri Url { get; }

    /// <summary>
    /// Gets the URL of the referring page from the Referer header.
    /// </summary>
    public Uri UrlReferrer => Headers.ContainsKey("Referer") ? new Uri(Headers["Referer"]) : null!;

    /// <summary>
    /// Gets the user agent string from the request headers.
    /// </summary>
    public string UserAgent => Headers.ContainsKey("User-Agent") ? Headers["User-Agent"] : string.Empty;

    /// <summary>
    /// Gets the IP address of the remote client.
    /// </summary>
    public string UserHostAddress { get; }

    /// <summary>
    /// Gets the host name from the request's Host header.
    /// </summary>
    public string UserHostName { get; }

    /// <summary>
    /// Gets the languages accepted by the client from the Accept-Language header.
    /// </summary>
    public string[] UserLanguages { get; }

    /// <summary>
    /// Gets the raw URL path and query string of the request.
    /// </summary>
    public string RawUrl { get; }
}
