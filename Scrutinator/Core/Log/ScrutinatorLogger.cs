namespace Scrutinator.Core.Log;

using Microsoft.Extensions.Logging;

public class ScrutinatorLogger : ILogger
{
    private readonly string _category;
    private readonly LogHub _hub;

    public ScrutinatorLogger(string category, LogHub hub)
    {
        _category = category;
        _hub = hub;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    
    // Filter out generic noise if you want
    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None; 

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        // PREVENT FEEDBACK LOOP: Don't log Scrutinator's own logs
        if (_category.StartsWith("Scrutinator")) return;

        var message = formatter(state, exception);
        if (exception != null) message += $"\n{exception}";

        _hub.PushLog(_category, logLevel, message);
    }
}