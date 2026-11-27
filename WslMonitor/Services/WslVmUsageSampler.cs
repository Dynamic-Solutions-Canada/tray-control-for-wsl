using Microsoft.UI.Dispatching;

namespace WslMonitor.Services;

/// <summary>
/// Samples the WSL 2 VM's CPU and memory once a second, but only between <see cref="Start"/> and
/// <see cref="Stop"/>: the flyout starts it when it opens and stops it when it closes, so a closed
/// flyout costs nothing (no timer, no snapshot buffer, no history). Readings come from Windows, never
/// from inside WSL, so sampling can't keep a distro or the VM running.
/// <para>Use on the UI thread. Kept apart from <see cref="WslMonitorService"/> so a reading never
/// raises its <c>Changed</c>, which rebuilds the whole flyout.</para>
/// </summary>
public sealed class WslVmUsageSampler
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// The first CPU figure needs a second reading; take it this soon after opening rather than a
    /// full interval later, so the flyout doesn't sit on a placeholder.
    /// </summary>
    private static readonly TimeSpan FirstInterval = TimeSpan.FromMilliseconds(400);

    private readonly WslVmProcess _process = new();
    private DispatcherQueueTimer? _timer;

    /// <summary>Raised on the UI thread after each reading.</summary>
    public event Action? Updated;

    /// <summary>The readings since <see cref="Start"/>.</summary>
    public WslVmUsageTracker Usage { get; } = new();

    /// <summary>The VM's limits, read from .wslconfig at <see cref="Start"/>.</summary>
    public WslVmLimits Limits { get; private set; }

    /// <summary>True between <see cref="Start"/> and <see cref="Stop"/>.</summary>
    public bool IsRunning => _timer != null;

    /// <summary>
    /// Begin sampling, taking the first reading now so whoever shows it can size itself before it
    /// appears. Does nothing if already started.
    /// </summary>
    public void Start(DispatcherQueue dispatcher)
    {
        if (_timer != null)
        {
            return;
        }

        // .wslconfig is read once per opening: a change only applies after WSL restarts anyway.
        var config = WslConfigFile.Read();
        Limits = WslVmLimits.From(
            config.MemoryBytes,
            config.Processors,
            GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,
            Environment.ProcessorCount);

        _timer = dispatcher.CreateTimer();
        _timer.Interval = FirstInterval;
        _timer.IsRepeating = true;
        _timer.Tick += OnTick;
        _timer.Start();
        Logger.Debug("VM usage sampling started");

        Sample();
    }

    /// <summary>Stop sampling and free everything it held. Safe to call when already stopped.</summary>
    public void Stop()
    {
        if (_timer == null)
        {
            return;
        }

        _timer.Stop();
        _timer.Tick -= OnTick;
        _timer = null;
        _process.Dispose();
        Usage.Reset();
        Logger.Debug("VM usage sampling stopped");
    }

    private void OnTick(DispatcherQueueTimer timer, object args)
    {
        timer.Interval = Interval;
        Sample();
    }

    private void Sample()
    {
        try
        {
            Usage.Add(_process.Read(), Limits.Processors);
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Couldn't read WSL VM usage");
            Usage.Reset();
        }

        Updated?.Invoke();
    }
}
