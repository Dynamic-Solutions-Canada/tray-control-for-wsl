using Microsoft.UI.Xaml;
using System.IO;
using WslMonitor.Classes;

namespace WslMonitor;

/// <summary>
/// Application entry point (WinUI 3). The app is resident: <see cref="MainWindow"/> is a hidden host
/// that owns the tray icon, the WSL monitor service, the tray flyout and the details window.
/// </summary>
public partial class App : Application
{
    /// <summary>App icon (.ico) for window title bars and the taskbar.</summary>
    public static string IconPath => Path.Combine(AppContext.BaseDirectory, "Resources", "WslMonitor.ico");

    /// <summary>256 px PNG of the app icon for in-app images (.ico frames get stretched).</summary>
    public static string IconPngPath => Path.Combine(AppContext.BaseDirectory, "Resources", "WslMonitor.png");

    public App() => InitializeComponent();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, a) =>
        {
            NLog.LogManager.GetCurrentClassLogger().Error(a.ExceptionObject as Exception, "Unhandled exception");
            NLog.LogManager.Flush();
        };
        UnhandledException += (_, e) =>
        {
            NLog.LogManager.GetCurrentClassLogger().Error(e.Exception, "Unhandled UI exception");
            NLog.LogManager.Flush();
            e.Handled = true;
        };

        SettingsManager.Restore();

        _host = new MainWindow();
        _host.Activate();
    }

    private Window? _host;
}
