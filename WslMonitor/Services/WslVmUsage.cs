namespace WslMonitor.Services;

/// <summary>
/// One reading of the WSL 2 virtual machine's host process: the processor time it has used so far and
/// the memory Windows has given it. Taken from Windows' process snapshot by <see cref="WslVmProcess"/>.
/// </summary>
/// <param name="ProcessId">The VM process, so a restarted VM never pairs with the previous one's reading.</param>
/// <param name="CpuTime100ns">User plus kernel time since the VM started, in 100 ns units.</param>
/// <param name="PrivateBytes">Private working set: the memory Task Manager shows for the VM.</param>
/// <param name="WallTime100ns">When the reading was taken, on a monotonic clock, in 100 ns units.</param>
public readonly record struct WslVmSample(long ProcessId, long CpuTime100ns, long PrivateBytes, long WallTime100ns);

/// <summary>
/// What the WSL 2 VM may use, so usage reads as a share of its own limits rather than of the PC:
/// 100% CPU means every processor WSL may use is busy.
/// </summary>
/// <param name="MemoryBytes">The VM's memory limit.</param>
/// <param name="Processors">How many logical processors the VM may use.</param>
/// <param name="MemoryFromConfig">True when the limit is <c>memory</c> in .wslconfig, false when it is WSL's default.</param>
public readonly record struct WslVmLimits(long MemoryBytes, int Processors, bool MemoryFromConfig)
{
    /// <summary>
    /// The limits WSL applies: <c>[wsl2] memory</c> and <c>processors</c> from .wslconfig when set,
    /// otherwise its documented defaults (half of the PC's memory, every logical processor). Neither
    /// can exceed what the PC has.
    /// </summary>
    /// <param name="configMemoryBytes">The <c>memory</c> setting, or null when unset or unreadable.</param>
    /// <param name="configProcessors">The <c>processors</c> setting, or null when unset or unreadable.</param>
    public static WslVmLimits From(long? configMemoryBytes, int? configProcessors, long hostMemoryBytes, int hostProcessors)
    {
        bool fromConfig = configMemoryBytes is > 0;
        long memory = fromConfig ? configMemoryBytes!.Value : hostMemoryBytes / 2;
        memory = Math.Clamp(memory, 1, Math.Max(1, hostMemoryBytes));

        int processors = configProcessors is > 0 ? configProcessors.Value : hostProcessors;
        processors = Math.Clamp(processors, 1, Math.Max(1, hostProcessors));

        return new WslVmLimits(memory, processors, fromConfig);
    }
}

/// <summary>
/// Turns successive <see cref="WslVmSample"/>s into what the flyout shows: CPU use since the last
/// reading, memory in use, and a short CPU history for the sparkline. Holds only what has been read
/// while it was being fed; nothing is sampled in the background, so the history starts empty each time
/// the flyout opens.
/// </summary>
public sealed class WslVmUsageTracker
{
    /// <summary>How many CPU readings the history keeps (a minute at one reading a second).</summary>
    public const int HistoryLength = 60;

    private readonly List<double> _cpuHistory = new(HistoryLength);
    private WslVmSample? _last;

    /// <summary>True when the last reading found the VM running.</summary>
    public bool IsRunning { get; private set; }

    /// <summary>CPU use between the last two readings, 0 to 100; null until there are two.</summary>
    public double? CpuPercent { get; private set; }

    /// <summary>Memory the VM is using, in bytes; 0 when it isn't running.</summary>
    public long MemoryBytes { get; private set; }

    /// <summary>CPU readings, oldest first, at most <see cref="HistoryLength"/>.</summary>
    public IReadOnlyList<double> CpuHistory => _cpuHistory;

    /// <summary>
    /// Record a reading. A null sample means the VM isn't running, which clears everything: when it
    /// starts again its first reading has nothing to pair with.
    /// </summary>
    /// <param name="processors">The VM's processor limit, the whole that CPU use is a share of.</param>
    public void Add(WslVmSample? sample, int processors)
    {
        if (sample is not { } next)
        {
            Reset();
            return;
        }

        // A new VM process (WSL restarted between readings) has nothing to compare against.
        if (_last is { } previous && previous.ProcessId == next.ProcessId)
        {
            double? percent = CpuPercentBetween(previous, next, processors);
            if (percent is { } p)
            {
                CpuPercent = p;
                AddToHistory(p);
            }
        }
        else
        {
            CpuPercent = null;
            _cpuHistory.Clear();
        }

        _last = next;
        IsRunning = true;
        MemoryBytes = next.PrivateBytes;
    }

    /// <summary>Forget every reading (the flyout closed, or the VM stopped).</summary>
    public void Reset()
    {
        _last = null;
        _cpuHistory.Clear();
        IsRunning = false;
        CpuPercent = null;
        MemoryBytes = 0;
    }

    /// <summary>
    /// The share of <paramref name="processors"/> the VM kept busy between two readings of the same
    /// process, clamped to 0 to 100. Null when no time passed between them, so a reading taken twice in
    /// the same instant can't divide by zero.
    /// </summary>
    public static double? CpuPercentBetween(WslVmSample previous, WslVmSample next, int processors)
    {
        long wall = next.WallTime100ns - previous.WallTime100ns;
        if (wall <= 0)
        {
            return null;
        }

        long cpu = Math.Max(0, next.CpuTime100ns - previous.CpuTime100ns);
        double percent = cpu * 100.0 / (wall * (double)Math.Max(1, processors));
        return Math.Clamp(percent, 0, 100);
    }

    private void AddToHistory(double percent)
    {
        if (_cpuHistory.Count == HistoryLength)
        {
            _cpuHistory.RemoveAt(0);
        }

        _cpuHistory.Add(percent);
    }
}
