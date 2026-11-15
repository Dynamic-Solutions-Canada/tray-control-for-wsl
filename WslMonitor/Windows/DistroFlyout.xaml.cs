using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
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
            RowsPanel.Children.Add(Message("WSL isn't installed on this PC."));
        }
        else if (distros.Count == 0)
        {
            RowsPanel.Children.Add(Message("No WSL distros are installed."));
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
        if (_service.IsShuttingDown) return "Shutting down WSL…";
        if (_service.IsStateUnknown) return "WSL isn't responding; showing last known state";
        if (distros.Count == 0) return "";
        int running = _service.RunningCount;
        return running == 0 ? "Nothing running" : $"{running} of {distros.Count} running";
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
