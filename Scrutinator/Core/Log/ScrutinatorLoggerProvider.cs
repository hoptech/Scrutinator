namespace Scrutinator.Core.Log;

using Microsoft.Extensions.Logging;

public class ScrutinatorLoggerProvider : ILoggerProvider
{
    private readonly LogHub _hub;

    public ScrutinatorLoggerProvider(LogHub hub) => _hub = hub;

    public ILogger CreateLogger(string categoryName) => new ScrutinatorLogger(categoryName, _hub);

    public void Dispose() { }
}