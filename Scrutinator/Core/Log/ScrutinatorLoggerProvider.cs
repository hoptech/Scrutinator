namespace Scrutinator.Core.Log;

using Microsoft.Extensions.Logging;

public class ScrutinatorLoggerProvider(LogHub hub) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new ScrutinatorLogger(categoryName, hub);

    public void Dispose() { }
}