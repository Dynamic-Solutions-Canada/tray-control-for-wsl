// Stand-ins for the boundaries WslMonitorService talks to: wsl.exe, the registry, settings, the
// interface text and NLog. Each scenario calls Fakes.Reset() first, so no state leaks between them.
using WslMonitor.Services;

// The stand-ins live in the production namespaces they replace, not this folder's; IDE0130 would
// try to move them and crashes dotnet format.
#pragma warning disable IDE0130
namespace WslMonitor.Services
{
    /// <summary>Scriptable wsl.exe. Records every call; by default every command succeeds at once.</summary>
    public static class WslCli
    {
        public static bool IsInstalled { get; set; } = true;

        /// <summary>What WSL lists as running; null makes ListRunningAsync report "couldn't tell".</summary>
        public static HashSet<string>? Running { get; set; } = new();

        /// <summary>When set, the next ListRunningAsync waits for and returns this answer instead.</summary>
        public static TaskCompletionSource<HashSet<string>?>? NextListRunning { get; set; }

        /// <summary>PID 1's name per distro; a missing entry makes InitSystemAsync fail (null).</summary>
        public static Dictionary<string, string> InitSystems { get; } = new();

        /// <summary>Overrides for the action commands; null means succeed and update <see cref="Running"/>.</summary>
        public static Func<string, Task<WslResult>>? Start { get; set; }
        public static Func<string, Task<WslResult>>? Terminate { get; set; }

        /// <summary>Every command run, as "verb:distro".</summary>
        public static List<string> Calls { get; } = new();

        public static WslResult Ok => new(true, false, 0, "", "");
        public static WslResult Failed(string error) => new(true, false, 1, "", error);

        public static async Task<HashSet<string>?> ListRunningAsync(IReadOnlyCollection<string> knownNames)
        {
            Calls.Add("list");
            if (NextListRunning is { } gate)
            {
                NextListRunning = null;
                return await gate.Task;
            }
            await Task.Yield();
            return Running == null ? null : new HashSet<string>(Running);
        }

        public static async Task<string?> InitSystemAsync(string distro)
        {
            Calls.Add("init:" + distro);
            await Task.Yield();
            return InitSystems.GetValueOrDefault(distro);
        }

        public static async Task<WslResult> StartAsync(string distro)
        {
            Calls.Add("start:" + distro);
            if (Start != null)
            {
                return await Start(distro);
            }

            await Task.Yield();
            Running?.Add(distro);
            return Ok;
        }

        public static async Task<WslResult> PowerOffAsync(string distro)
        {
            Calls.Add("poweroff:" + distro);
            await Task.Yield();
            Running?.Remove(distro);
            return Ok;
        }

        public static async Task<WslResult> TerminateAsync(string distro)
        {
            Calls.Add("terminate:" + distro);
            if (Terminate != null)
            {
                return await Terminate(distro);
            }

            await Task.Yield();
            Running?.Remove(distro);
            return Ok;
        }

        public static async Task<WslResult> ShutdownAsync()
        {
            Calls.Add("shutdown");
            await Task.Yield();
            Running?.Clear();
            return Ok;
        }

        public static async Task<WslResult> SetDefaultAsync(string distro)
        {
            Calls.Add("set-default:" + distro);
            await Task.Yield();
            return Ok;
        }
    }

    /// <summary>The registered distros, as the registry would list them.</summary>
    public static class WslRegistry
    {
        public static List<RegisteredDistro> Distros { get; } = new();

        public static List<RegisteredDistro> ReadDistros() => new(Distros);
    }
}

namespace WslMonitor.Classes
{
    /// <summary>The one setting the service reads.</summary>
    public sealed class TestSettings
    {
        public bool ShowSystemDistros { get; set; }
    }

    public static class SettingsManager
    {
        public static TestSettings Current { get; set; } = new();
    }
}

namespace WinUIAppKit.Localization
{
    /// <summary>Returns keys instead of text; scenarios assert on keys, never on wording.</summary>
    public static class UiText
    {
        public static string Get(string key) => key;

        public static string Format(string key, params (string Name, object? Value)[] values) => key;
    }
}

namespace NLog
{
    /// <summary>Discards log lines; the harness asserts on state, not on logging.</summary>
    public sealed class Logger
    {
        public void Info(string message, params object?[] args) { }
        public void Warn(string message, params object?[] args) { }
        public void Warn(Exception ex, string message, params object?[] args) { }
        public void Error(Exception ex, string message, params object?[] args) { }
    }

    public static class LogManager
    {
        public static Logger GetCurrentClassLogger() => new();
    }
}

#pragma warning restore IDE0130

namespace WslMonitor.State.Tests
{
    /// <summary>Puts every stand-in back to its defaults.</summary>
    public static class Fakes
    {
        public static void Reset()
        {
            WslCli.IsInstalled = true;
            WslCli.Running = new();
            WslCli.NextListRunning = null;
            WslCli.InitSystems.Clear();
            WslCli.Start = null;
            WslCli.Terminate = null;
            WslCli.Calls.Clear();
            WslRegistry.Distros.Clear();
            WslMonitor.Classes.SettingsManager.Current = new();
        }

        /// <summary>Register a distro with sensible defaults.</summary>
        public static RegisteredDistro Register(string name, string busyReason = "")
        {
            var distro = new RegisteredDistro(Guid.NewGuid(), name, 2, "", "", "", "", false, busyReason);
            WslRegistry.Distros.Add(distro);
            return distro;
        }
    }
}
