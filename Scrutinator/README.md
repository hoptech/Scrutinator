# Scrutinator

Scrutinator is a lightweight .NET 10 diagnostic library for ASP.NET Core applications. It provides dashboards for dependency injection registrations, captured logs, and assemblies loaded in the current process.

## Features

- Lists all registered services with their implementation types, lifetimes, and namespaces.
- Filters by lifetime and text search in the embedded dashboard.
- Optional captive dependency warnings (singleton depending on scoped services).
- Hides Microsoft/System services by default to reduce noise.
- Optional auto-open of the dashboard on app start.

## Quick start

Install the package:

```bash
dotnet add package Scrutinator
```

Wire it up in your application:

```csharp
using Scrutinator.Middleware.DI;
using Scrutinator.Middleware.Log;
using Scrutinator.Middleware.Package;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddScrutinatorDI()
    .AddScrutinatorLog();
    
var app = builder.Build();

app
    .UseScrutinatorDI()
    .UseScrutinatorLog()
    .UseScrutinatorPackage();

app.Run();
```

Open the dashboards at `/di-scrutinator`, `/log-scrutinator`, and `/package-scrutinator` on your application host. The live log stream is available at `/log-scrutinator/stream`.

Scrutinator targets `net10.0` and references `Microsoft.AspNetCore.App`. Build with the .NET 10 SDK. Framework-dependent deployments need the ASP.NET Core 10 runtime; a self-contained publish can bundle it.

To intercept a custom logger, register it in `builder.Services`, then call `AddScrutinatorCustomLogger<TInterface>()` before `builder.Build()`. The custom logger interface must already be registered. You can provide a `CustomLogMapper` for a non-standard logger API.

## Configuration

Configure options when adding the middleware:

```csharp
app.UseScrutinatorDI(options =>
{
    options.IncludeSystemServices = false;
    options.ScanForCaptiveDependencies = true;
    options.OpenDashboardAutomatically = false;
});

app.UseScrutinatorLog(options =>
{
    options.OpenDashboardAutomatically = false;
});

app.UseScrutinatorPackage(opts =>
{
    opts.OpenDashboardAutomatically = false;
});
```

Options:

- `IncludeSystemServices` (default: false): include `System.*` and `Microsoft.*` registrations.
- `ScanForCaptiveDependencies` (default: true): scan singleton constructors for scoped dependencies.
- `OpenDashboardAutomatically` (default: true for all three dashboards): attempt to open the dashboard in the default browser after app start.

The dashboards are mapped only when the host environment is `Development`; middleware calls add no dashboard routes in other environments. Built-in and custom log capture is also disabled outside `Development`. Development routes have no built-in authentication, so do not expose them on an untrusted network.

Automatic browser launching is intended for local development. In `Development`, each dashboard option defaults to `true` and uses the first HTTP address reported by the server; it does not discover an external host, forwarded scheme, or proxy path prefix. Because this option is per dashboard, enabling all three produces three launch attempts. Set it to `false` for headless, containerized, or remote hosts. This only disables browser launching; it does not change whether the dashboard routes are mapped.

The route prefixes are fixed. When hosting under a path base or behind a proxy that adds a prefix, apply `UsePathBase` or path-rewrite middleware before the Scrutinator middleware and configure the proxy to forward that prefix. Navigate using the external base path plus the dashboard route.

## How it works

### DI - Scrutinator
Scrutinator captures the `IServiceCollection` via `AddScrutinatorDI()`, then analyzes it when the dashboard route is hit. The UI is an embedded HTML file that renders the serialized report.

Captive dependency analysis is best-effort. It selects a unique, most-parameterized resolvable public constructor, applies last-registration semantics for single services and all-registration semantics for `IEnumerable<T>`, and matches closed generic dependencies to open-generic registrations. The report separates potential direct captive-dependency findings from analysis limitations. Factories, instances, keyed dependencies, open-generic singleton implementations, ambiguous constructors, and transitive or service-locator dependencies are identified as limitations rather than treated as definitive findings.

### Log - Scrutinator

Scrutinator captures `ILogger` events in process memory, retaining the most recent 50 entries and streaming new events over Server-Sent Events. Each stream has a bounded 128-entry queue; when a client falls behind, the oldest queued events are dropped. The dashboard keeps at most 500 rendered rows.

### Package - Scrutinator
Scrutinator lists assemblies loaded in the current process. Direct-dependency status is inferred from assembly references, not package lock-file metadata.

The dashboards are mapped only when the host environment is `Development`; middleware calls add no dashboard routes in other environments. Built-in and custom log capture is also disabled outside `Development`. Development routes have no built-in authentication, so do not expose them on an untrusted network. Disable automatic browser launching in headless development environments.

## Development

The project's [repo](https://github.com/hoptech/Scrutinator) includes a `Sandbox` project that demonstrates usage:

```bash
dotnet run --project Sandbox --launch-profile headless

dotnet build --configuration Release
dotnet pack --configuration Release
```

The `headless` profile disables browser launching while keeping the app in `Development`. Then navigate manually to `http://localhost:5218/di-scrutinator`, `/log-scrutinator`, or `/package-scrutinator`.
