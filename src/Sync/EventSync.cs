namespace Sync;

using System.Collections.Concurrent;

public sealed class EventSync<TEvent> : IDisposable
{
    private readonly ConcurrentQueue<TEvent> eventQueue = new();
    private readonly SemaphoreSlim semaphore = new(0);
    private readonly Func<TEvent, bool> condition;
    private readonly Func<TEvent, bool> eventFilter = e => false;

    public EventSync(Func<TEvent, bool> condition)
    {
        this.condition = condition;
    }

    public EventSync(Func<TEvent, bool> condition, Func<TEvent, bool> eventFilter)
        : this(condition)
    {
        this.eventFilter = eventFilter;
    }

    public void OnEvent(object? sender, TEvent e)
    {
        OnEvent(e);
    }

    public void OnEvent(TEvent e)
    {
        if (eventFilter.Invoke(e))
        {
            return;
        }

        eventQueue.Enqueue(e);

        if (condition.Invoke(e))
        {
            semaphore.Release();
        }
    }

    public async Task<TEvent> WaitForEvent(TimeSpan timeout, CancellationToken ct = default)
    {
        if (eventQueue.Any(e => condition.Invoke(e)))
        {
            // This covers a race condition when condition parameters change.
            return eventQueue.First(e => condition.Invoke(e));
        }

        await semaphore.WaitAsync(timeout, ct);

        return eventQueue.First(e => condition.Invoke(e));
    }

    public void Dispose()
    {
        semaphore.Dispose();
    }
}
