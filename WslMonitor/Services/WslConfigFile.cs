using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using WinUIAppKit.Localization;

namespace WslMonitor.Services;

/// <summary>
/// The settings the app reads from the user's <c>%UserProfile%\.wslconfig</c>. The idle timeouts decide
/// whether a distro started from the tray keeps running once nothing is attached to it, so the app
/// explains them instead of silently working on one PC and not another; the VM's memory and processor
/// limits are what the flyout's usage meters measure against.
/// </summary>
public sealed partial class WslConfigFile
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    /// <summary><c>[general] instanceIdleTimeout</c> in milliseconds; -1 keeps distros running; null when unset.</summary>
    public int? InstanceIdleTimeoutMs { get; private init; }

    /// <summary><c>[wsl2] vmIdleTimeout</c> in milliseconds (VM shutdown after the last distro stops); null when unset.</summary>
    public int? VmIdleTimeoutMs { get; private init; }

    /// <summary><c>[wsl2] memory</c> in bytes (the VM's memory limit); null when unset or unreadable.</summary>
    public long? MemoryBytes { get; private init; }

    /// <summary><c>[wsl2] processors</c> (logical processors the VM may use); null when unset or unreadable.</summary>
    public int? Processors { get; private init; }

    public static string FilePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".wslconfig");

    /// <summary>Read the file. A missing or unreadable file reads as "all defaults".</summary>
    public static WslConfigFile Read()
    {
        int? instance = null, vm = null, processors = null;
        long? memory = null;
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
                string value = line[(eq + 1)..].Trim();

                if (section == "general" && key.Equals("instanceIdleTimeout", StringComparison.OrdinalIgnoreCase))
                {
                    instance = ParseInt(value) ?? instance;
                }

                if (section == "wsl2" && key.Equals("vmIdleTimeout", StringComparison.OrdinalIgnoreCase))
                {
                    vm = ParseInt(value) ?? vm;
                }

                if (section == "wsl2" && key.Equals("memory", StringComparison.OrdinalIgnoreCase))
                {
                    memory = ParseSize(value) ?? memory;
                }

                if (section == "wsl2" && key.Equals("processors", StringComparison.OrdinalIgnoreCase))
                {
                    processors = ParseInt(value) ?? processors;
                }
            }
        }
        catch (Exception ex) { Logger.Warn(ex, "Couldn't read .wslconfig"); }

        return new WslConfigFile
        {
            InstanceIdleTimeoutMs = instance,
            VmIdleTimeoutMs = vm,
            MemoryBytes = memory,
            Processors = processors,
        };
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

    /// <summary>A whole number in invariant form, or null.</summary>
    private static int? ParseInt(string value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : null;

    /// <summary>
    /// A <c>size</c> value in bytes: a number with an optional unit (<c>8GB</c>, <c>512MB</c>, <c>8G</c>,
    /// <c>1.5GB</c>, or plain bytes), units in any case and in powers of 1024 as WSL reads them. Null
    /// for anything else, so a typo reads as "unset" (WSL's default) rather than a wrong limit.
    /// </summary>
    public static long? ParseSize(string value)
    {
        var match = SizePattern().Match(value.Trim());
        if (!match.Success)
        {
            return null;
        }

        if (!double.TryParse(match.Groups[1].Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double amount))
        {
            return null;
        }

        int power = char.ToUpperInvariant(match.Groups[2].Value.FirstOrDefault()) switch
        {
            'K' => 1,
            'M' => 2,
            'G' => 3,
            'T' => 4,
            _ => 0,
        };

        double bytes = amount * Math.Pow(1024, power);
        return bytes is >= 1 and < long.MaxValue ? (long)bytes : null;
    }

    [GeneratedRegex(@"^(\d+(?:\.\d+)?)\s*([KMGT]?)B?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SizePattern();

    private static string StripComment(string line)
    {
        int hash = line.IndexOfAny(['#', ';']);
        return hash >= 0 ? line[..hash] : line;
    }
}
