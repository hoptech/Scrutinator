namespace Scrutinator.Middleware.Package;

using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Scrutinator.Core.Package;
using Scrutinator.Util;

public static class PackageScrutinatorExtensions
{
    private const string RoutePrefix = "/package-scrutinator";

    public static IApplicationBuilder UsePackageScrutinator(this IApplicationBuilder app, Action<PackageScrutinatorOptions>? configure = null)
    {
        var options = new PackageScrutinatorOptions();
        configure?.Invoke(options);

        app.Map(RoutePrefix, builder =>
        {
            builder.Run(async context =>
            {
                // 1. Get Embedded HTML
                var assembly = Assembly.GetExecutingAssembly();
                using var stream = assembly.GetManifestResourceStream("Scrutinator.UI.packages.html");
                
                if (stream == null)
                {
                    await context.Response.WriteAsync("Error: UI resource 'Scrutinator.UI.packages.html' not found.");
                    return;
                }

                using var reader = new StreamReader(stream);
                var htmlTemplate = await reader.ReadToEndAsync();

                // 2. Fetch Data (Live from AppDomain)
                var report = PackageAnalyzer.Analyze();
                var data = report.Packages
                    .OrderByDescending(x => x.IsDirect).ThenBy(x => x.Name)
                    .ToArray();

                var json = JsonSerializer.Serialize(data);

                // 3. Inject Data & Serve
                var finalHtml = htmlTemplate.Replace("{{DATA_PLACEHOLDER}}", json);
                
                context.Response.ContentType = "text/html";
                await context.Response.WriteAsync(finalHtml);
            });
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
}