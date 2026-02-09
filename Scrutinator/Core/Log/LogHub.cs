namespace Scrutinator.Core.Log;

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

public class LogHub
{
    private readonly ConcurrentQueue<LogEntry> _history = new();
    private const int MaxHistory = 50;
    
    // The event that the Middleware listens to
    public event Action<LogEntry>? OnLogCaptured;

    public void PushLog(string category, LogLevel level, string message)
    {
        var entry = new LogEntry(
            DateTime.Now.ToString("HH:mm:ss.fff"),
            level.ToString(),
            category,
            message
        );

        _history.Enqueue(entry);
        if (_history.Count > MaxHistory) _history.TryDequeue(out _);

        OnLogCaptured?.Invoke(entry);
    }

    public IEnumerable<LogEntry> GetHistory() => _history;
}