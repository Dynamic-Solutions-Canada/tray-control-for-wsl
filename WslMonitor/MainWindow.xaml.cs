using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinRT.Interop;
using WinUIAppKit;
using WinUIAppKit.Localization;
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
/// Hidden, resident host window (1×1, off-screen, never closes). Owns the tray icon, the
/// <see cref="WslMonitorService"/> and the tray flyout. Exit happens only from the tray menu.
/// </summary>
public sealed partial class MainWindow : Window
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    // App identity. Never reuse a sibling app's values: a shared tray GUID or mutex makes the apps
    // collide with each other.
    public const string HostTitle = "WSL Monitor Host";
    private const string MutexName = "WslMonitor";
    private const string ShowMessageName = "WslMonitor_Show";
    private static readonly Guid TrayGuid = new("d3b6a0f2-6c4e-4f0b-9a7e-2f51c8e4a913");

    /// <summary>"Start with Windows" (manifest task id / unpackaged Run value).</summary>
    public static StartupRegistration Startup { get; } = new("WslMonitorStartup", "WSL Monitor");

    public static WslMonitorService Service { get; private set; } = null!;

    private readonly IntPtr _hwnd;
    private readonly SUBCLASSPROC _wndProc;   // held in a field so the GC can't collect the delegate
    private readonly int _wmShow;
    private readonly TrayIcon _tray;
    private readonly DistroFlyout _flyout;
    private readonly global::Windows.UI.ViewManagement.UISettings _uiSettings = new();

    public MainWindow()
    {
        InitializeComponent();

        // Single instance: ask the running copy to show itself, then exit.
        if (!SingleInstance.TryClaim(MutexName))
        {
            if (!Startup.IsSilentLaunch()) SingleInstance.SignalRunningInstance(HostTitle, ShowMessageName);
            Environment.Exit(0);
        }

        _hwnd = WindowNative.GetWindowHandle(this);
        HideHostWindow();

        _wmShow = RegisterWindowMessage(ShowMessageName);
        _wndProc = WndProc;
        SetWindowSubclass(_hwnd, _wndProc, 0, 0);
        RegisterApplicationRestart("--silent", 0);

        Service = new WslMonitorService();
        _flyout = new DistroFlyout(Service);

        _tray = new TrayIcon(_hwnd, TrayGuid, "WslMonitor_TrayCallback");
        _tray.Clicked += a => _flyout.Toggle(a.X, a.Y);
        _tray.DoubleClicked += a => _flyout.ShowAt(a.X, a.Y);   // double-click also just opens the flyout
        _tray.ContextMenuRequested += ShowContextMenu;
        _tray.RenderNeeded += UpdateTrayIcon;
        _tray.DisplayChanged += _flyout.OnDisplayChanged;

        Service.Changed += UpdateTrayIcon;
        AppTheme.Changed += UpdateTrayIcon;
        UiText.Changed += UpdateTrayIcon;   // tooltip wording
        _uiSettings.ColorValuesChanged += (_, _) => DispatcherQueue.TryEnqueue(UpdateTrayIcon);
        UpdateTrayIcon();

        if (SettingsManager.IsFirstRun && SettingsManager.Current.Startup) _ = Startup.RegisterOnFirstRunAsync();
        Startup.RemoveUnpackagedEntries();

        Logger.Info("Started v{Version}, packaged={Packaged}", AppEnvironment.Version, AppEnvironment.IsPackaged);
        Service.Start();

        if (!Startup.IsSilentLaunch() || SettingsManager.IsFirstRun) DetailsWindow.ShowInstance();
    }

    private void HideHostWindow()
    {
        AddExStyle(_hwnd, WS_EX_TOOLWINDOW);
        var appWindow = AppWindow.GetFromWindowId(Microsoft.UI.Win32Interop.GetWindowIdFromWindow(_hwnd));
        appWindow.IsShownInSwitchers = false;
        appWindow.Move(new global::Windows.Graphics.PointInt32(-9999, -9999));
        appWindow.Resize(new global::Windows.Graphics.SizeInt32(1, 1));
        appWindow.Closing += (_, e) => e.Cancel = true;
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
        if (Service.IsShuttingDown || Service.Distros.Any(d => d.IsTransitioning)) return TrayStatus.Changing;
        if (Service.RunningCount > 0) return TrayStatus.Running;
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
            .Show(_hwnd, x, y);
    }

    public static void OpenLogsFolder() => AppEnvironment.OpenFolder(SettingsManager.PhysicalDataDirectory);

    private void ExitApp()
    {
        Logger.Info("Exit requested");
        SettingsManager.Save();
        Service.Dispose();
        _tray.Dispose();
        NLog.LogManager.Flush();
        Environment.Exit(0);
    }

    // ── Window procedure ────────────────────────────────────────────────
    private IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam, UIntPtr id, UIntPtr refData)
    {
        if (_wmShow != 0 && (int)msg == _wmShow)
        {
            DispatcherQueue.TryEnqueue(() => DetailsWindow.ShowInstance());
            return IntPtr.Zero;
        }
        return DefSubclassProc(hwnd, msg, wParam, lParam);
    }
}
