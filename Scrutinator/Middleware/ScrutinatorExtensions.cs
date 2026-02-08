using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Scrutinator.Core;
using Scrutinator.Util;

namespace Scrutinator.Middleware;

public static class ScrutinatorExtensions
{
    private static IServiceCollection? _capturedServices;
    
    /// <summary>
    /// Step 1: Capture the Service Collection
    /// </summary>
    public static IServiceCollection AddScrutinator(this IServiceCollection services)
    {
        _capturedServices = services;
        return services;
    }

    /// <summary>
    /// Step 2: Map the UI and Analysis Logic
    /// </summary>
    // [Conditional("DEBUG")]
    public static IApplicationBuilder UseScrutinator(this IApplicationBuilder app, Action<ScrutinatorOptions>? configure = null)
    {
        if (_capturedServices == null)
        {
            throw new InvalidOperationException("Scrutinator Error: You must call 'services.AddScrutinator()' in ConfigureServices before calling 'app.UseScrutinator()'.");
        }

        var options = new ScrutinatorOptions();
        configure?.Invoke(options);

        // --- Part A: Middleware Logic (The UI) ---
        // app.Map branches the request pipeline. If the URL matches RoutePrefix, this branch runs.
        app.Map(options.RoutePrefix, builder =>
        {
            builder.Run(async context =>
            {
                // 1. Analyze
                var report = DependencyAnalyzer.Analyze(_capturedServices, options);
                
                // 2. Serialize
                var json = JsonSerializer.Serialize(report);

                // 3. Load HTML
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = "Scrutinator.UI.index.html"; // Check your namespace!

                using var stream = assembly.GetManifestResourceStream(resourceName);
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

        // --- Part B: Auto-Opener Logic ---
        if (options.OpenDashboardAutomatically)
        {
            // In Startup.cs, we access services via app.ApplicationServices
            var lifetime = app.ApplicationServices.GetService<IHostApplicationLifetime>();
            var server = app.ApplicationServices.GetService<IServer>();

            if (lifetime != null && server != null)
            {
                lifetime.ApplicationStarted.Register(() =>
                {
                    var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses;
                    var address = addresses?.FirstOrDefault(a => a.StartsWith("http"));

                    if (address != null)
                    {
                        var finalUrl = address.Replace("0.0.0.0", "localhost")
                                              .Replace("[::]", "localhost") 
                                              + options.RoutePrefix;
                        BrowserLauncher.Open(finalUrl);
                    }
                });
            }
        }

        return app;
    }
}