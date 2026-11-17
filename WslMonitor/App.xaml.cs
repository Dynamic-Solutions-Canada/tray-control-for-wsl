using Microsoft.UI.Xaml;
using System.IO;
using WinUIAppKit.Hosting;
using WinUIAppKit.Localization;
using WinUIAppKit.Logging;
using WslMonitor.Classes;

namespace WslMonitor;

/// <summary>
/// Application entry point (WinUI 3). The app is resident: <see cref="MainWindow"/> is a hidden host
/// that owns the tray icon, the WSL monitor service and the tray flyout; the details window is a
/// single instance opened through <see cref="DetailsWindow"/>'s static Show methods.
/// </summary>
public partial class App : Application
{
    /// <summary>App icon (.ico) for window title bars and the taskbar.</summary>
    public static string IconPath => Path.Combine(AppContext.BaseDirectory, "Resources", "WslMonitor.ico");

    /// <summary>256 px PNG of the app icon for in-app images (.ico frames get stretched).</summary>
    public static string IconPngPath => Path.Combine(AppContext.BaseDirectory, "Resources", "WslMonitor.png");

    public App()
    {
        // The shared NLog config writes to %AppData%\<data folder>; name it before anything logs.
        AppLogging.UseDataFolder(SettingsManager.DataFolderName);
        InitializeComponent();
    }

    /// <summary>
    /// Starts the app through the kit's <see cref="AppBootstrap"/>: crash logging first (LOG-7), then
    /// <see cref="ClaimInstanceAndRestoreSettings"/>, then the hidden host window.
    /// </summary>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _host = AppBootstrap.Launch(this, ClaimInstanceAndRestoreSettings, () => new MainWindow());
    }

    /// <summary>
    /// The settings step of the launch. The single instance is claimed first, so a second launch asks
    /// the running copy to show itself and exits without touching the settings file (the store writes
    /// the file it loads).
    /// </summary>
    private static void ClaimInstanceAndRestoreSettings()
    {
        if (!MainWindow.ClaimSingleInstance())
        {
            Environment.Exit(0);
        }

        SettingsManager.Restore();
        UiText.Configure(typeof(App).Assembly, "WslMonitor.Localization.", AppLanguages.Supported, SettingsManager.Current.Language);
    }

    private Window? _host;
}
