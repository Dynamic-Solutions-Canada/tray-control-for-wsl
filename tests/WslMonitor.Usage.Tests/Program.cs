// Portable harness for the flyout's WSL VM usage figures: CPU share between readings, the history the
// sparkline draws, memory, the VM's limits, and the .wslconfig sizes they come from. Exits non-zero on
// the first failed check.
using WslMonitor.Services;

const long Second = 10_000_000;   // 100 ns units
const long Gb = 1L << 30;

var scenarios = new List<(string Name, Action Body)>
{
    ("CPU use is the share of the VM's processors kept busy", CpuShareOfProcessors),
    ("CPU use stays within 0 to 100%", CpuClamped),
    ("two readings at the same instant give no CPU figure", CpuNoElapsedTime),
    ("the first reading has memory but no CPU figure yet", FirstReading),
    ("the history keeps the last minute, oldest first", HistoryKeepsLastMinute),
    ("a restarted VM starts its CPU figure and history again", RestartedVm),
    ("a stopped VM clears every figure", StoppedVm),
    ("limits default to half the PC's memory and every processor", DefaultLimits),
    ("limits from .wslconfig are used, but never above the PC's", ConfigLimits),
    (".wslconfig sizes read as WSL reads them", Sizes),
    (".wslconfig memory and processors are read from [wsl2] only", ConfigFileRead),
};

foreach (var (name, body) in scenarios)
{
    body();
    Console.WriteLine("PASS: " + name);
}
Console.WriteLine($"All {scenarios.Count} scenarios passed.");
return 0;

// ── Scenarios ───────────────────────────────────────────────────────────

static void CpuShareOfProcessors()
{
    var a = Sample(cpu: 0, wall: 0);
    var b = Sample(cpu: 2 * Second, wall: Second);   // two cores busy for a second
    Check(Near(WslVmUsageTracker.CpuPercentBetween(a, b, 8), 25), "2 of 8 processors is 25%");
    Check(Near(WslVmUsageTracker.CpuPercentBetween(a, b, 2), 100), "2 of 2 processors is 100%");
}

static void CpuClamped()
{
    var a = Sample(cpu: 0, wall: 0);
    Check(Near(WslVmUsageTracker.CpuPercentBetween(a, Sample(cpu: 10 * Second, wall: Second), 4), 100), "more than the limit reads 100%");
    Check(Near(WslVmUsageTracker.CpuPercentBetween(Sample(cpu: Second, wall: 0), Sample(cpu: 0, wall: Second), 4), 0), "CPU time going backwards reads 0%");
}

static void CpuNoElapsedTime()
{
    var a = Sample(cpu: 0, wall: Second);
    Check(WslVmUsageTracker.CpuPercentBetween(a, a with { CpuTime100ns = Second }, 4) == null, "no figure without elapsed time");
}

static void FirstReading()
{
    var usage = new WslVmUsageTracker();
    usage.Add(Sample(cpu: 0, wall: 0, memory: 3 * Gb), 4);
    Check(usage.IsRunning, "running after a reading");
    Check(usage.CpuPercent == null, "no CPU figure from one reading");
    Check(usage.MemoryBytes == 3 * Gb, "memory from the first reading");
    Check(usage.CpuHistory.Count == 0, "no history from one reading");
}

static void HistoryKeepsLastMinute()
{
    var usage = new WslVmUsageTracker();
    int readings = WslVmUsageTracker.HistoryLength + 10;
    for (int i = 0; i <= readings; i++)
    {
        // Reading i adds i% of one processor over the second before it.
        long cpu = Enumerable.Range(1, i).Sum(n => (long)n) * Second / 100;
        usage.Add(Sample(cpu: cpu, wall: i * Second), 1);
    }

    Check(usage.CpuHistory.Count == WslVmUsageTracker.HistoryLength, "history is capped");
    Check(Near(usage.CpuHistory[^1], readings), "newest reading is last");
    Check(Near(usage.CpuHistory[0], readings - WslVmUsageTracker.HistoryLength + 1), "oldest kept reading is first");
}

static void RestartedVm()
{
    var usage = new WslVmUsageTracker();
    usage.Add(Sample(cpu: 0, wall: 0), 1);
    usage.Add(Sample(cpu: Second / 2, wall: Second), 1);
    Check(Near(usage.CpuPercent, 50), "50% before the restart");

    usage.Add(Sample(cpu: 0, wall: 2 * Second, pid: 99), 1);
    Check(usage.CpuPercent == null, "a new VM process has no CPU figure yet");
    Check(usage.CpuHistory.Count == 0, "a new VM process starts its history again");
}

