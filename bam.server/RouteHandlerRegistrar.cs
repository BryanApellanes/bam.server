using System.Reflection;
using System.Text;
using Bam.Protocol;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace Bam.Server;

public static class RouteHandlerRegistrar
{
    public static void RegisterRoutes<T>(WebApplicationBamServer server, WebApplication app)
    {
        RegisterRoutes(typeof(T), server, app);
    }

    public static void RegisterRoutes(Type type, WebApplicationBamServer server, WebApplication app)
    {
        var routePrefix = type.GetCustomAttribute<RoutePrefixAttribute>();
        string prefix = routePrefix?.Prefix ?? string.Empty;

        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            var routePath = method.GetCustomAttribute<RoutePathAttribute>();
            if (routePath == null) continue;

            string fullPath = prefix + routePath.Path;
            string operationIdentifier = OperationIdentifier.For(method);
            ParameterInfo[] parameters = method.GetParameters();

            RequestDelegate handler = async httpContext =>
            {
                var arguments = await ExtractArgumentsAsync(httpContext, parameters, routePath.HttpMethod);
                var request = new
                {
                    OperationIdentifier = operationIdentifier,
                    Arguments = arguments
                };

                string synthesizedBody = JsonConvert.SerializeObject(request);
                await server.HandlePipelineRequestAsync(httpContext, synthesizedBody);
            };

            switch (routePath.HttpMethod)
            {
                case "GET":
                    app.MapGet(fullPath, handler);
                    break;
                case "POST":
                    app.MapPost(fullPath, handler);
                    break;
                case "PUT":
                    app.MapPut(fullPath, handler);
                    break;
                case "DELETE":
                    app.MapDelete(fullPath, handler);
                    break;
                default:
                    app.Map(fullPath, handler);
                    break;
            }
        }
    }

    private static async Task<List<ArgumentDto>> ExtractArgumentsAsync(
        HttpContext httpContext,
        ParameterInfo[] parameters,
        string httpMethod)
    {
        Dictionary<string, object?>? bodyValues = null;
        if (httpMethod is "POST" or "PUT" or "PATCH")
        {
            httpContext.Request.EnableBuffering();
            using var reader = new StreamReader(httpContext.Request.Body, Encoding.UTF8, leaveOpen: true);
            string body = await reader.ReadToEndAsync();
            httpContext.Request.Body.Position = 0;

            if (!string.IsNullOrEmpty(body))
            {
                var parsed = JsonConvert.DeserializeObject<Dictionary<string, object?>>(body);
                if (parsed != null)
                {
                    bodyValues = new Dictionary<string, object?>(parsed, StringComparer.OrdinalIgnoreCase);
                }
            }
        }

        var arguments = new List<ArgumentDto>();
        foreach (var param in parameters)
        {
            string paramName = param.Name!;
            object? value = null;

            if (httpContext.Request.RouteValues.TryGetValue(paramName, out var routeValue))
            {
                value = routeValue;
            }
            else if (bodyValues != null && bodyValues.TryGetValue(paramName, out var bodyValue))
            {
                value = bodyValue;
            }
            else if (httpContext.Request.Query.TryGetValue(paramName, out var queryValue))
            {
                value = queryValue.ToString();
            }

            arguments.Add(new ArgumentDto { ParameterName = paramName, Value = value });
        }

        return arguments;
    }

    private class ArgumentDto
    {
        public string ParameterName { get; set; } = null!;
        public object? Value { get; set; }
    }
}
