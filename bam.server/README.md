# bam.server

ASP.NET Core-based HTTP server implementation for the BAM protocol request pipeline.

## Overview

bam.server provides a lightweight HTTP server built on top of ASP.NET Core's `WebApplication` infrastructure. It wraps ASP.NET Core's request handling to integrate with the BAM protocol layer, translating incoming HTTP requests into `IBamRequest` objects and routing them through a `BamRequestPipeline` for session management, authentication, authorization, and command processing.

The primary entry point is `WebApplicationBamServer`, which manages the full server lifecycle (start, stop, dispose) and exposes a rich set of events for observability. The companion `BamPlatform` class provides static factory methods for creating and managing server instances, including support for deterministic port assignment based on server names.

The server context model (`AspNetCoreBamServerContext`) carries the full request processing state -- session, actor identity, authentication result, command, and authorization calculation -- through the pipeline, allowing each stage to contribute its piece before the final response is written.

## Key Classes

| Class | Description |
|---|---|
| `WebApplicationBamServer` | Core HTTP server. Wraps `WebApplication`, binds to a `HostBinding`, routes all requests through `BamRequestPipeline`, and manages start/stop lifecycle with events. Implements `IAsyncManagedServer`, `IConfigurable`, `IDisposable`. |
| `BamPlatform` | Static factory for creating and tracking server instances. Provides `CreateServerAsync`, `CreateNamedServerAsync`, `CreateWebApplicationServerAsync`, and `StopServersAsync`. Supports deterministic port assignment via `GetUnprivilegedPortForName`. |
| `AspNetCoreBamServerContext` | Implements `IBamServerContext`. Carries request-scoped state through the pipeline: session state, actor, authentication, command, and authorization. |
| `AspNetCoreBamRequest` | Adapts ASP.NET Core `HttpContext` into the BAM `IBamRequest` interface. Extracts headers, query strings, cookies, HTTP method, URL, content type, and request body. |
| `HttpPostedFile` | Utility for parsing multipart file uploads from raw HTTP request streams. Handles boundary detection, metadata extraction, and saving uploaded files to disk. |

## Dependencies

**Project References:**
- `bam.base` -- core framework primitives (logging, extensions, configuration contracts)
- `bam.configuration` -- `IConfigurer`, `IConfigurable` infrastructure
- `bam.protocol` -- `IBamRequest`, `IBamResponse`, `HostBinding`, `HttpMethods`, protocol types
- `bam.protocol.server` -- `BamRequestPipeline`, `BamServerOptions`, `IBamServerContext`, `BamServerEventArgs`, `InitializationStatus`, server-side protocol contracts

**Framework References:**
- `Microsoft.AspNetCore.App` (ASP.NET Core shared framework)

**Target Framework:** net10.0

## Usage Examples

### Create and start a server on a specific port

```csharp
using Bam.Net;
using Bam.Server;

// Create via BamPlatform factory (registers in Servers collection)
WebApplicationBamServer server = await BamPlatform.CreateWebApplicationServerAsync("my-service", 8080);

// Subscribe to lifecycle events
server.Starting += (s, e) => Console.WriteLine("Server starting...");
server.Started += (s, e) => Console.WriteLine("Server started.");
server.HttpRequestReceived += (s, e) => Console.WriteLine("Request received.");

// Start the server
server.Start();

// ... server is listening on http://localhost:8080 ...

// Stop when done
server.Stop();
```

### Create a named server with deterministic port

```csharp
// Port is derived deterministically from the name (between 1024-65535)
WebApplicationBamServer server = await BamPlatform.CreateWebApplicationServerAsync("user-service");
server.Start();
Console.WriteLine($"Listening on port {server.Port}");
```

### Stop all registered servers

```csharp
await BamPlatform.StopServersAsync();
```

## Known Gaps / Not Yet Implemented

- **Streaming folder is empty.** The `.csproj` includes a `Streaming\` folder reference, but no source files exist in it. Streaming support appears to be planned but not yet implemented.
- **TCP/UDP event handlers are declared but not wired.** `TcpClientConnected` and `UdpDataReceived` events exist on `WebApplicationBamServer` but are never fired; the server currently handles only HTTP requests.
- **Response handling is HTTP-only.** The `WriteResponseAsync` method hardcodes `application/json` content type. There is no content negotiation or support for other response formats.
