// Portable harness for WslMonitorService's action and polling state rules (AGENTS.md "Behaviour" and
// "Actions and polling"). It links the real service and Distro model and drives them with the
// stand-ins in Fakes.cs, one scenario at a time on a single thread. Exits non-zero on the first
// failed check.
using WslMonitor.Models;
using WslMonitor.Services;
using WslMonitor.State.Tests;

var scenarios = new List<(string Name, Func<Task> Body)>
{
    ("a poll lists registered distros and their running state", PollListsDistros),
    ("Changed is raised only when something visible changed", ChangedOnlyOnChange),
    ("the service says it has not polled until its first poll finishes", HasPolledAfterFirstPoll),
    ("Docker Desktop's distros are hidden unless the setting is on", SystemDistrosHidden),
    ("a registry busy state wins over running state", BusyWins),
    ("when WSL can't say what is running, the last state is kept", UnknownKeepsState),
    ("Starting holds until WSL lists the distro running", StartingHolds),
    ("a failed start leaves the distro stopped with the error", FailedStart),
    ("an action's outcome is not written over a state a poll set", OutcomeOnlyIfStillInState),
    ("a poll that began before an action leaves that distro alone", StalePollIgnored),
    ("Stop uses systemctl poweroff for systemd distros", StopWithSystemd),
    ("Stop uses terminate for distros without systemd", StopWithoutSystemd),
    ("Stop never terminates when the init check fails", StopInitCheckFails),
    ("a failed terminate puts the distro back to running", StopTerminateFails),
    ("Force stop ends a distro that missed its clean stop", ForceStopFromTimedOut),
    ("Shut down WSL stops every running distro, then WSL", ShutdownAllClean),
    ("Shut down WSL asks before forcing, and stops if declined", ShutdownStuckDeclined),
    ("Shut down WSL forces stuck distros when the user agrees", ShutdownStuckConfirmed),
};

foreach (var (name, body) in scenarios)
{
    Fakes.Reset();
    SingleThreadContext.Run(body, TimeSpan.FromSeconds(10));
    Console.WriteLine("PASS: " + name);
}
Console.WriteLine($"All {scenarios.Count} scenarios passed.");
return 0;

// ── Helpers ─────────────────────────────────────────────────────────────

static void Check(bool condition, string what)
{
    if (!condition)
    {
        throw new Exception("FAILED: " + what);
    }
}

/// <summary>A service that has polled once, so the registered distros exist as Distro objects.</summary>
static async Task<WslMonitorService> PolledService()
{
    var service = new WslMonitorService();
    await service.RefreshAsync();
    return service;
}

static Distro Get(WslMonitorService service, string name) =>
    service.Find(name) ?? throw new Exception("No distro " + name);

// ── Polling ─────────────────────────────────────────────────────────────

static async Task PollListsDistros()
{
    Fakes.Register("Ubuntu");
    Fakes.Register("Debian");
    WslCli.Running = new() { "Ubuntu" };
    var service = await PolledService();
    Check(Get(service, "Ubuntu").State == DistroState.Running, "Ubuntu is running");
    Check(Get(service, "Debian").State == DistroState.Stopped, "Debian is stopped");
    Check(service.RunningCount == 1, "one distro counts as running");
}

static async Task ChangedOnlyOnChange()
{
    Fakes.Register("Ubuntu");
    var service = new WslMonitorService();
    int changes = 0;
    service.Changed += () => changes++;
    await service.RefreshAsync();
    Check(changes == 1, "the first poll raises Changed");
    await service.RefreshAsync();
    Check(changes == 1, "an identical poll does not raise Changed");
    WslCli.Running = new() { "Ubuntu" };
    await service.RefreshAsync();
    Check(changes == 2, "a state change raises Changed");
}

static async Task HasPolledAfterFirstPoll()
{
    // Nothing registered: before the first poll an empty list must not read as "none installed".
    var service = new WslMonitorService();
    int changes = 0;
    service.Changed += () => changes++;
    Check(!service.HasPolled && service.Distros.Count == 0, "a new service has not polled and lists nothing");
    await service.RefreshAsync();
    Check(service.HasPolled, "the first poll marks the service as polled");
    Check(changes == 1, "the first poll raises Changed even when the list stays empty");
}

