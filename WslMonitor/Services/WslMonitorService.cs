using WinUIAppKit.Localization;
using WslMonitor.Classes;
using WslMonitor.Models;

namespace WslMonitor.Services;

/// <summary>
/// Owns the live distro list and every start/stop action. Polls WSL in the background (faster while
/// a window is showing or an action is in progress) and raises <see cref="Changed"/> only when
/// something visible changed, so UIs can rebuild without flicker or losing a pending confirmation.
/// <para>All public members must be used on the UI thread; WSL calls are awaited off it.</para>
/// </summary>
public sealed class WslMonitorService : IDisposable
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    /// <summary>How long a clean shutdown may take before the app offers to force it.</summary>
    public static readonly TimeSpan CleanStopLimit = TimeSpan.FromMinutes(2);

    private static readonly TimeSpan IdleInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ActiveInterval = TimeSpan.FromSeconds(1.5);

    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _wake = new(0, 1);
    private readonly HashSet<object> _viewers = new();
    /// <summary>
    /// Distros whose init system was already probed during their current run. Probing runs a process
    /// inside the distro, so a failed probe must not repeat every poll: on PCs with an idle timeout
    /// that would keep the distro awake. Cleared when the distro stops, so the next boot probes again.
    /// </summary>
    private readonly HashSet<Guid> _initProbed = new();
    private List<Distro> _distros = new();
    private string _lastSignature = "";

    /// <summary>Raised on the UI thread when the distro list, any state, or availability changed.</summary>
    public event Action? Changed;

    /// <summary>Every registered distro, including Docker Desktop's internal ones.</summary>
    public IReadOnlyList<Distro> AllDistros => _distros;

    /// <summary>The distros to show, honouring "show Docker Desktop's distros".</summary>
    public IReadOnlyList<Distro> Distros =>
        _distros.Where(d => SettingsManager.Current.ShowSystemDistros || !d.IsSystem).ToList();

    /// <summary>False when wsl.exe isn't on this PC.</summary>
    public bool IsWslInstalled { get; private set; } = true;

    /// <summary>True when the last poll couldn't get running state from WSL (states shown are the last known).</summary>
    public bool IsStateUnknown { get; private set; }

    /// <summary>True while "Shut down WSL" is in progress; individual actions are disabled meanwhile.</summary>
    public bool IsShuttingDown { get; private set; }

    public int RunningCount => Distros.Count(d => d.State is DistroState.Running or DistroState.Stopping or DistroState.StopTimedOut);

    public Distro? Find(string name) =>
        _distros.FirstOrDefault(d => d.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    // ── Polling ─────────────────────────────────────────────────────────
    /// <summary>Begin background polling (call once, on the UI thread).</summary>
    public void Start() => _ = PollLoopAsync(_cts.Token);

    /// <summary>
    /// Poll faster while <paramref name="viewer"/> (a flyout or window) is showing, so state on screen
    /// is fresh. Pass false when it closes.
    /// </summary>
    public void SetViewerActive(object viewer, bool active)
    {
        bool changed = active ? _viewers.Add(viewer) : _viewers.Remove(viewer);
        if (changed && active) RequestRefresh();
    }

    /// <summary>Poll now instead of waiting for the next interval.</summary>
    public void RequestRefresh()
    {
        if (_wake.CurrentCount == 0) _wake.Release();
    }

    /// <summary>Settings that affect the visible list changed (e.g. show Docker Desktop's distros).</summary>
    public void SettingsChanged() => NotifyIfChanged(force: true);

    private async Task PollLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Refresh failed");
            }

            bool busy = _viewers.Count > 0 || _distros.Any(d => d.IsTransitioning) || IsShuttingDown;
            try
            {
                await _wake.WaitAsync(busy ? ActiveInterval : IdleInterval, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task RefreshAsync()
    {
        IsWslInstalled = WslCli.IsInstalled;
        if (!IsWslInstalled)
        {
            _distros = new();
            NotifyIfChanged();
            return;
        }

        var registered = await Task.Run(WslRegistry.ReadDistros);
        var running = await WslCli.ListRunningAsync(registered.Select(r => r.Name).ToList());
        Apply(registered, running);
    }

    /// <summary>
    /// Merge a fresh registry read and running set into the existing <see cref="Distro"/> objects,
    /// keeping in-progress actions (Starting/Stopping) until WSL confirms them.
    /// </summary>
    /// <param name="running">Running names, or null when WSL couldn't say (states are then kept).</param>
    private void Apply(List<RegisteredDistro> registered, HashSet<string>? running)
    {
        IsStateUnknown = running == null;

        var next = new List<Distro>();
        foreach (var r in registered)
        {
            var d = _distros.FirstOrDefault(x => x.Id == r.Id) ?? new Distro { Id = r.Id, Name = r.Name };
            d.WslVersion = r.WslVersion;
            d.BasePath = r.BasePath;
            d.DiskPath = r.DiskPath;
            d.Flavor = r.Flavor;
            d.OsVersion = r.OsVersion;
            d.IsDefault = r.IsDefault;
            d.BusyReason = r.BusyReason;

            UpdateState(d, running);
            next.Add(d);

            if (d.State == DistroState.Stopped) _initProbed.Remove(d.Id);
            if (d.State == DistroState.Running && d.InitSystem == null) _ = DetectInitSystemAsync(d);
        }

        _distros = next;
        NotifyIfChanged();
    }

    private static void UpdateState(Distro d, HashSet<string>? running)
    {
        if (d.BusyReason.Length > 0)
        {
            d.State = DistroState.Busy;
            return;
        }
        if (running == null)
        {
            if (d.State == DistroState.Busy) d.State = DistroState.Stopped;
            return;
        }

        bool isRunning = running.Contains(d.Name);
        d.State = d.State switch
        {
            DistroState.Starting => isRunning ? DistroState.Running : DistroState.Starting,
            DistroState.Stopping or DistroState.StopTimedOut => isRunning ? d.State : DistroState.Stopped,
            _ => isRunning ? DistroState.Running : DistroState.Stopped,
        };
    }

    /// <summary>Learn PID 1's name once per run, so the UI can say whether Stop will be clean.</summary>
    private async Task DetectInitSystemAsync(Distro d)
    {
        if (!_initProbed.Add(d.Id)) return;
        d.InitSystem = await WslCli.InitSystemAsync(d.Name);
        Logger.Info("{Distro} init system: {Init}", d.Name, d.InitSystem ?? "(unknown)");
        NotifyIfChanged();
    }

    /// <summary>Raise <see cref="Changed"/> when anything a UI shows differs from last time.</summary>
    private void NotifyIfChanged(bool force = false)
    {
        var parts = _distros.Select(d =>
            $"{d.Id}|{d.Name}|{d.State}|{d.IsDefault}|{d.InitSystem}|{d.LastError}|{d.BusyReason}|{d.WslVersion}|{d.OsVersion}");
        string signature = $"{IsWslInstalled}|{IsStateUnknown}|{IsShuttingDown}||{string.Join("||", parts)}";
        if (!force && signature == _lastSignature) return;
        _lastSignature = signature;
        Changed?.Invoke();
    }

    // ── Actions ─────────────────────────────────────────────────────────
    /// <summary>Start a stopped distro. Failures are shown on the distro (<see cref="Distro.LastError"/>).</summary>
    public async Task StartAsync(Distro d)
    {
        if (d.State != DistroState.Stopped || IsShuttingDown) return;

        Logger.Info("Starting {Distro}", d.Name);
        d.State = DistroState.Starting;
        d.LastError = null;
        NotifyIfChanged();

        var result = await WslCli.StartAsync(d.Name);
        if (result.Succeeded)
        {
            d.State = DistroState.Running;
        }
        else
        {
            d.State = DistroState.Stopped;
            d.LastError = new DistroError("error.start", result.Message);
            Logger.Warn("Starting {Distro} failed: {Message}", d.Name, result.Message);
        }
        NotifyIfChanged();
        RequestRefresh();
    }

    /// <summary>
    /// Stop a running distro as cleanly as it allows: <c>systemctl poweroff</c> when it runs systemd
    /// (services stop in order, processes get to exit), otherwise WSL's immediate terminate. Waits up
    /// to <see cref="CleanStopLimit"/>; if it is still running then, the distro moves to
    /// <see cref="DistroState.StopTimedOut"/> and only <see cref="ForceStopAsync"/> is offered.
    /// </summary>
    public async Task StopAsync(Distro d)
    {
        if (d.State != DistroState.Running) return;

        d.State = DistroState.Stopping;
        d.LastError = null;
        NotifyIfChanged();

        d.InitSystem ??= await WslCli.InitSystemAsync(d.Name);
        if (d.HasSystemd)
        {
            Logger.Info("Stopping {Distro} cleanly (systemctl poweroff)", d.Name);
            await WslCli.PowerOffAsync(d.Name);
        }
        else
        {
            Logger.Info("Stopping {Distro} with terminate (init is {Init})", d.Name, d.InitSystem ?? "unknown");
            var result = await WslCli.TerminateAsync(d.Name);
            if (!result.Succeeded) d.LastError = new DistroError("error.stop", result.Message);
        }

        bool stopped = await WaitUntilStoppedAsync(d.Name, CleanStopLimit);
        if (stopped)
        {
            d.State = DistroState.Stopped;
        }
        else
        {
            d.State = DistroState.StopTimedOut;
            Logger.Warn("{Distro} still running after {Seconds}s", d.Name, CleanStopLimit.TotalSeconds);
        }
        NotifyIfChanged();
        RequestRefresh();
    }

    /// <summary>Hard-stop a distro (<c>wsl --terminate</c>). Running processes are killed without warning.</summary>
    public async Task ForceStopAsync(Distro d)
    {
        if (d.State is not (DistroState.Running or DistroState.StopTimedOut)) return;

        Logger.Info("Force-stopping {Distro}", d.Name);
        d.State = DistroState.Stopping;
        d.LastError = null;
        NotifyIfChanged();

        var result = await WslCli.TerminateAsync(d.Name);
        if (!result.Succeeded)
        {
            d.State = DistroState.StopTimedOut;
            d.LastError = new DistroError("error.force-stop", result.Message);
        }
        NotifyIfChanged();
        RequestRefresh();
    }

    /// <summary>
    /// Stop every running distro cleanly (in parallel), then shut down the WSL virtual machine to free
    /// its memory. If any distro misses the clean-stop limit, <paramref name="confirmForce"/> is asked
    /// (with their names) whether to shut down anyway.
    /// </summary>
    /// <param name="progress">Short status lines for the UI, in the current interface language.</param>
    /// <returns>Null on success, otherwise a message (current interface language) explaining why it stopped short.</returns>
    public async Task<string?> ShutdownAllAsync(IProgress<string> progress, Func<IReadOnlyList<string>, Task<bool>> confirmForce)
    {
        if (IsShuttingDown) return UiText.Get("shutdown.in-progress");
        IsShuttingDown = true;
        NotifyIfChanged();

        try
        {
            var running = _distros.Where(d => d.State == DistroState.Running).ToList();
            if (running.Count > 0)
            {
                progress.Report(UiText.Format("shutdown.stopping", ("names", JoinNames(running.Select(d => d.Name)))));
                await Task.WhenAll(running.Select(StopAsync));
            }

            var stuck = _distros.Where(d => d.State is DistroState.StopTimedOut or DistroState.Stopping).Select(d => d.Name).ToList();
            if (stuck.Count > 0 && !await confirmForce(stuck))
                return UiText.Format("shutdown.stuck", ("names", JoinNames(stuck)));

            progress.Report(UiText.Get("status.shutting-down"));
            var result = await WslCli.ShutdownAsync();
            return result.Succeeded ? null : UiText.Format("shutdown.failed", ("detail", result.Message));
        }
        finally
        {
            IsShuttingDown = false;
            NotifyIfChanged();
            RequestRefresh();
        }
    }

    /// <summary>Distro names as a list in the current language ("Ubuntu, Debian"; 「Ubuntu、Debian」).</summary>
    public static string JoinNames(IEnumerable<string> names) => string.Join(UiText.Get("list.separator"), names);

    /// <summary>Make <paramref name="d"/> the default distro.</summary>
    public async Task SetDefaultAsync(Distro d)
    {
        var result = await WslCli.SetDefaultAsync(d.Name);
        d.LastError = result.Succeeded ? null : new DistroError("error.set-default", result.Message);
        NotifyIfChanged();
        RequestRefresh();
    }

    private async Task<bool> WaitUntilStoppedAsync(string name, TimeSpan limit)
    {
        var deadline = DateTime.UtcNow + limit;
        var names = _distros.Select(x => x.Name).ToList();
        while (DateTime.UtcNow < deadline)
        {
            var running = await WslCli.ListRunningAsync(names);
            if (running != null && !running.Contains(name)) return true;
            await Task.Delay(TimeSpan.FromSeconds(1.5));
        }
        return false;
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}
