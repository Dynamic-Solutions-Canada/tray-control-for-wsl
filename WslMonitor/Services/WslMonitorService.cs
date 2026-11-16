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

    /// <summary>
    /// Per distro, bumped whenever an action changes its state. A poll whose WSL answer was taken
    /// before the latest bump is stale for that distro and must not overwrite the action's state.
    /// </summary>
    private readonly Dictionary<Guid, int> _actionStamps = new();

    /// <summary>Stops in progress, so "Shut down WSL" waits for them instead of calling them stuck.</summary>
    private readonly Dictionary<Guid, Task> _stopsInFlight = new();

    /// <summary>How long a force stop may take to show up as stopped before it is reported as failed.</summary>
    private static readonly TimeSpan ForceStopLimit = TimeSpan.FromSeconds(30);
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

    /// <summary>
    /// What "Shut down WSL" is doing, or why the last one stopped short; null when there is nothing to
    /// report. Kept here rather than on the page that started it, so it survives navigation, language
    /// changes and closing the window.
    /// </summary>
    public LocalizedMessage? ShutdownStatus { get; private set; }

    /// <summary>How many listed distros are on (<see cref="Distro.IsOn"/>).</summary>
    public int RunningCount => Distros.Count(d => d.IsOn);

    /// <summary>The registered distro with this name (case-insensitive, as WSL treats names), or null.</summary>
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
        if (changed && active)
        {
            RequestRefresh();
        }
    }

    /// <summary>Poll now instead of waiting for the next interval.</summary>
    public void RequestRefresh()
    {
        if (_wake.CurrentCount == 0)
        {
            _wake.Release();
        }
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

    /// <summary>
    /// One poll: the registry for the list, wsl.exe for what is running. Internal so the state-rules
    /// harness (tests/WslMonitor.State.Tests) can run one poll at a time.
    /// </summary>
    internal async Task RefreshAsync()
    {
        IsWslInstalled = WslCli.IsInstalled;
        if (!IsWslInstalled)
        {
            _distros = new();
            NotifyIfChanged();
            return;
        }

        // Taken before any await: actions that start or finish while WSL is being asked make this
        // poll's answer stale for their distro (see Apply).
        var stamps = new Dictionary<Guid, int>(_actionStamps);

        var registered = await Task.Run(WslRegistry.ReadDistros);

        // With nothing registered there is nothing to ask; don't start wsl.exe every few seconds.
        var running = registered.Count == 0
            ? new HashSet<string>()
            : await WslCli.ListRunningAsync(registered.Select(r => r.Name).ToList());
        Apply(registered, running, stamps);
    }

    /// <summary>
    /// Merge a fresh registry read and running set into the existing <see cref="Distro"/> objects,
    /// keeping in-progress actions (Starting/Stopping) until WSL confirms them.
    /// </summary>
    /// <param name="running">Running names, or null when WSL couldn't say (states are then kept).</param>
    /// <param name="stampsAtStart"><see cref="_actionStamps"/> when the poll began.</param>
    private void Apply(List<RegisteredDistro> registered, HashSet<string>? running, Dictionary<Guid, int> stampsAtStart)
    {
        IsStateUnknown = running == null;

        var next = new List<Distro>();
        foreach (var r in registered)
        {
            var d = _distros.FirstOrDefault(x => x.Id == r.Id) ?? new Distro { Id = r.Id };
            d.Name = r.Name;
            d.WslVersion = r.WslVersion;
            d.BasePath = r.BasePath;
            d.DiskPath = r.DiskPath;
            d.Flavor = r.Flavor;
            d.OsVersion = r.OsVersion;
            d.IsDefault = r.IsDefault;
            d.BusyReason = r.BusyReason;

            bool stale = StampOf(_actionStamps, d.Id) != StampOf(stampsAtStart, d.Id);
            if (!stale)
            {
                UpdateState(d, running);
            }

            next.Add(d);

            // Probe once per run: the init system can change between runs (systemd enabled in wsl.conf).
            if (d.State == DistroState.Stopped)
            {
                _initProbed.Remove(d.Id);
            }

            if (d.State == DistroState.Running && !_initProbed.Contains(d.Id))
            {
                _ = DetectInitSystemAsync(d);
            }
        }

        _distros = next;
        NotifyIfChanged();
    }

    private static int StampOf(Dictionary<Guid, int> stamps, Guid id) => stamps.GetValueOrDefault(id);

    /// <summary>Record that an action changed <paramref name="d"/>'s state (see <see cref="_actionStamps"/>).</summary>
    private void Stamp(Distro d) => _actionStamps[d.Id] = StampOf(_actionStamps, d.Id) + 1;

    /// <summary>
    /// The state machine for one poll. Registry busy states win. When WSL couldn't answer, keep what
    /// we had. Starting holds until WSL lists it running; Stopping and StopTimedOut hold until WSL no
    /// longer lists it; otherwise the state is simply running or stopped.
    /// </summary>
    private static void UpdateState(Distro d, HashSet<string>? running)
    {
        if (d.BusyReason.Length > 0)
        {
            d.State = DistroState.Busy;
            return;
        }
        if (running == null)
        {
            if (d.State == DistroState.Busy)
            {
                d.State = DistroState.Stopped;
            }

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

    /// <summary>
    /// Learn PID 1's name once per run, so the UI can say whether Stop will be clean. A failed probe
    /// keeps the previous run's answer for display; <see cref="StopAsync"/> always checks afresh.
    /// </summary>
    private async Task DetectInitSystemAsync(Distro d)
    {
        if (!_initProbed.Add(d.Id))
        {
            return;
        }

        string? init = await WslCli.InitSystemAsync(d.Name);
        if (init != null)
        {
            d.InitSystem = init;
        }

        Logger.Info("{Distro} init system: {Init}", d.Name, init ?? "(couldn't check)");
        NotifyIfChanged();
    }

    /// <summary>Raise <see cref="Changed"/> when anything a UI shows differs from last time.</summary>
    private void NotifyIfChanged(bool force = false)
    {
        var parts = _distros.Select(d =>
            $"{d.Id}|{d.Name}|{d.State}|{d.IsDefault}|{d.InitSystem}|{d.LastError}|{d.BusyReason}|{d.WslVersion}|{d.OsVersion}");
        string signature = $"{IsWslInstalled}|{IsStateUnknown}|{IsShuttingDown}|{ShutdownStatus}||{string.Join("||", parts)}";
        if (!force && signature == _lastSignature)
        {
            return;
        }

        _lastSignature = signature;
        Changed?.Invoke();
    }

    // ── Actions ─────────────────────────────────────────────────────────
    /// <summary>Start a stopped distro. Failures are shown on the distro (<see cref="Distro.LastError"/>).</summary>
    public async Task StartAsync(Distro d)
    {
        if (d.State != DistroState.Stopped || IsShuttingDown)
        {
            return;
        }

        Logger.Info("Starting {Distro}", d.Name);
        d.State = DistroState.Starting;
        d.LastError = null;
        Stamp(d);
        NotifyIfChanged();

        var result = await WslCli.StartAsync(d.Name);

        // Only a distro still marked Starting gets this outcome (a poll may have seen it run already).
        if (d.State == DistroState.Starting)
        {
            d.State = result.Succeeded ? DistroState.Running : DistroState.Stopped;
            Stamp(d);
        }
        if (!result.Succeeded)
        {
            d.LastError = new LocalizedMessage("error.start", ("detail", result));
            Logger.Warn("Starting {Distro} failed: {Message}", d.Name, result.Message);
        }
        NotifyIfChanged();
        RequestRefresh();
    }

    /// <summary>
    /// Stop a running distro as cleanly as it allows: <c>systemctl poweroff</c> when it runs systemd
    /// (services stop in order, processes get to exit), otherwise WSL's immediate terminate. The init
    /// system is checked afresh first; if that check fails, nothing is stopped (a hard kill is never an
    /// automatic fallback for "couldn't tell"): the distro moves straight to
    /// <see cref="DistroState.StopTimedOut"/> with the error, so the user can choose Force stop. Waits up to
    /// <see cref="CleanStopLimit"/>; if it is still running then, the distro moves to
    /// <see cref="DistroState.StopTimedOut"/> and only <see cref="ForceStopAsync"/> is offered.
    /// </summary>
    public Task StopAsync(Distro d)
    {
        if (_stopsInFlight.TryGetValue(d.Id, out var inFlight))
        {
            return inFlight;
        }

        if (d.State != DistroState.Running)
        {
            return Task.CompletedTask;
        }

        return TrackStopAsync(d);
    }

    /// <summary>Run <see cref="StopCoreAsync"/> and keep it in <see cref="_stopsInFlight"/> while it runs.</summary>
    private async Task TrackStopAsync(Distro d)
    {
        var stop = StopCoreAsync(d);
        _stopsInFlight[d.Id] = stop;
        try
        {
            await stop;
        }
        finally
        {
            _stopsInFlight.Remove(d.Id);
        }
    }

    private async Task StopCoreAsync(Distro d)
    {
        d.State = DistroState.Stopping;
        d.LastError = null;
        Stamp(d);
        NotifyIfChanged();

        // 1. Which init is running now (it can change between runs).
        string? init = await WslCli.InitSystemAsync(d.Name);
        if (init == null)
        {
            Logger.Warn("Not stopping {Distro}: couldn't check its init system; offering Force stop", d.Name);
            if (d.State == DistroState.Stopping)
            {
                d.State = DistroState.StopTimedOut;
                Stamp(d);
            }
            d.LastError = new LocalizedMessage("error.init-check", ("name", d.Name));
            NotifyIfChanged();
            RequestRefresh();
            return;
        }
        d.InitSystem = init;

        // 2. Ask it to stop.
        if (d.HasSystemd)
        {
            Logger.Info("Stopping {Distro} cleanly (systemctl poweroff)", d.Name);
            var poweroff = await WslCli.PowerOffAsync(d.Name);

            // wsl.exe often exits non-zero here because the distro goes away under it, so a failure is
            // only logged; the wait below decides whether the stop worked.
            if (!poweroff.Succeeded)
            {
                Logger.Info("systemctl poweroff for {Distro}: {Message}", d.Name, poweroff.Message);
            }
        }
        else
        {
            Logger.Info("Stopping {Distro} with terminate (init is {Init})", d.Name, init);
            var terminate = await WslCli.TerminateAsync(d.Name);
            if (!terminate.Succeeded)
            {
                RestoreRunning(d, new LocalizedMessage("error.stop", ("detail", terminate)));
                return;
            }
        }

        // 3. Wait for WSL to report it stopped. Polls may change the state meanwhile (a restart by
        // another program); only a distro still marked Stopping gets this action's outcome.
        bool stopped = await WaitUntilStoppedAsync(d.Name, CleanStopLimit);
        if (d.State == DistroState.Stopping)
        {
            if (stopped)
            {
                d.State = DistroState.Stopped;
            }
            else
            {
                d.State = DistroState.StopTimedOut;
                Logger.Warn("{Distro} still running after {Seconds}s", d.Name, CleanStopLimit.TotalSeconds);
            }
            Stamp(d);
        }
        NotifyIfChanged();
        RequestRefresh();
    }

    /// <summary>An action failed before anything was stopped: show the distro as running again, with the error.</summary>
    private void RestoreRunning(Distro d, LocalizedMessage error)
    {
        if (d.State == DistroState.Stopping)
        {
            d.State = DistroState.Running;
            Stamp(d);
        }
        d.LastError = error;
        NotifyIfChanged();
        RequestRefresh();
    }

    /// <summary>
    /// Hard-stop a distro (<c>wsl --terminate</c>). Running processes are killed without warning. If
    /// WSL still lists it after <see cref="ForceStopLimit"/>, it goes back to
    /// <see cref="DistroState.StopTimedOut"/> so Force stop is offered again rather than spinning forever.
    /// </summary>
    public async Task ForceStopAsync(Distro d)
    {
        if (d.State is not (DistroState.Running or DistroState.StopTimedOut))
        {
            return;
        }

        Logger.Info("Force-stopping {Distro}", d.Name);
        d.State = DistroState.Stopping;
        d.LastError = null;
        Stamp(d);
        NotifyIfChanged();

        var result = await WslCli.TerminateAsync(d.Name);
        bool stopped = result.Succeeded && await WaitUntilStoppedAsync(d.Name, ForceStopLimit);

        if (d.State == DistroState.Stopping)
        {
            d.State = stopped ? DistroState.Stopped : DistroState.StopTimedOut;
            Stamp(d);
        }
        if (!result.Succeeded)
        {
            d.LastError = new LocalizedMessage("error.force-stop", ("detail", result));
        }

        NotifyIfChanged();
        RequestRefresh();
    }

    /// <summary>
    /// Stop every running distro cleanly (in parallel), then shut down the WSL virtual machine to free
    /// its memory. If any distro misses the clean-stop limit, <paramref name="confirmForce"/> is asked
    /// (with their names) whether to shut down anyway. Progress and the outcome are published in
    /// <see cref="ShutdownStatus"/>.
    /// </summary>
    /// <param name="confirmForce">Asks the user; returns false (don't force) when it can't ask.</param>
    public async Task ShutdownAllAsync(Func<IReadOnlyList<string>, Task<bool>> confirmForce)
    {
        if (IsShuttingDown)
        {
            return;
        }

        try
        {
            IsShuttingDown = true;
            ShutdownStatus = null;
            NotifyIfChanged();

            // 1. Clean-stop everything that is running, and wait for stops already under way.
            var running = _distros.Where(d => d.State == DistroState.Running).ToList();
            var stopping = _distros.Where(d => _stopsInFlight.ContainsKey(d.Id)).ToList();
            var waitFor = running.Concat(stopping).ToList();
            if (waitFor.Count > 0)
            {
                ShutdownStatus = new LocalizedMessage("shutdown.stopping", ("names", Names(waitFor.Select(d => d.Name))));
                NotifyIfChanged();
                await Task.WhenAll(waitFor.Select(StopAsync));
            }

            // 2. Anything that didn't stop needs the user's go-ahead before it is cut off.
            // Stuck: missed the limit, or couldn't be stopped cleanly at all (still running).
            var stuck = _distros
                .Where(d => d.State is DistroState.StopTimedOut or DistroState.Stopping
                            || (waitFor.Contains(d) && d.State == DistroState.Running))
                .Select(d => d.Name)
                .ToList();
            if (stuck.Count > 0 && !await confirmForce(stuck))
            {
                ShutdownStatus = new LocalizedMessage("shutdown.stuck", ("names", Names(stuck)));
                return;
            }

            // 3. Shut down the VM.
            ShutdownStatus = new LocalizedMessage("status.shutting-down");
            NotifyIfChanged();
            var result = await WslCli.ShutdownAsync();
            ShutdownStatus = result.Succeeded ? null : new LocalizedMessage("shutdown.failed", ("detail", result));
            Logger.Info("Shut down WSL: {Result}", result.Succeeded ? "done" : result.Message);
        }
        finally
        {
            IsShuttingDown = false;
            NotifyIfChanged();
            RequestRefresh();
        }
    }

    /// <summary>Distro names as a <see cref="LocalizedMessage"/> value, joined in whatever language it is shown in.</summary>
    private static NameList Names(IEnumerable<string> names) => new(names.ToList());

    /// <summary>Make <paramref name="d"/> the default distro.</summary>
    public async Task SetDefaultAsync(Distro d)
    {
        var result = await WslCli.SetDefaultAsync(d.Name);
        d.LastError = result.Succeeded ? null : new LocalizedMessage("error.set-default", ("detail", result));
        NotifyIfChanged();
        RequestRefresh();
    }

    /// <summary>
    /// Ask WSL every 1.5 s until <paramref name="name"/> is no longer running. Returns false if it still
    /// is (or WSL never answered) after <paramref name="limit"/>.
    /// </summary>
    private async Task<bool> WaitUntilStoppedAsync(string name, TimeSpan limit)
    {
        var deadline = DateTime.UtcNow + limit;
        var names = _distros.Select(x => x.Name).ToList();
        while (DateTime.UtcNow < deadline)
        {
            var running = await WslCli.ListRunningAsync(names);
            if (running != null && !running.Contains(name))
            {
                return true;
            }

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
