using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Hosting;
using Scrutinator.Util;

namespace Scrutinator.Middleware.Log;

using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scrutinator.Core.Log;

public static class LogScrutinatorExtensions
{
    private const string RoutePrefix = "/log-scrutinator";

    public static IServiceCollection AddLogScrutinator(this IServiceCollection services)
    {
        services.AddSingleton<LogHub>();

        services.AddSingleton<ILoggerProvider, ScrutinatorLoggerProvider>();

        services.AddLogging(builder =>
        {
            services.AddSingleton<ILoggerProvider, ScrutinatorLoggerProvider>(sp =>
            {
                var hub = sp.GetRequiredService<LogHub>();
                return new ScrutinatorLoggerProvider(hub);
            });
        });

        return services;
    }
    public static IApplicationBuilder UseLogScrutinator(this IApplicationBuilder app, Action<LogScrutinatorOptions>? configure = null)
    {
        var hub = app.ApplicationServices.GetService<LogHub>();
        if (hub == null) throw new InvalidOperationException($"You must call services.{nameof(AddLogScrutinator)}() first.");
        
        var options = new LogScrutinatorOptions();
        configure?.Invoke(options);

        app.Map(RoutePrefix, builder =>
        {
            // 1. The UI Route (GET /logs)
            builder.Map("", uiApp => 
            {
                uiApp.Run(async context => 
                {
                    var assembly = Assembly.GetExecutingAssembly();

                    using var stream = assembly.GetManifestResourceStream("Scrutinator.UI.logs.html");
                    if (stream == null) 
                    {
                        context.Response.StatusCode = 404;
                        await context.Response.WriteAsync("<h1>Error 404</h1><p>UI Resource not found. Check 'Build Action: Embedded Resource'.</p>");
                        return;
                    }

                    using var reader = new StreamReader(stream);
                    var html = await reader.ReadToEndAsync();
                    context.Response.ContentType = "text/html";
                    await context.Response.WriteAsync(html);
                });
            });

            // 2. The Stream Route (GET /logs/stream)
            app.Map(RoutePrefix, builder =>
            {
                // 1. Map the stream explicitly
                builder.Map("/stream", streamApp => streamApp.Run(HandleStream));

                // 2. Run the UI for the base route
                builder.Run(async context =>
                {
                    var assembly = Assembly.GetExecutingAssembly();
                    // Ensure this string matches your Embedded Resource ID exactly!
                    using var stream = assembly.GetManifestResourceStream("Scrutinator.UI.logs.html");
            
                    if (stream == null)
                    {
                        context.Response.StatusCode = 404;
                        await context.Response.WriteAsync("<h1>Error 404</h1><p>UI Resource not found. Check 'Build Action: Embedded Resource'.</p>");
                        return;
                    }

                    using var reader = new StreamReader(stream);
                    var html = await reader.ReadToEndAsync();
                    context.Response.ContentType = "text/html";
                    await context.Response.WriteAsync(html);
                });
            });

            async Task HandleStream(HttpContext context)
            {
                context.Response.Headers.Append("Content-Type", "text/event-stream");
                context.Response.Headers.Append("Cache-Control", "no-cache");
                context.Response.Headers.Append("Connection", "keep-alive");

                // 1. Send History
                foreach (var log in hub.GetHistory())
                {
                    await SendLog(context, log);
                }

                // 2. Define Live Handler
                // We use a local function to wrap the async call
                async void OnLog(LogEntry log) => await SendLog(context, log);

                hub.OnLogCaptured += OnLog;

                try
                {
                    // Keep connection open indefinitely
                    await Task.Delay(Timeout.Infinite, context.RequestAborted);
                }
                catch (OperationCanceledException)
                {
                    // Expected when client disconnects
                }
                finally
                {
                    hub.OnLogCaptured -= OnLog;
                }
            }
        });
        
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
                                       + RoutePrefix;
                        BrowserLauncher.Open(finalUrl);
                    }
                });
            }
        }

        return app;
    }

    private static async Task SendLog(HttpContext context, LogEntry log)
    {
        try 
        {
            var json = JsonSerializer.Serialize(log);
            await context.Response.WriteAsync($"data: {json}\n\n");
            await context.Response.Body.FlushAsync();
        }
        catch { /* Ignore write errors */ }
    }
}