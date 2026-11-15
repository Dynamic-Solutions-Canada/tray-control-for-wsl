using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinUIAppKit.Localization;
using WinUIAppKit.Theming;
using WinUIAppKit.Windowing;
using WslMonitor.Controls;
using WslMonitor.Models;
using WslMonitor.Services;

namespace WslMonitor.Windows;

/// <summary>
/// Tray flyout: one row per distro with its state and a Start / Stop button, plus a Details button
/// for the full window. Window behaviour (placement, acrylic, parking, dismissal) comes from the
/// shared <see cref="FlyoutController"/>; this class only builds content.
/// </summary>
public sealed partial class DistroFlyout : Window
{
    private const double WidthDip = 380;

    private readonly WslMonitorService _service;
    private readonly FlyoutController _controller;

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
        };
        _controller.Dismissed += () => _service.SetViewerActive(this, false);

        _service.Changed += OnServiceChanged;
        UiText.Changed += OnServiceChanged;   // code-built rows and status relabel the same way
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
        if (!_controller.IsOpen) return;   // rebuilt on the next open anyway
        Refresh();
        _controller.UpdateSize();
    }

    // ── Content ─────────────────────────────────────────────────────────
    private void Refresh()
    {
        RowsPanel.Children.Clear();

        var distros = _service.Distros;
        if (!_service.IsWslInstalled)
        {
            RowsPanel.Children.Add(Message(UiText.Get("list.no-wsl")));
        }
        else if (distros.Count == 0)
        {
            RowsPanel.Children.Add(Message(UiText.Get("list.no-distros")));
        }
        else
        {
            foreach (var d in distros)
            {
                RowsPanel.Children.Add(DistroRow.Build(d, _service, () =>
                {
                    _controller.Dismiss();
                    DetailsWindow.ShowInstance(d.Name);
                }));
            }
        }

        StatusText.Text = StatusLine(distros);
    }

    private string StatusLine(IReadOnlyList<Distro> distros)
    {
        if (_service.IsShuttingDown) return UiText.Get("status.shutting-down");
        if (_service.IsStateUnknown) return UiText.Get("status.unresponsive");
        if (distros.Count == 0) return "";
        int running = _service.RunningCount;
        return running == 0
            ? UiText.Get("status.nothing-running")
            : UiText.Format("status.some-running", ("running", running), ("total", distros.Count));
    }

    private static TextBlock Message(string text) => new()
    {
        Text = text,
        Margin = new Thickness(10, 4, 10, 8),
        TextWrapping = TextWrapping.Wrap,
        Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
    };

    private void Details_Click(object sender, RoutedEventArgs e)
    {
        _controller.Dismiss();
        DetailsWindow.ShowInstance();
    }
}
