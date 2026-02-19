# Route-to-Pipeline Bridge: Attribute-Based Routing Through BamPipeline

> See also: [bamsvc route-to-pipeline-bridge.md](../../bamsvc/docs/route-to-pipeline-bridge.md) for the consumer-side changes.

## Problem

Direct minimal API routes (e.g., `MapPost("/api/register", ...)`) can call service methods directly, bypassing the BamPipeline entirely. Security attributes like `[AnonymousAccess(encryptionRequired: true)]` are silently ignored — no encryption, no authorization, no session management, no event hooks.

## Solution: Alternative A — Route-to-Pipeline Bridge

When a mapped route is hit, synthesize a `MethodInvocationRequest` JSON body, set it as `BamRequest.Content`, and delegate to the existing `HandleRequestAsync` pipeline path. Every pipeline stage (command resolution, anonymous access, encryption, authorization) runs unchanged.

### Alternatives Considered

| Alternative | Approach | Why Not |
|-------------|----------|---------|
| B: Pipeline-Aware Middleware | Middleware intercepts routes, selectively runs pipeline stages | Tightly couples to pipeline internals; two code paths for security |
| C: Dual Protocol Support | Keep REST routes; add REST-specific security layer | Duplicates security logic; drift risk between pipeline and REST |
| D: Convention-Based Routes | Derive routes from type/method names | Less explicit; hard to assign HTTP methods; naming collisions |

## New Types (bam.server)

### `RoutePrefixAttribute`

Class-level attribute specifying a URL prefix for all routed methods on the type.

```csharp
[RoutePrefix("/api/registration")]
public class RegistrationService { ... }
```

### `RoutePathAttribute`

Method-level attribute specifying a sub-path and HTTP method.

```csharp
[RoutePath("/register", "POST")]
public AccountData RegisterPerson(...) { ... }

[RoutePath("/profile/{handle}", "GET")]
public object? GetProfile(string handle) { ... }
```

### `RouteHandlerRegistrar`

Static class that scans a type for `[RoutePrefix]`/`[RoutePath]`, then maps ASP.NET routes that:

1. Extract arguments from route values, query string, and/or JSON body (case-insensitive matching)
2. Synthesize a `MethodInvocationRequest` JSON with correct `OperationIdentifier` and `Arguments`
3. Delegate to `WebApplicationBamServer.HandlePipelineRequestAsync(httpContext, synthesizedBody)`

## Changes to `WebApplicationBamServer`

### `AddRouteHandler<T>()`

Fluent method that registers a type for attribute-based route scanning. Routes are stored and mapped in `Start()` after `ConfigureRoutes` and before the catch-all `{**path}`.

```csharp
var webServer = new WebApplicationBamServer(options);
webServer.AddRouteHandler<RegistrationService>();
```

### `ExecutePipelineAsync` refactor

`HandleRequestAsync` was refactored into `ExecutePipelineAsync(httpContext, contentOverride?)`:

- If `contentOverride != null`, sets `bamRequest.Content` directly (skip `ReadContentAsync`)
- If `contentOverride == null`, reads from HTTP body (existing behavior)
- `HandlePipelineRequestAsync(httpContext, synthesizedBody)` is the internal entry point used by `RouteHandlerRegistrar`

## Encryption Design

For methods marked `[AnonymousAccess(encryptionRequired: true)]`, the pipeline enforces ECDH encryption. REST callers must:

1. POST to the session endpoint to negotiate ECDH keys
2. Encrypt the `MethodInvocationRequest` body with the derived AES key
3. Include session headers (`SessionId`, `BodySignature`, `Nonce`, `NonceHash`)

For methods marked `[AnonymousAccess]` without encryption, the pipeline allows plaintext anonymous access.

## Existing Code Reused (no changes)

- `OperationIdentifier.For(MethodInfo)` — generates `"TypeFullName+MethodName, AssemblyFullName"` format
- `Argument` class — `{ ParameterName, Value }` shape for serialization
- `AspNetCoreBamRequest.Content` setter — inject synthesized body
- `CommandResolver.ResolveCommand()` — parses MethodInvocationRequest JSON
- `BamRequestPipeline.RunPipeline()` — full pipeline unchanged
