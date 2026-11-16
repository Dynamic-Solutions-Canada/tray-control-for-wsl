using System.IO;
using WinUIAppKit.Localization;

namespace WslMonitor.Services;

/// <summary>
/// The idle-timeout settings from the user's <c>%UserProfile%\.wslconfig</c>. They decide whether a
/// distro started from the tray keeps running once nothing is attached to it, so the app explains
/// them instead of silently working on one PC and not another.
/// </summary>
public sealed class WslConfigFile
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

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
            if (!File.Exists(FilePath))
            {
                return new WslConfigFile();
            }

            string section = "";
            foreach (var raw in File.ReadLines(FilePath))
            {
                string line = StripComment(raw).Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                if (line.StartsWith('[') && line.EndsWith(']'))
                {
                    section = line[1..^1].Trim().ToLowerInvariant();
                    continue;
                }

                int eq = line.IndexOf('=');
                if (eq <= 0)
                {
                    continue;
                }

                string key = line[..eq].Trim();
                if (!int.TryParse(line[(eq + 1)..].Trim(), out int value))
                {
                    continue;
                }

                if (section == "general" && key.Equals("instanceIdleTimeout", StringComparison.OrdinalIgnoreCase))
                {
                    instance = value;
                }

                if (section == "wsl2" && key.Equals("vmIdleTimeout", StringComparison.OrdinalIgnoreCase))
                {
                    vm = value;
                }
            }
        }
        catch (Exception ex) { Logger.Warn(ex, "Couldn't read .wslconfig"); }

        return new WslConfigFile { InstanceIdleTimeoutMs = instance, VmIdleTimeoutMs = vm };
    }

    /// <summary>True when started distros stay running with nothing attached.</summary>
    public bool KeepsDistrosRunning => InstanceIdleTimeoutMs is < 0;

    /// <summary>Plain-language summary of what happens to a distro started here, in the current interface language.</summary>
    public string StartBehaviour => InstanceIdleTimeoutMs switch
    {
        < 0 => UiText.Get("wslconfig.keep"),
        { } ms => UiText.Format("wslconfig.timeout", ("seconds", ms / 1000.0)),
        null => UiText.Get("wslconfig.default"),
    };

    private static string StripComment(string line)
    {
        int hash = line.IndexOfAny(['#', ';']);
        return hash >= 0 ? line[..hash] : line;
    }
}
