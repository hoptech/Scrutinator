namespace Scrutinator.Core.Log;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public class ScrutinatorLoggerProvider(LogHub hub, IWebHostEnvironment environment) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) =>
        environment.IsDevelopment() ? new ScrutinatorLogger(categoryName, hub) : NullLogger.Instance;

    public void Dispose() { }
}