static async Task SystemDistrosHidden()
{
    Fakes.Register("Ubuntu");
    Fakes.Register("docker-desktop");
    var service = await PolledService();
    Check(service.Distros.Count == 1 && service.Distros[0].Name == "Ubuntu", "docker-desktop is hidden");
    WslMonitor.Classes.SettingsManager.Current.ShowSystemDistros = true;
    Check(service.Distros.Count == 2, "docker-desktop is listed with the setting on");
}

static async Task BusyWins()
{
    var registered = Fakes.Register("Ubuntu", busyReason: "state.installing");
    WslCli.Running = new() { "Ubuntu" };
    var service = await PolledService();
    var d = Get(service, "Ubuntu");
    Check(d.State == DistroState.Busy, "a busy registration shows Busy even while running");

    WslRegistry.Distros[0] = registered with { BusyReason = "" };
    await service.RefreshAsync();
    Check(d.State == DistroState.Running, "once WSL is done, the running state shows");
}

static async Task UnknownKeepsState()
{
    Fakes.Register("Ubuntu");
    WslCli.Running = new() { "Ubuntu" };
    var service = await PolledService();
    WslCli.Running = null;
    await service.RefreshAsync();
    Check(service.IsStateUnknown, "the service says the state is unknown");
    Check(Get(service, "Ubuntu").State == DistroState.Running, "the last known state is kept");
}

// ── Start ───────────────────────────────────────────────────────────────

static async Task StartingHolds()
{
    Fakes.Register("Ubuntu");
    var service = await PolledService();
    var d = Get(service, "Ubuntu");
    var started = new TaskCompletionSource<WslResult>();
    WslCli.Start = _ => started.Task;

    var start = service.StartAsync(d);
    Check(d.State == DistroState.Starting, "Start shows Starting at once");
    await service.RefreshAsync();
    Check(d.State == DistroState.Starting, "a poll that doesn't list it yet keeps Starting");

    WslCli.Running!.Add("Ubuntu");
    started.SetResult(WslCli.Ok);
    await start;
    Check(d.State == DistroState.Running, "a successful start ends Running");
}

static async Task FailedStart()
{
    Fakes.Register("Ubuntu");
    var service = await PolledService();
    var d = Get(service, "Ubuntu");
    WslCli.Start = _ => Task.FromResult(WslCli.Failed("boom"));
    await service.StartAsync(d);
    Check(d.State == DistroState.Stopped, "a failed start ends Stopped");
    Check(d.LastError?.Key == "error.start", "the start error is shown on the distro");
}

static async Task OutcomeOnlyIfStillInState()
{
    Fakes.Register("Ubuntu");
    var service = await PolledService();
    var d = Get(service, "Ubuntu");
    var started = new TaskCompletionSource<WslResult>();
    WslCli.Start = _ => started.Task;

    var start = service.StartAsync(d);
    WslCli.Running!.Add("Ubuntu");
    await service.RefreshAsync();
    Check(d.State == DistroState.Running, "a poll that sees it running moves it to Running");

    // wsl.exe then reports a failure (it often exits non-zero for a distro that did come up).
    started.SetResult(WslCli.Failed("late failure"));
    await start;
    Check(d.State == DistroState.Running, "the late failure does not overwrite Running with Stopped");
    Check(d.LastError?.Key == "error.start", "the failure is still shown");
}

static async Task StalePollIgnored()
{
    Fakes.Register("Ubuntu");
    var service = await PolledService();
    var d = Get(service, "Ubuntu");

    // A poll asks WSL what is running and is answered "nothing" from before the start below.
    var answer = new TaskCompletionSource<HashSet<string>?>();
    WslCli.NextListRunning = answer;
    var poll = service.RefreshAsync();

    await service.StartAsync(d);
    Check(d.State == DistroState.Running, "the start completes while the poll is waiting");

    answer.SetResult(new HashSet<string>());
    await poll;
    Check(d.State == DistroState.Running, "the stale answer does not mark it Stopped");
}

// ── Stop and Force stop ─────────────────────────────────────────────────

static async Task<(WslMonitorService Service, Distro Distro)> RunningDistro(string name, string? init)
{
    Fakes.Register(name);
    WslCli.Running = new() { name };
    if (init != null)
    {
        WslCli.InitSystems[name] = init;
    }

    var service = await PolledService();
    return (service, Get(service, name));
}

