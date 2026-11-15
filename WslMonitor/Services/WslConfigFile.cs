using System.IO;

namespace WslMonitor.Services;

/// <summary>
/// The idle-timeout settings from the user's <c>%UserProfile%\.wslconfig</c>. They decide whether a
/// distro started from the tray keeps running once nothing is attached to it, so the app explains
/// them instead of silently working on one PC and not another.
/// </summary>
public sealed class WslConfigFile
{
    /// <summary><c>[general] instanceIdleTimeout</c> in milliseconds; -1 keeps distros running; null when unset.</summary>
    public int? InstanceIdleTimeoutMs { get; private init; }

    /// <summary><c>[wsl2] vmIdleTimeout</c> in milliseconds (VM shutdown after the last distro stops); null when unset.</summary>
    public int? VmIdleTimeoutMs { get; private init; }

    public static string FilePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".wslconfig");

    /// <summary>Read the file. A missing or unreadable file reads as "all defaults".</summary>
    public static WslConfigFile Read()
    {
        int? instance = null, vm = null;
        try
        {
            if (!File.Exists(FilePath)) return new WslConfigFile();

            string section = "";
            foreach (var raw in File.ReadLines(FilePath))
            {
                string line = StripComment(raw).Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith('[') && line.EndsWith(']'))
                {
                    section = line[1..^1].Trim().ToLowerInvariant();
                    continue;
                }

                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string key = line[..eq].Trim();
                if (!int.TryParse(line[(eq + 1)..].Trim(), out int value)) continue;

                if (section == "general" && key.Equals("instanceIdleTimeout", StringComparison.OrdinalIgnoreCase)) instance = value;
                if (section == "wsl2" && key.Equals("vmIdleTimeout", StringComparison.OrdinalIgnoreCase)) vm = value;
            }
        }
        catch (Exception ex) { NLog.LogManager.GetCurrentClassLogger().Warn(ex, "Couldn't read .wslconfig"); }

        return new WslConfigFile { InstanceIdleTimeoutMs = instance, VmIdleTimeoutMs = vm };
    }

    /// <summary>True when started distros stay running with nothing attached.</summary>
    public bool KeepsDistrosRunning => InstanceIdleTimeoutMs is < 0;

    /// <summary>Plain-language summary of what happens to a distro started here.</summary>
    public string StartBehaviour => InstanceIdleTimeoutMs switch
    {
        < 0 => "Started distros keep running until you stop them (instanceIdleTimeout=-1 in .wslconfig).",
        { } ms => $"Started distros stop again after {FormatSeconds(ms)} with nothing running in them (instanceIdleTimeout in .wslconfig). Set it to -1 to keep them running.",
        null => "Started distros stop again shortly after nothing is running in them (WSL's default idle timeout). Set instanceIdleTimeout=-1 under [general] in .wslconfig to keep them running.",
    };

    private static string FormatSeconds(int ms) => ms % 1000 == 0 ? $"{ms / 1000} s" : $"{ms / 1000.0:0.#} s";

    private static string StripComment(string line)
    {
        int hash = line.IndexOfAny(['#', ';']);
        return hash >= 0 ? line[..hash] : line;
    }
}
