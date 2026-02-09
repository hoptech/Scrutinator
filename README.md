# Scrutinator

Scrutinator is a lightweight diagnostic tool for inspecting ASP.NET Core dependency injection containers. It captures your `IServiceCollection`, analyzes registrations, and serves a small dashboard that lists services, lifetimes, and potential captive dependency issues.

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
using Scrutinator.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScrutinator();

var app = builder.Build();

app.UseDIScrutinator();
app.UseLogScrutinator();

app.Run();
```

Open the dashboard at `http(s)://<host>/scrutinator`.

## Configuration

Configure options when adding the middleware:

```csharp
app.UseDIScrutinator(options =>
{
    options.IncludeSystemServices = false;
    options.ScanForCaptiveDependencies = true;
    options.OpenDashboardAutomatically = true;
});
```

Options:

- `IncludeSystemServices` (default: false): include `System.*` and `Microsoft.*` registrations.
- `ScanForCaptiveDependencies` (default: true): scan singleton constructors for scoped dependencies.
- `OpenDashboardAutomatically` (default: true): open the dashboard in the default browser on app start.

## How it works

Scrutinator captures the `IServiceCollection` via `AddScrutinator()`, then analyzes it when the dashboard route is hit. The UI is an embedded HTML file that renders the serialized report.

Captive dependency analysis is best-effort and only runs when a singleton has a concrete `ImplementationType`. Registrations based on factories or instances cannot be fully inspected.

## Development

This repo includes a `Sandbox` project that demonstrates usage:

```bash
dotnet run --project Sandbox
```

Then navigate to `https://localhost:<port>/scrutinator`.
