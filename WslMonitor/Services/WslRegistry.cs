using Microsoft.Win32;
using System.IO;

namespace WslMonitor.Services;

/// <summary>A distro as WSL registers it for the current user.</summary>
public sealed record RegisteredDistro(
    Guid Id,
    string Name,
    int WslVersion,
    string BasePath,
    string DiskPath,
    string Flavor,
    string OsVersion,
    bool IsDefault,
    string BusyReason);

/// <summary>
/// Reads the distro list from WSL's per-user registry key. This is how WSL itself records installed
/// distros, it is instant (no wsl.exe process), and it includes details the command line doesn't
/// print. Running state is not stored here; that comes from <see cref="WslCli.ListRunningAsync"/>.
/// </summary>
public static class WslRegistry
{
    private const string LxssKey = @"Software\Microsoft\Windows\CurrentVersion\Lxss";
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    /// <summary>All registered distros, in name order. Empty when none are installed or the key is unreadable.</summary>
    public static List<RegisteredDistro> ReadDistros()
    {
        var list = new List<RegisteredDistro>();
        try
        {
            using var root = Registry.CurrentUser.OpenSubKey(LxssKey);
            if (root == null) return list;

            string defaultId = root.GetValue("DefaultDistribution") as string ?? "";
            foreach (var subName in root.GetSubKeyNames())
            {
                if (!Guid.TryParse(subName, out var id)) continue;
                using var key = root.OpenSubKey(subName);
                if (key?.GetValue("DistributionName") is not string name || name.Length == 0) continue;
                list.Add(Read(key, id, name, string.Equals(subName, defaultId, StringComparison.OrdinalIgnoreCase)));
            }
        }
        catch (Exception ex) { Logger.Warn(ex, "Couldn't read the WSL registry key"); }

        return list.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static RegisteredDistro Read(RegistryKey key, Guid id, string name, bool isDefault)
    {
        int version = key.GetValue("Version") is int v ? v : 2;
        string basePath = key.GetValue("BasePath") as string ?? "";
        string vhd = key.GetValue("VhdFileName") as string ?? "";
        string diskPath = version >= 2 && basePath.Length > 0 && vhd.Length > 0 ? Path.Combine(basePath, vhd) : "";

        return new RegisteredDistro(
            id,
            name,
            version,
            basePath,
            diskPath,
            key.GetValue("Flavor") as string ?? "",
            key.GetValue("OsVersion") as string ?? "",
            isDefault,
            BusyReasonFor(key.GetValue("State") is int s ? s : 1));
    }

    /// <summary>
    /// WSL's registration state: 1 is a normal installed distro; the others are transient
    /// (2 installing, 3 uninstalling, 4 converting between WSL versions). Empty means usable.
    /// </summary>
    private static string BusyReasonFor(int state) => state switch
    {
        1 => "",
        2 => "Installing",
        3 => "Removing",
        4 => "Converting",
        _ => "Busy",
    };

    /// <summary>Size of a distro's virtual disk in bytes, or null when it can't be read.</summary>
    public static long? DiskSize(string diskPath)
    {
        try { return diskPath.Length > 0 && File.Exists(diskPath) ? new FileInfo(diskPath).Length : null; }
        catch { return null; }
    }
}
