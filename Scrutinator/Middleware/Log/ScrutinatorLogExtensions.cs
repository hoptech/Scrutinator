namespace Scrutinator.Middleware.Log;

using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Scrutinator.Util;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Scrutinator.Core.Log;

public static class ScrutinatorLogExtensions
{
    private const string RoutePrefix = "/log-scrutinator";

    /// <summary>
    /// Registers the Log Scrutinator logging service into the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection to register services into.</param>
    /// <returns>The service collection for method chaining.</returns>
    /// <remarks>
    /// This method must be called before <see cref="UseScrutinatorLog"/> to properly initialize the logging infrastructure.
    /// It registers the <see cref="LogHub"/> singleton and the <see cref="ScrutinatorLoggerProvider"/> as the application's logger provider.
    /// </remarks>
    public static IServiceCollection AddScrutinatorLog(this IServiceCollection services)
    {
        services.AddSingleton<LogHub>();
        services.AddLogging();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, ScrutinatorLoggerProvider>());

        return services;
    }

    /// <summary>
    /// Configures the Log Scrutinator middleware into the application pipeline.
    /// </summary>
    /// <param name="app">The application builder to configure.</param>
    /// <param name="configure">Optional configuration delegate to customize Log Scrutinator behavior.</param>
    /// <returns>The application builder for method chaining.</returns>
    /// <remarks>
    /// This method must be called after <see cref="AddScrutinatorLog"/> to activate the logging middleware.
    /// It sets up the dashboard UI at the route "/log-scrutinator" and configures Server-Sent Events (SSE) streaming for real-time log updates.
    /// </remarks>
    public static IApplicationBuilder UseScrutinatorLog(this IApplicationBuilder app, Action<LogScrutinatorOptions>? configure = null)
    {
        if (!app.ApplicationServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment())
        {
            return app;
        }

        var hub = app.ApplicationServices.GetService<LogHub>();
        if (hub == null) throw new InvalidOperationException($"You must call services.{nameof(AddScrutinatorLog)}() first.");
        
        var options = new LogScrutinatorOptions();
        configure?.Invoke(options);

        app.Map(RoutePrefix, builder =>
        {
            builder.Map("/stream", streamApp => streamApp.Run(HandleStream));

            builder.Run(async context =>
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

            async Task HandleStream(HttpContext context)
            {
                using var subscription = hub.Subscribe();

                context.Response.Headers.Append("Content-Type", "text/event-stream");
                context.Response.Headers.Append("Cache-Control", "no-cache");
                context.Response.Headers.Append("Connection", "keep-alive");

                try
                {
                    await context.Response.StartAsync(context.RequestAborted);

                    foreach (var log in subscription.History)
                    {
                        await SendLog(context, log);
                    }

                    await foreach (var log in subscription.Reader.ReadAllAsync(context.RequestAborted))
                    {
                        await SendLog(context, log);
                    }
                }
                catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
                {
                }
                catch (IOException) when (context.RequestAborted.IsCancellationRequested)
                {
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

    public static IServiceCollection AddScrutinatorCustomLogger<TInterface>(
        this IServiceCollection services,
        string categoryName = "CustomLogger",
        CustomLogMapper? customMapper = null) where TInterface : class
    {
        // Find the original registration of the custom logger
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(TInterface));
    
        if (descriptor == null) return services; // Not registered, nothing to intercept

        // Remove the original
        services.Remove(descriptor);

        var mapperToUse = customMapper ?? CustomLoggerProxy<TInterface>.DefaultMapper;

        // Re-register with the Proxy Wrapper
        services.Add(new ServiceDescriptor(typeof(TInterface), sp =>
        {
            // Create the original implementation manually
            TInterface realLogger;
            if (descriptor.ImplementationInstance != null)
            {
                realLogger = (TInterface)descriptor.ImplementationInstance;
            }
            else if (descriptor.ImplementationFactory != null)
            {
                realLogger = (TInterface)descriptor.ImplementationFactory(sp);
            }
            else
            {
                realLogger = (TInterface)ActivatorUtilities.CreateInstance(sp, descriptor.ImplementationType!);
            }

            if (!sp.GetRequiredService<IWebHostEnvironment>().IsDevelopment())
            {
                return realLogger;
            }

            var hub = sp.GetRequiredService<LogHub>();

            // Wrap it in the dynamic proxy
            return CustomLoggerProxy<TInterface>.Create(realLogger, hub, categoryName, mapperToUse);

        }, descriptor.Lifetime));

        return services;
    }

    private static async Task SendLog(HttpContext context, LogEntry log)
    {
        var json = JsonSerializer.Serialize(log);
        await context.Response.WriteAsync($"data: {json}\n\n", context.RequestAborted);
        await context.Response.Body.FlushAsync(context.RequestAborted);
    }
}