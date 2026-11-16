using System.Diagnostics;
using System.IO;
using System.Text;

namespace WslMonitor.Services;

/// <summary>
/// Runs <c>wsl.exe</c> hidden and off the UI thread. Output is requested as UTF-8 (<c>WSL_UTF8=1</c>);
/// without it, <c>--list</c> writes UTF-16. Every command here is one WSL documents; none changes WSL
/// configuration except <see cref="SetDefaultAsync"/>.
/// </summary>
public static class WslCli
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    /// <summary>System32 copy, not a PATH lookup: the packaged app's PATH can differ from the user's.</summary>
    private static string WslExe => Path.Combine(Environment.SystemDirectory, "wsl.exe");

    /// <summary>True when WSL's command line is present on this PC.</summary>
    public static bool IsInstalled => File.Exists(WslExe);

    // ── Queries ─────────────────────────────────────────────────────────
    /// <summary>
    /// Names of running distros, or null when WSL couldn't answer (so the caller keeps the last known
    /// state rather than briefly showing everything as stopped).
    /// </summary>
    public static async Task<HashSet<string>?> ListRunningAsync(IReadOnlyCollection<string> knownNames)
    {
        var result = await RunAsync(TimeSpan.FromSeconds(15), "--list", "--running", "--quiet");
        if (!result.Launched || result.TimedOut) return null;

        // With --quiet and nothing running, WSL prints nothing and exits 0 (checked in WSL 3.0.5's
        // WslClient.cpp, ListDistributionsHelper), so any non-zero exit is a
        // failure. Reading it as "nothing running" would show every distro stopped and make a pending
        // stop look finished.
        if (result.ExitCode != 0)
        {
            Logger.Warn("wsl --list --running failed: {Message}", result.Message);
            return null;
        }

        var lines = result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return lines.Where(l => knownNames.Contains(l, StringComparer.OrdinalIgnoreCase))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>The <c>wsl --version</c> text (WSL, kernel, WSLg and Windows versions), or null.</summary>
    public static async Task<string?> VersionAsync()
    {
        var result = await RunAsync(TimeSpan.FromSeconds(15), "--version");
        return result.Succeeded ? result.Output.Trim() : null;
    }

    /// <summary>Name of PID 1 in a running distro ("systemd", "init"), or null if it couldn't be read.</summary>
    public static async Task<string?> InitSystemAsync(string distro)
    {
        var result = await RunAsync(TimeSpan.FromSeconds(20), "-d", distro, "-u", "root", "--exec", "cat", "/proc/1/comm");
        return result.Succeeded ? result.Output.Trim() : null;
    }

    // ── Actions ─────────────────────────────────────────────────────────
    /// <summary>
    /// Boot a distro by running a no-op in it. Whether it then stays running depends on WSL's idle
    /// timeout (see <see cref="WslConfigFile"/>); systemd boot can take a while, hence the long limit.
    /// </summary>
    public static Task<WslResult> StartAsync(string distro) =>
        RunAsync(TimeSpan.FromMinutes(2), "-d", distro, "--exec", "true");

    /// <summary>
    /// Ask systemd to shut the distro down cleanly (services stopped in order, processes signalled).
    /// Returns once the request is queued or the distro disappears, not when shutdown finishes.
    /// </summary>
    public static Task<WslResult> PowerOffAsync(string distro) =>
        RunAsync(TimeSpan.FromSeconds(30), "-d", distro, "-u", "root", "--exec", "systemctl", "poweroff");

    /// <summary>Hard-stop one distro immediately (processes are killed without warning).</summary>
    public static Task<WslResult> TerminateAsync(string distro) =>
        RunAsync(TimeSpan.FromSeconds(60), "--terminate", distro);

    /// <summary>Hard-stop every distro and the WSL virtual machine.</summary>
    public static Task<WslResult> ShutdownAsync() =>
        RunAsync(TimeSpan.FromSeconds(90), "--shutdown");

    /// <summary>Make a distro the one plain <c>wsl</c> opens.</summary>
    public static Task<WslResult> SetDefaultAsync(string distro) =>
        RunAsync(TimeSpan.FromSeconds(30), "--set-default", distro);

    // ── Interactive launches ────────────────────────────────────────────
    /// <summary>
    /// Open a shell in the distro's home folder, in the user's default terminal (starts the distro if
    /// needed). Returns false if it couldn't be launched.
    /// </summary>
    public static bool OpenTerminal(string distro) =>
        Launch(WslExe, ["-d", distro, "--cd", "~"]);

    /// <summary>Open the distro's files in Explorer (WSL starts the distro if it is stopped).</summary>
    public static bool OpenFiles(string distro) =>
        Launch("explorer.exe", [$@"\\wsl.localhost\{distro}"]);

    private static bool Launch(string file, string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo(file) { UseShellExecute = true };
            foreach (var a in args) psi.ArgumentList.Add(a);
            Process.Start(psi)?.Dispose();
            return true;
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Couldn't launch {File} {Args}", file, string.Join(' ', args));
            return false;
        }
    }

    // ── Process plumbing ────────────────────────────────────────────────
    /// <summary>
    /// Run wsl.exe with <paramref name="args"/> (passed as separate arguments, never a joined string),
    /// capturing output. Exceeding <paramref name="timeout"/> (waiting for exit or for its output) kills
    /// the wsl.exe process tree; that stops our client, not the distro.
    /// </summary>
    private static async Task<WslResult> RunAsync(TimeSpan timeout, params string[] args)
    {
        var psi = new ProcessStartInfo(WslExe)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,   // a command must never wait on our (nonexistent) console
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        psi.Environment["WSL_UTF8"] = "1";

        Process process;
        try
        {
            process = Process.Start(psi) ?? throw new InvalidOperationException("Process.Start returned null");
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Couldn't start wsl.exe {Args}", string.Join(' ', args));
            return new WslResult(false, false, -1, "", ex.Message);
        }

        using (process)
        {
            process.StandardInput.Close();

            // One deadline covers the exit and the output reads: a child process that inherited the
            // pipes can hold them open after wsl.exe exits, and an unbounded read would hang the caller
            // (for the poll loop, forever).
            using var cts = new CancellationTokenSource(timeout);
            var stdout = process.StandardOutput.ReadToEndAsync(cts.Token);
            var stderr = process.StandardError.ReadToEndAsync(cts.Token);

            string output, error;
            try
            {
                await process.WaitForExitAsync(cts.Token);
                output = await stdout;
                error = await stderr;
            }
            catch (OperationCanceledException)
            {
                Logger.Warn("wsl.exe {Args} timed out after {Seconds}s", string.Join(' ', args), timeout.TotalSeconds);
                try { process.Kill(entireProcessTree: true); } catch { }
                return new WslResult(true, true, -1, "", "");
            }

            var result = new WslResult(true, false, process.ExitCode, output, error);
            if (result.ExitCode != 0)
                Logger.Info("wsl.exe {Args} exited {Code}: {Message}", string.Join(' ', args), result.ExitCode, result.Message);
            return result;
        }
    }
}