static async Task StopWithSystemd()
{
    var (service, d) = await RunningDistro("Ubuntu", "systemd");
    await service.StopAsync(d);
    Check(d.State == DistroState.Stopped, "the distro ends Stopped");
    Check(WslCli.Calls.Contains("poweroff:Ubuntu"), "systemctl poweroff was used");
    Check(!WslCli.Calls.Contains("terminate:Ubuntu"), "terminate was not used");
}

static async Task StopWithoutSystemd()
{
    var (service, d) = await RunningDistro("Alpine", "init");
    await service.StopAsync(d);
    Check(d.State == DistroState.Stopped, "the distro ends Stopped");
    Check(WslCli.Calls.Contains("terminate:Alpine"), "terminate was used");
    Check(!WslCli.Calls.Contains("poweroff:Alpine"), "poweroff was not used");
}

static async Task StopInitCheckFails()
{
    var (service, d) = await RunningDistro("Ubuntu", init: null);
    await service.StopAsync(d);
    Check(d.State == DistroState.StopTimedOut, "the distro is offered Force stop");
    Check(d.LastError?.Key == "error.init-check", "the init check error is shown");
    Check(!WslCli.Calls.Any(c => c.StartsWith("terminate:") || c.StartsWith("poweroff:")), "nothing was stopped");
}

static async Task StopTerminateFails()
{
    var (service, d) = await RunningDistro("Alpine", "init");
    WslCli.Terminate = _ => Task.FromResult(WslCli.Failed("denied"));
    await service.StopAsync(d);
    Check(d.State == DistroState.Running, "the distro shows Running again");
    Check(d.LastError?.Key == "error.stop", "the stop error is shown");
}

static async Task ForceStopFromTimedOut()
{
    var (service, d) = await RunningDistro("Ubuntu", init: null);
    await service.StopAsync(d);
    Check(d.State == DistroState.StopTimedOut, "set up: the clean stop could not run");
    await service.ForceStopAsync(d);
    Check(d.State == DistroState.Stopped, "Force stop ends Stopped");
    Check(WslCli.Calls.Contains("terminate:Ubuntu"), "Force stop used terminate");
}

// ── Shut down WSL ───────────────────────────────────────────────────────

static async Task<WslMonitorService> TwoRunning(string? debianInit)
{
    Fakes.Register("Ubuntu");
    Fakes.Register("Debian");
    WslCli.Running = new() { "Ubuntu", "Debian" };
    WslCli.InitSystems["Ubuntu"] = "systemd";
    if (debianInit != null)
    {
        WslCli.InitSystems["Debian"] = debianInit;
    }

    return await PolledService();
}

static async Task ShutdownAllClean()
{
    var service = await TwoRunning("systemd");
    bool asked = false;
    await service.ShutdownAllAsync(_ => { asked = true; return Task.FromResult(true); });
    Check(!asked, "nothing needed forcing, so the user was not asked");
    Check(WslCli.Calls.Contains("poweroff:Ubuntu") && WslCli.Calls.Contains("poweroff:Debian"), "both distros were stopped cleanly");
    Check(WslCli.Calls.Count(c => c == "shutdown") == 1, "WSL was shut down once");
    Check(WslCli.Calls.IndexOf("shutdown") > WslCli.Calls.IndexOf("poweroff:Debian"), "WSL was shut down after the distros");
    Check(service.ShutdownStatus == null && !service.IsShuttingDown, "the shutdown finished with nothing to report");
}

static async Task ShutdownStuckDeclined()
{
    var service = await TwoRunning(debianInit: null);
    IReadOnlyList<string>? askedAbout = null;
    await service.ShutdownAllAsync(names => { askedAbout = names; return Task.FromResult(false); });
    Check(askedAbout is ["Debian"], "the user was asked about Debian only");
    Check(!WslCli.Calls.Contains("shutdown"), "WSL was not shut down");
    Check(service.ShutdownStatus?.Key == "shutdown.stuck", "the status says which distros are still running");
    Check(!service.IsShuttingDown, "the shutdown is over");
}

static async Task ShutdownStuckConfirmed()
{
    var service = await TwoRunning(debianInit: null);
    await service.ShutdownAllAsync(_ => Task.FromResult(true));
    Check(WslCli.Calls.Contains("shutdown"), "WSL was shut down");
    Check(service.ShutdownStatus == null, "the shutdown finished with nothing to report");
}