static void StoppedVm()
{
    var usage = new WslVmUsageTracker();
    usage.Add(Sample(cpu: 0, wall: 0, memory: Gb), 1);
    usage.Add(Sample(cpu: Second, wall: Second, memory: Gb), 1);
    usage.Add(null, 1);
    Check(!usage.IsRunning && usage.CpuPercent == null && usage.MemoryBytes == 0 && usage.CpuHistory.Count == 0, "everything cleared");
}

static void DefaultLimits()
{
    var limits = WslVmLimits.From(null, null, 32 * Gb, 16);
    Check(limits.MemoryBytes == 16 * Gb, "half of 32 GB");
    Check(limits.Processors == 16, "every processor");
    Check(!limits.MemoryFromConfig, "the memory limit is WSL's default");
}

static void ConfigLimits()
{
    var set = WslVmLimits.From(8 * Gb, 4, 32 * Gb, 16);
    Check(set.MemoryBytes == 8 * Gb && set.Processors == 4 && set.MemoryFromConfig, ".wslconfig values used");

    var tooBig = WslVmLimits.From(64 * Gb, 32, 32 * Gb, 16);
    Check(tooBig.MemoryBytes == 32 * Gb && tooBig.Processors == 16, "capped at what the PC has");

    var zero = WslVmLimits.From(0, 0, 32 * Gb, 16);
    Check(zero.MemoryBytes == 16 * Gb && zero.Processors == 16 && !zero.MemoryFromConfig, "zero reads as unset");
}

static void Sizes()
{
    Check(WslConfigFile.ParseSize("8GB") == 8 * Gb, "8GB");
    Check(WslConfigFile.ParseSize("8gb") == 8 * Gb, "units in any case");
    Check(WslConfigFile.ParseSize("8G") == 8 * Gb, "8G");
    Check(WslConfigFile.ParseSize("512MB") == 512L << 20, "512MB");
    Check(WslConfigFile.ParseSize("1.5GB") == 3 * Gb / 2, "1.5GB");
    Check(WslConfigFile.ParseSize("1048576") == 1L << 20, "plain bytes");
    Check(WslConfigFile.ParseSize(" 4 GB ") == 4 * Gb, "spaces around and before the unit");
    Check(WslConfigFile.ParseSize("eight") == null, "not a size");
    Check(WslConfigFile.ParseSize("8XB") == null, "unknown unit");
    Check(WslConfigFile.ParseSize("") == null, "empty");
}

static void ConfigFileRead()
{
    string home = Path.Combine(Path.GetTempPath(), "wslmonitor-usage-tests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(home);
    string? previousHome = Environment.GetEnvironmentVariable("HOME");
    try
    {
        Environment.SetEnvironmentVariable("HOME", home);
        File.WriteAllText(Path.Combine(home, ".wslconfig"), """
            [general]
            memory=2GB
            instanceIdleTimeout=-1

            [wsl2]
            memory=6GB   # comment
            processors=4
            vmIdleTimeout=60000
            """);

        var config = WslConfigFile.Read();
        Check(config.MemoryBytes == 6 * Gb, "memory from [wsl2], comment stripped");
        Check(config.Processors == 4, "processors from [wsl2]");
        Check(config.InstanceIdleTimeoutMs == -1 && config.VmIdleTimeoutMs == 60000, "idle timeouts still read");

        File.WriteAllText(Path.Combine(home, ".wslconfig"), "[wsl2]\nmemory=lots\nprocessors=many\n");
        var unreadable = WslConfigFile.Read();
        Check(unreadable.MemoryBytes == null && unreadable.Processors == null, "unreadable values read as unset");
    }
    finally
    {
        Environment.SetEnvironmentVariable("HOME", previousHome);
        Directory.Delete(home, recursive: true);
    }
}

// ── Helpers ─────────────────────────────────────────────────────────────

static WslVmSample Sample(long cpu, long wall, long memory = 1L << 30, long pid = 42) =>
    new(pid, cpu, memory, wall);

static bool Near(double? actual, double expected) => actual is { } a && Math.Abs(a - expected) < 1e-9;

static void Check(bool condition, string what)
{
    if (!condition)
    {
        throw new Exception("FAILED: " + what);
    }
}
