namespace Scrutinator.Middleware.DI;

using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Scrutinator.Core.DI;
using Scrutinator.Util;

public static class DIScrutinatorExtensions
{
    private static IServiceCollection? _capturedServices;
    private const string RoutePrefix = "/di-scrutinator";
    
    /// <summary>
    /// Step 1: Capture the Service Collection
    /// </summary>
    public static IServiceCollection AddDIScrutinator(this IServiceCollection services)
    {
        _capturedServices = services;
        return services;
    }

    /// <summary>
    /// Step 2: Map the UI and Analysis Logic
    /// </summary>
    // [Conditional("DEBUG")]
    public static IApplicationBuilder UseDIScrutinator(this IApplicationBuilder app, Action<DIScrutinatorOptions>? configure = null)
    {
        if (_capturedServices == null)
        {
            throw new InvalidOperationException($"Scrutinator Error: You must call 'services.{nameof(AddDIScrutinator)}()' in ConfigureServices before calling 'app.{nameof(UseDIScrutinator)}()'.");
        }

        var options = new DIScrutinatorOptions();
        configure?.Invoke(options);

        // --- Part A: Middleware Logic (The UI) ---
        // app.Map branches the request pipeline. If the URL matches RoutePrefix, this branch runs.
        app.Map(RoutePrefix, builder =>
        {
            builder.Run(async context =>
            {
                // 1. Analyze
                var report = DependencyAnalyzer.Analyze(_capturedServices, options);
                
                // 2. Serialize
                var json = JsonSerializer.Serialize(report);

                // 3. Load HTML
                var assembly = Assembly.GetExecutingAssembly();

                using var stream = assembly.GetManifestResourceStream("Scrutinator.UI.index.html");
                if (stream == null)
                {
                    await context.Response.WriteAsync("<h1>Error: Embedded UI resource not found.</h1>");
                    return;
                }

                using var reader = new StreamReader(stream);
                var htmlTemplate = await reader.ReadToEndAsync();

                // 4. Inject & Serve
                var finalHtml = htmlTemplate.Replace("{{DATA_PLACEHOLDER}}", json);
                
                context.Response.ContentType = "text/html";
                await context.Response.WriteAsync(finalHtml);
            });
        });

        if (options.OpenDashboardAutomatically)
        {
            var lifetime = app.ApplicationServices.GetService<IHostApplicationLifetime>();
            var server = app.ApplicationServices.GetService<IServer>();

            if (lifetime != null && server != null)
            {
                lifetime.ApplicationStarted.Register(() =>
                {
                    var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses;
                    var address = addresses?.FirstOrDefault(a => a.StartsWith("http"));

                    if (address == null) return;

                    var finalUrl = address.Replace("0.0.0.0", "localhost")
                                       .Replace("[::]", "localhost") 
                                   + RoutePrefix;
                    BrowserLauncher.Open(finalUrl);
                });
            }
        }

        return app;
    }
}