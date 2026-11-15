namespace WslMonitor.Models;

/// <summary>What a distro is doing, as far as the app can tell.</summary>
public enum DistroState
{
    /// <summary>Installed and not running.</summary>
    Stopped,

    /// <summary>Running (listed by <c>wsl --list --running</c>).</summary>
    Running,

    /// <summary>This app asked it to start and it hasn't shown up as running yet.</summary>
    Starting,

    /// <summary>This app asked it to shut down and it is still running.</summary>
    Stopping,

    /// <summary>A clean shutdown didn't finish within the time limit; only a force stop remains.</summary>
    StopTimedOut,

    /// <summary>WSL is installing, converting or removing it; start/stop are unavailable.</summary>
    Busy,
}

/// <summary>
/// One registered WSL distro: identity and on-disk facts from the registry, plus live state. Instances
/// are owned and mutated by <see cref="Services.WslMonitorService"/> on the UI thread only.
/// </summary>
public sealed class Distro
{
    public Guid Id { get; init; }
    public string Name { get; init; } = "";

    /// <summary>1 or 2 (WSL 1 runs without a VM).</summary>
    public int WslVersion { get; set; }

    /// <summary>Folder holding the distro's disk (Windows path).</summary>
    public string BasePath { get; set; } = "";

    /// <summary>Full path of the virtual disk (WSL 2), or empty.</summary>
    public string DiskPath { get; set; } = "";

    /// <summary>Distro family and release from the registry (e.g. "ubuntu", "26.04"); may be empty.</summary>
    public string Flavor { get; set; } = "";
    public string OsVersion { get; set; } = "";

    public bool IsDefault { get; set; }

    /// <summary>
    /// Docker Desktop's own distros (docker-desktop, docker-desktop-data). Hidden by default: stopping
    /// them from here confuses Docker Desktop, which manages them itself.
    /// </summary>
    public bool IsSystem => Name.StartsWith("docker-desktop", StringComparison.OrdinalIgnoreCase);

    public DistroState State { get; set; }

    /// <summary>Why <see cref="DistroState.Busy"/> (e.g. "Installing").</summary>
    public string BusyReason { get; set; } = "";

    /// <summary>
    /// Name of PID 1 inside the distro ("systemd", "init"), learned the first time it is seen running;
    /// null until then. Decides whether a clean shutdown is possible.
    /// </summary>
    public string? InitSystem { get; set; }

    /// <summary>True when the distro boots systemd, so <c>systemctl poweroff</c> can stop it cleanly.</summary>
    public bool HasSystemd => string.Equals(InitSystem, "systemd", StringComparison.Ordinal);

    /// <summary>The last start/stop failure, shown until the next successful action.</summary>
    public string? LastError { get; set; }

    /// <summary>"Ubuntu 26.04" style label, or empty when the registry has no release info.</summary>
    public string OsLabel
    {
        get
        {
            if (Flavor.Length == 0) return OsVersion;
            string family = char.ToUpperInvariant(Flavor[0]) + Flavor[1..];
            return OsVersion.Length > 0 ? $"{family} {OsVersion}" : family;
        }
    }

    /// <summary>True for states where the app is waiting on WSL and start/stop must not be offered.</summary>
    public bool IsTransitioning => State is DistroState.Starting or DistroState.Stopping;
}
