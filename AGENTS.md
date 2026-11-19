# Agent guide

## What this repository contains

Scrutinator is a .NET 10 ASP.NET Core diagnostics library with three opt-in dashboards:

- Dependency injection registrations and best-effort captive-dependency warnings.
- Captured `ILogger` events, shown live over Server-Sent Events (SSE).
- Assemblies loaded in the current application process.

The library is in `Scrutinator/`; `Sandbox/` is a runnable sample. The dashboard HTML is embedded from `Scrutinator/UI/`. Public integration methods live in `Scrutinator/Middleware/`.

## Using Scrutinator in an ASP.NET Core app

Install the package, then register and enable each dashboard independently. The DI and log dashboards require a registration call before their corresponding middleware. The package dashboard does not require service registration.

```csharp
using Scrutinator.Middleware.DI;
using Scrutinator.Middleware.Log;
using Scrutinator.Middleware.Package;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddScrutinatorDI()
    .AddScrutinatorLog();

var app = builder.Build();

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

app.UseScrutinatorPackage(options =>
{
    options.OpenDashboardAutomatically = false;
});

app.Run();
```

The dashboards are served at `/di-scrutinator`, `/log-scrutinator`, and `/package-scrutinator`. Log events stream from `/log-scrutinator/stream`.

To intercept a custom logger, register the logger in `builder.Services`, call `AddScrutinatorLog()`, and then call `AddScrutinatorCustomLogger<TInterface>()` before `builder.Build()`. The custom interface must be registered for the wrapper to be applied. Supply a `CustomLogMapper` if the default method-name/message mapping does not fit the logger.

## Runtime and data limitations

- Scrutinator targets `net10.0` and references `Microsoft.AspNetCore.App`. Build with the .NET 10 SDK; framework-dependent deployments need the ASP.NET Core 10 runtime, while self-contained publishes can bundle it.
- DI registrations are captured in a per-container singleton holder; the internal holder is excluded from dashboard counts.
- DI analysis is static and best-effort: it selects a unique, most-parameterized resolvable public constructor, applies last-registration semantics for single services and all-registration semantics for `IEnumerable<T>`, and matches closed generic dependencies to open-generic registrations. The report separates potential direct captive-dependency findings from limitations; factories, instances, keyed dependencies, open-generic singleton implementations, ambiguous constructors, and transitive or service-locator dependencies are not presented as definitive findings.
- Log history is held in memory and limited to the most recent 50 entries; each SSE client has a 128-entry bounded queue that drops oldest queued events under backpressure. The browser keeps at most 500 rendered rows.
- The package view inspects assemblies loaded into the current process. Direct-dependency status is inferred from assembly references, not package lock-file metadata.
- Dashboard routes are mapped only when `IHostEnvironment.EnvironmentName` is `Development`; middleware calls add no routes in other environments. Built-in and custom log capture is also disabled outside `Development`.
- Development routes have no built-in authentication or authorization. Use them only on a trusted development host or protect them at the host level.
- `OpenDashboardAutomatically` defaults to `true` for all three dashboards. It only acts in Development and uses the first HTTP address reported by the server; enabling all three causes three launch attempts. Disable it for headless, containerized, or remote hosts. It does not discover proxy-forwarded hosts, schemes, or path prefixes.
- Routes have fixed prefixes. For a path base or proxy-added prefix, apply `UsePathBase` or path-rewrite middleware before Scrutinator and configure the proxy to forward the prefix. The dashboard routes have no built-in authentication.

## Working in this repository

- Build: `dotnet build Scrutinator.sln --configuration Release`
- Run the sample: `dotnet run --project Sandbox`
- Run the browser-free Development sample: `dotnet run --project Sandbox --launch-profile headless`
- There is currently no automated test project. When changing runtime behavior, add focused tests before relying on a build-only check.
- Keep the package README at `Scrutinator/README.md` in sync with the root `README.md`; the project file packages the former.
- Embedded UI files and their resource names must stay consistent with the lookups in the middleware.
