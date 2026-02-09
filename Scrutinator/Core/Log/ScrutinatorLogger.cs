namespace Scrutinator.Core.Log;

using Microsoft.Extensions.Logging;

public class ScrutinatorLogger(string category, LogHub hub) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    
    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None; 

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        // PREVENT FEEDBACK LOOP: Don't log Scrutinator's own logs
        if (category.StartsWith("Scrutinator")) return;

        var message = formatter(state, exception);
        if (exception != null) message += $"\n{exception}";

        hub.PushLog(category, logLevel, message);
    }
}