using System.IO;
using WinUIAppKit;
using WinUIAppKit.Settings;
using WinUIAppKit.Theming;
using WslMonitor.Models;

namespace WslMonitor.Classes;

/// <summary>
/// The app's settings: %AppData%\WslMonitor\settings.json (redirected into package data when packaged).
/// Edits apply and save immediately.
/// </summary>
public static class SettingsManager
{
    /// <summary>Folder name under %AppData% for settings and logs (must match NLog.config).</summary>
    public const string DataFolderName = "WslMonitor";

    private static readonly JsonSettingsStore<UserSettings> Store =
        new(Path.Combine(AppEnvironment.DataDirectory(DataFolderName), "settings.json"));

    /// <summary>The loaded settings. Valid after <see cref="Restore"/>.</summary>
    public static UserSettings Current { get; private set; } = new();

    /// <summary>True when no settings file existed at launch.</summary>
    public static bool IsFirstRun { get; private set; }

    /// <summary>Real folder of settings and logs, for Explorer.</summary>
    public static string PhysicalDataDirectory => AppEnvironment.PhysicalDataDirectory(DataFolderName);

    /// <summary>Load settings (creating them on first run) and apply the saved theme.</summary>
    public static void Restore()
    {
        Current = Store.Load(out bool firstRun);
        IsFirstRun = firstRun;
        AppTheme.Set(Current.AppTheme);
        if (firstRun) Save();
    }

    public static void Save() => Store.Save(Current);
}
