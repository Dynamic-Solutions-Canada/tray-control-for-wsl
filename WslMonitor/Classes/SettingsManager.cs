using System.IO;
using WinUIAppKit;
using WinUIAppKit.Localization;
using WinUIAppKit.Settings;
using WinUIAppKit.Theming;
using WslMonitor.ViewModels;

namespace WslMonitor.Classes;

/// <summary>
/// The app's settings: %AppData%\WslMonitor\settings.json (redirected into package data when packaged),
/// loaded and saved atomically by the kit's versioned <see cref="JsonSettingsStore{T}"/> (CONF-3). It
/// stamps <c>SchemaVersion</c>, runs <see cref="SettingsMigrations"/> on older files after backing them
/// up, and keeps an unreadable file as <c>.bad</c>. Edits apply and save immediately.
/// </summary>
public static class SettingsManager
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    /// <summary>Folder name under %AppData% for settings and logs; also named to the shared NLog config through AppLogging.UseDataFolder.</summary>
    public const string DataFolderName = "WslMonitor";

    private static readonly JsonSettingsStore<UserSettings> Store =
        new(Path.Combine(AppEnvironment.DataDirectory(DataFolderName), "settings.json"), SettingsMigrations.Steps);

    /// <summary>The loaded settings. Valid after <see cref="Restore"/>; replaced by <see cref="Reset"/>.</summary>
    public static UserSettings Current { get; private set; } = new();

    /// <summary>
    /// True when no settings file existed at launch: the app registers Start with Windows and opens
    /// its window. An unreadable file is not a first run, so those actions don't repeat.
    /// </summary>
    public static bool IsFirstRun { get; private set; }

    /// <summary>Real folder of settings and logs, for Explorer.</summary>
    public static string PhysicalDataDirectory => AppEnvironment.PhysicalDataDirectory(DataFolderName);

    /// <summary>
    /// Load settings (creating them on first run, upgrading an older file) and apply the saved theme.
    /// Never throws. Called after the single instance is claimed, so a second launch that only signals
    /// the first never touches the file.
    /// </summary>
    public static void Restore()
    {
        var result = Store.Load();
        Current = result.Settings;
        IsFirstRun = result.Outcome == SettingsLoadOutcome.FirstRun;
        Logger.Info("Settings: {Outcome} (file schema {FileVersion}, backup {Backup})",
            result.Outcome, result.FileSchemaVersion?.ToString() ?? "none", result.BackupPath ?? "none");
        AppTheme.Set(Current.AppTheme);
    }

    /// <summary>
    /// "Reset settings" (CONF-4): the store keeps the current file as
    /// <c>settings.json.&lt;UTC time&gt;.reset</c> and writes defaults; the default theme and language
    /// apply at once. Start with Windows is Windows' state and is left as it is. Callers refresh their
    /// controls and call <c>MainWindow.Service.SettingsChanged()</c>.
    /// </summary>
    public static void Reset()
    {
        Current = Store.Reset().Settings;
        AppTheme.Set(Current.AppTheme);
        UiText.SetOverride(Current.Language);
    }

    /// <summary>Save the current settings; failures are logged by the store, never thrown.</summary>
    public static void Save() => Store.Save(Current);
}
