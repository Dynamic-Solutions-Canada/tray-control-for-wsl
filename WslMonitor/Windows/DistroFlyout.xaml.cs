using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinUIAppKit.Localization;
using WinUIAppKit.Theming;
using WinUIAppKit.Windowing;
using WslMonitor.Controls;
using WslMonitor.Services;

namespace WslMonitor.Windows;

/// <summary>
/// Tray flyout: the WSL VM's CPU and memory, one row per distro with its state and a Start / Stop
/// button, plus Shut down WSL and a Details button for the full window. Window behaviour (placement,
/// acrylic, parking, dismissal) comes from the shared <see cref="FlyoutController"/>; this class only
/// builds content.
/// </summary>
public sealed partial class DistroFlyout : Window
{
    private const double WidthDip = 380;

    private readonly WslMonitorService _service;
    private readonly FlyoutController _controller;
    private readonly WslVmUsageSampler _usage = new();

    public DistroFlyout(WslMonitorService service)
    {
        InitializeComponent();
        _service = service;

        AppTheme.Track(this);
        Refresh();
        _controller = new FlyoutController(this, WindowRoot, WidthDip);
        _controller.Opening += () =>
        {
            _service.SetViewerActive(this, true);
            Refresh();

            // Sampling runs only while open; its first reading lands before the flyout is sized.
            _usage.Start(DispatcherQueue);
        };
        _controller.Dismissed += () =>
        {
            _service.SetViewerActive(this, false);
            _usage.Stop();
        };

        _usage.Updated += OnUsageUpdated;
        _service.Changed += OnServiceChanged;
        UiText.Changed += OnServiceChanged;   // code-built rows and status relabel the same way
        UiText.Changed += UsageStrip.Relabel;
    }

    /// <summary>Tray single click: open, or close if open.</summary>
    public void Toggle(int anchorX, int anchorY) => _controller.Toggle(anchorX, anchorY);

    /// <summary>Tray double-click: make sure it is open.</summary>
    public void ShowAt(int anchorX, int anchorY) => _controller.Show(anchorX, anchorY);

    /// <summary>The display layout changed: close and re-park off-screen.</summary>
    public void OnDisplayChanged()
    {
        _controller.Dismiss();
        _controller.Park();
    }

    private void OnServiceChanged()
    {
        if (!_controller.IsOpen)
        {
            return;   // rebuilt on the next open anyway
        }

        Refresh();
        _controller.UpdateSize();
    }

    /// <summary>
    /// Show a usage reading in place. Only a change in whether the strip shows (the VM started or
    /// stopped) changes the flyout's height, so only then is it re-placed.
    /// </summary>
    private void OnUsageUpdated()
    {
        var before = UsageStrip.Visibility;
        UsageStrip.Show(_usage.Usage, _usage.Limits);
        if (UsageStrip.Visibility != before)
        {
            _controller.UpdateSize();
        }
    }

    // ── Content ─────────────────────────────────────────────────────────
    private void Refresh()
    {
        DistroList.Fill(RowsPanel, _service, d =>
        {
            _controller.Dismiss();
            DetailsWindow.ShowDistro(d.Name);
        });

        ShutdownHost.Content = new ShutdownButton(_service);

        // With no distros the list's own message already says so; don't repeat it in the footer.
        bool listExplainsItself = _service.Distros.Count == 0 && !_service.IsShuttingDown;
        StatusText.Text = listExplainsItself ? "" : DistroVisuals.StatusLine(_service);
    }

    private void Details_Click(object sender, RoutedEventArgs e)
    {
        _controller.Dismiss();
        DetailsWindow.ShowInstance();
    }
}
