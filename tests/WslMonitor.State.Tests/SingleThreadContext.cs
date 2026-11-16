namespace WslMonitor.State.Tests;

/// <summary>
/// Runs a scenario on one thread, the way the app runs the service on its UI thread, so continuations
/// interleave in a fixed order. Fails the scenario if it has not finished within the time limit, so a
/// regression that leaves the service waiting shows up as a failure instead of a hang.
/// </summary>
public sealed class SingleThreadContext : SynchronizationContext
{
    private readonly Queue<(SendOrPostCallback Callback, object? State)> _queue = new();
    private readonly object _lock = new();

    public override void Post(SendOrPostCallback d, object? state)
    {
        lock (_lock)
        {
            _queue.Enqueue((d, state));
            Monitor.Pulse(_lock);
        }
    }

    public override void Send(SendOrPostCallback d, object? state) =>
        throw new NotSupportedException("The service never blocks on the UI thread.");

    /// <summary>Run <paramref name="body"/> to completion on this thread, or throw after <paramref name="limit"/>.</summary>
    public static void Run(Func<Task> body, TimeSpan limit)
    {
        var context = new SingleThreadContext();
        var previous = Current;
        SetSynchronizationContext(context);
        try
        {
            var task = body();
            var deadline = DateTime.UtcNow + limit;

            // Pump posted continuations until the scenario finishes. Work it leaves running in the
            // background (an init probe, say) is simply abandoned with the context.
            while (!task.IsCompleted)
            {
                (SendOrPostCallback Callback, object? State) item;
                lock (context._lock)
                {
                    while (context._queue.Count == 0 && !task.IsCompleted)
                    {
                        var remaining = deadline - DateTime.UtcNow;
                        if (remaining <= TimeSpan.Zero)
                        {
                            throw new TimeoutException($"Scenario still running after {limit.TotalSeconds} s");
                        }

                        Monitor.Wait(context._lock, TimeSpan.FromMilliseconds(Math.Min(remaining.TotalMilliseconds, 50)));
                    }
                    if (context._queue.Count == 0)
                    {
                        break;
                    }

                    item = context._queue.Dequeue();
                }
                item.Callback(item.State);
            }

            task.GetAwaiter().GetResult();
        }
        finally
        {
            SetSynchronizationContext(previous);
        }
    }
}
