# bam.server

ASP.NET Core hosting for the Bam protocol — bridges attribute-routed minimal API endpoints into the framework's `bam.protocol` request pipeline.

## Overview

`bam.server` hosts a `bam.protocol` server on top of ASP.NET Core (`WebApplicationBamServer`, `AspNetCoreBamServerContext`, `AspNetCoreBamRequest`). Types marked `[WebService]` are eligible for remote execution; `WebServiceRegistry` tracks them.

Its notable feature is the **route-to-pipeline bridge**: `[RoutePrefix]`/`[RoutePath]` attributes let a service method declare a minimal-API route (e.g. `[RoutePath("/register", "POST")]`), and `RouteHandlerRegistrar.RegisterRoutes` maps that route to a handler that synthesizes a `MethodInvocationRequest` and replays it through the existing `bam.protocol` pipeline — rather than calling the service method directly. This ensures every route still passes through command resolution, encryption, and authorization instead of bypassing them, which a direct `MapPost` call would do. See `docs/route-to-pipeline-bridge.md` for the full design rationale and alternatives considered (consumer-side changes are documented in `bamsvc/docs/route-to-pipeline-bridge.md`).

## Key Classes

| Class / Interface | Description |
|---|---|
| `WebApplicationBamServer` | ASP.NET Core-backed implementation of the Bam protocol server host. |
| `AspNetCoreBamServerContext` / `AspNetCoreBamRequest` | Adapts ASP.NET Core's `HttpContext`/request into the `bam.protocol` `IBamRequest` abstraction. |
| `WebServiceAttribute` | Marks a class as eligible for remote execution via a `WebServiceRegistry`. |
| `WebServiceRegistry` | Tracks types decorated with `[WebService]`. |
| `RoutePrefixAttribute` / `RoutePathAttribute` | Declare a class-level URL prefix and per-method sub-path/HTTP verb for the route-to-pipeline bridge. |
| `RouteHandlerRegistrar` | Maps `[RoutePath]`-decorated methods to ASP.NET Core minimal-API routes that delegate into the `bam.protocol` request pipeline. |

## Dependencies

**Project References:** `bam.base`, `bam.configuration`, `bam.protocol`, `bam.protocol.server`.

**Framework Reference:** `Microsoft.AspNetCore.App`.

**Target Framework:** net10.0.

## Running Tests

```bash
dotnet run --project bam.server.tests/bam.server.tests.csproj -- --ut
```
