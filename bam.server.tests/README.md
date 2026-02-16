# bam.server.tests

Unit tests for the bam.server project, validating server lifecycle and factory behavior.

## Overview

bam.server.tests is a console-based test project that uses the BAM test framework (`bam.test`) and its menu-driven test runner. Tests are defined using the `[UnitTestMenu]` and `[UnitTest]` attributes and executed via `BamConsoleContext.StaticMain`. The project does not use xUnit or NUnit; instead it relies on the BAM `When.A<T>()` fluent assertion API.

The tests focus on the `WebApplicationBamServer` class, verifying that servers can be created through the `BamPlatform` factory methods, that lifecycle events (Starting, Stopping, Stopped) fire correctly, and that servers release their ports after stopping.

## Key Classes

| Class | Description |
|---|---|
| `WebApplicationBamServerShould` | Unit test container with the `wabs` selector. Contains tests for factory method creation (`BeCreatedViaFactoryMethod`) and clean shutdown behavior (`StopCleanly`). |
| `Program` | Entry point that delegates to `BamConsoleContext.StaticMain(args)` for menu-driven test execution. |

## Dependencies

**Project References:**
- `bam.console` -- `BamConsoleContext` for menu-driven CLI
- `bam.test` -- `UnitTestMenu`, `UnitTest`, `When.A<T>()` fluent test API, `UnitTestMenuContainer`
- `bam.server` -- the project under test

**Target Framework:** net10.0
**Output Type:** Exe

## Usage Examples

### Run all unit tests

```bash
dotnet run --project bam.server.tests -- --ut
```

### Run a specific test menu by selector

```bash
dotnet run --project bam.server.tests -- --ut wabs
```

### Example test structure

```csharp
[UnitTestMenu("WebApplicationBamServer should", Selector = "wabs")]
public class WebApplicationBamServerShould : UnitTestMenuContainer
{
    [UnitTest]
    public void BeCreatedViaFactoryMethod()
    {
        When.A<WebApplicationBamServer>("is created via factory method",
            () => BamPlatform.CreateWebApplicationServerAsync("factory-test", 0).GetAwaiter().GetResult(),
            (server) => new object?[] { server.ServerName })
        .TheTest
        .ShouldPass(because =>
        {
            object?[] r = (object?[])because.Result;
            because.ItsTrue("ServerName equals expected", "factory-test".Equals(r[0]));
        })
        .SoBeHappy()
        .UnlessItFailed();
    }
}
```

## Known Gaps / Not Yet Implemented

- **Unit folder is empty.** The `.csproj` includes a `Unit\` folder reference, and only one test file (`WebApplicationBamServerShould.cs`) exists. Additional test coverage for `AspNetCoreBamRequest`, `AspNetCoreBamServerContext`, `HttpPostedFile`, and pipeline behavior is not yet present.
- **No integration tests.** There are no tests that exercise the full request pipeline end-to-end (sending actual HTTP requests and validating responses).
