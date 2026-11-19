namespace Scrutinator.Core.Log;

using Microsoft.Extensions.Logging;
using System.Threading.Channels;

public class LogHub
{
    private const int MaxHistory = 50;
    private const int MaxSubscriberBuffer = 128;
    private readonly object _gate = new();
    private readonly Queue<LogEntry> _history = new();
    private readonly HashSet<Channel<LogEntry>> _subscribers = [];

    public event Action<LogEntry>? OnLogCaptured;

    public void PushLog(string category, LogLevel level, string message)
    {
        var entry = new LogEntry(
            DateTime.Now.ToString("HH:mm:ss.fff"),
            level.ToString(),
            category,
            message
        );

        lock (_gate)
        {
            _history.Enqueue(entry);
            while (_history.Count > MaxHistory)
            {
                _history.Dequeue();
            }

            foreach (var subscriber in _subscribers)
            {
                subscriber.Writer.TryWrite(entry);
            }
        }

        OnLogCaptured?.Invoke(entry);
    }

    public IEnumerable<LogEntry> GetHistory()
    {
        lock (_gate)
        {
            return _history.ToArray();
        }
    }

    internal LogSubscription Subscribe()
    {
        var channel = Channel.CreateBounded<LogEntry>(new BoundedChannelOptions(MaxSubscriberBuffer)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

        LogEntry[] history;
        lock (_gate)
        {
            history = _history.ToArray();
            _subscribers.Add(channel);
        }

        return new LogSubscription(history, channel.Reader, () =>
        {
            lock (_gate)
            {
                _subscribers.Remove(channel);
                channel.Writer.TryComplete();
            }
        });
    }
}

internal sealed class LogSubscription(
    IReadOnlyList<LogEntry> history,
    ChannelReader<LogEntry> reader,
    Action unsubscribe) : IDisposable
{
    private Action? _unsubscribe = unsubscribe;

    public IReadOnlyList<LogEntry> History { get; } = history;
    public ChannelReader<LogEntry> Reader { get; } = reader;

    public void Dispose()
    {
        Interlocked.Exchange(ref _unsubscribe, null)?.Invoke();
    }
}