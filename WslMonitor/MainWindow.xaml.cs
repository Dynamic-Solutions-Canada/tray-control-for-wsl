using Microsoft.UI.Xaml;
using WinUIAppKit;
using WinUIAppKit.Hosting;
using WinUIAppKit.Localization;
using WinUIAppKit.Logging;
using WinUIAppKit.Startup;
using WinUIAppKit.Theming;
using WinUIAppKit.Tray;
using WslMonitor.Classes;
using WslMonitor.Controls;
using WslMonitor.Models;
using WslMonitor.Services;
using WslMonitor.Windows;
using static WinUIAppKit.Interop.NativeMethods;

namespace WslMonitor;

/// <summary>
/// Hidden, resident host window (the kit's <see cref="HostWindow"/>: 1×1, off-screen, never closes).
/// Owns the tray icon, the <see cref="WslMonitorService"/> and the tray flyout. Exit happens only from
/// the tray menu.
/// </summary>
public sealed partial class MainWindow : Window
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    // App identity. Never reuse a sibling app's values: a shared tray GUID or mutex makes the apps
    // collide with each other. Never change them either: Windows keeps the tray icon's show or hide
    // choice by the GUID, and a second launch finds the running copy by the title and message.
    public const string HostTitle = "WSL Monitor Host";
    private const string MutexName = "WslMonitor";
    private const string ShowMessageName = "WslMonitor_Show";
    private static readonly Guid TrayGuid = new("d3b6a0f2-6c4e-4f0b-9a7e-2f51c8e4a913");

    /// <summary>"Start with Windows" (manifest task id / unpackaged Run value).</summary>
    public static StartupRegistration Startup { get; } = new("WslMonitorStartup", "WSL Monitor");

    public static WslMonitorService Service { get; private set; } = null!;

    private readonly HostWindow _host;
    private readonly TrayIcon _tray;
    private readonly DistroFlyout _flyout;
    private readonly global::Windows.UI.ViewManagement.UISettings _uiSettings = new();

    /// <summary>
    /// Claim the single instance. Called by <see cref="App"/> before settings are loaded, so a second
    /// launch never touches the settings file. When another copy is running, asks it to show itself
    /// (unless this is a silent sign-in launch) and returns false; the caller then exits.
    /// </summary>
    public static bool ClaimSingleInstance()
    {
        if (SingleInstance.TryClaim(MutexName))
        {
            return true;
        }

        if (!Startup.IsSilentLaunch())
        {
            SingleInstance.SignalRunningInstance(HostTitle, ShowMessageName);
        }

        return false;
    }

    public MainWindow()
    {
        InitializeComponent();

        // The kit's hidden host (TRAY-7): answers a second launch's "show yourself" message.
        _host = new HostWindow(this, HostTitle, ShowMessageName);
        _host.ShowRequested += () => DetailsWindow.ShowInstance();
        RegisterApplicationRestart("--silent", 0);

        Service = new WslMonitorService();
        _flyout = new DistroFlyout(Service);

        _tray = new TrayIcon(_host.Hwnd, TrayGuid, "WslMonitor_TrayCallback");
        _tray.Clicked += a => _flyout.Toggle(a.X, a.Y);
        _tray.DoubleClicked += a => _flyout.ShowAt(a.X, a.Y);   // double-click also just opens the flyout
        _tray.ContextMenuRequested += ShowContextMenu;
        _tray.RenderNeeded += UpdateTrayIcon;
        _tray.DisplayChanged += _flyout.OnDisplayChanged;

        Service.Changed += UpdateTrayIcon;
        AppTheme.Changed += UpdateTrayIcon;
        UiText.Changed += UpdateTrayIcon;   // tooltip wording
        // The shared "Our other apps" page has its own strings; point it at our language choice.
        TechnicallyReal.Promo.PromoLocalization.LocaleProvider = () => UiText.Locale;
        UiText.Changed += TechnicallyReal.Promo.PromoLocalization.Refresh;
        _uiSettings.ColorValuesChanged += (_, _) => DispatcherQueue.TryEnqueue(UpdateTrayIcon);
        UpdateTrayIcon();

        // Exit steps run in this order when the user picks Exit (HostWindow.Exit).
        _host.AddExitStep("save settings", SettingsManager.Save);
        _host.AddExitStep("stop the WSL monitor", Service.Dispose);
        _host.AddExitStep("remove the tray icon", _tray.Dispose);

        if (SettingsManager.IsFirstRun && SettingsManager.Current.Startup)
        {
            _ = Startup.RegisterOnFirstRunAsync();
        }

        Startup.RemoveUnpackagedEntries();

        Logger.Info("Started v{Version}, packaged={Packaged}", AppEnvironment.Version, AppEnvironment.IsPackaged);
        Service.Start();

        // An unreadable settings file was replaced by defaults: say so on this launch, even a silent
        // one, because the next launch loads the new file normally (CONF-2). Startup is not re-registered.
        if (SettingsManager.NeedsResetNotice)
        {
            DetailsWindow.ShowSettings();
        }
        else if (!Startup.IsSilentLaunch() || SettingsManager.IsFirstRun)
        {
            DetailsWindow.ShowInstance();
        }
    }

    // ── Tray icon ───────────────────────────────────────────────────────
    private void UpdateTrayIcon()
    {
        var png = TrayIconRenderer.Render(TrayStatusNow(), TrayIcon.TaskbarIsDark(), TrayIcon.IconPixelSize);

        // Same summary as the flyout footer, so the two can't disagree.
        string tooltip = UiText.Format("tray.tooltip", ("detail", DistroVisuals.StatusLine(Service)));
        _tray.Update(png, tooltip);
    }

    /// <summary>Amber while anything is changing, green while something runs, plain otherwise.</summary>
    private static TrayStatus TrayStatusNow()
    {
        if (Service.IsShuttingDown || Service.Distros.Any(d => d.IsTransitioning))
        {
            return TrayStatus.Changing;
        }

        if (Service.RunningCount > 0)
        {
            return TrayStatus.Running;
        }

        return TrayStatus.Idle;
    }

    private void ShowContextMenu(int x, int y)
    {
        new TrayMenu()
            .Add(UiText.Get("menu.open"), DetailsWindow.ShowInstance)
            .Add(UiText.Get("nav.settings"), DetailsWindow.ShowSettings)
            .Add(UiText.Get("menu.logs"), OpenLogsFolder)
            .AddSeparator()
            .Add(UiText.Get("menu.exit"), ExitApp)
            .Show(_host.Hwnd, x, y);
    }

    public static void OpenLogsFolder() => AppLogging.OpenLogsFolder();

    /// <summary>Exits through the host: the exit steps run in order, the log is flushed, then the process ends.</summary>
    private void ExitApp() => _host.Exit();
}
