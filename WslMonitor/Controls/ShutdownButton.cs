using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinUIAppKit.Localization;
using WslMonitor.Models;
using WslMonitor.Services;

namespace WslMonitor.Controls;

/// <summary>
/// The tray flyout's "Shut down WSL" button: a power icon that, like Stop, needs two clicks
/// (<see cref="ClickConfirmation"/>). The first turns it into a red "Shut down WSL?", and its tooltip
/// names what will stop; the second runs the same shutdown as the Overview page. A dialog can't live
/// in the flyout (it closes when it loses focus), so if a distro won't stop cleanly the force question
/// opens in the details window.
/// </summary>
public sealed partial class ShutdownButton : Button
{
    private const string ConfirmKey = "shutdown";

    private readonly WslMonitorService _service;

    public ShutdownButton(WslMonitorService service)
    {
        _service = service;
        VerticalAlignment = VerticalAlignment.Center;
        Click += OnClick;
        Render();
    }

    /// <summary>Icon only when idle, red confirmation text when armed; disabled while a shutdown runs.</summary>
    private void Render()
    {
        var res = Application.Current.Resources;
        ConfirmButtonLook.Clear(this);
        IsEnabled = _service.IsWslInstalled && !_service.IsShuttingDown;
        AutomationProperties.SetName(this, UiText.Get("overview.shutdown"));

        if (ClickConfirmation.IsArmed(ConfirmKey))
        {
            ConfirmButtonLook.ShowArmed(this, UiText.Get("overview.confirm-title"));
            ClickConfirmation.OnLapse(ConfirmKey, DispatcherQueue, Render);
        }
        else
        {
            Content = new FontIcon { Glyph = "", Style = (Style)res["ButtonGlyphStyle"] };
        }

        ToolTipService.SetToolTip(this, Explanation());
    }

    /// <summary>"Shut down WSL" plus what it will stop (every running distro, hidden ones included).</summary>
    private string Explanation()
    {
        var running = _service.AllDistros.Where(d => d.IsOn).Select(d => d.Name).ToList();
        string what = running.Count == 0
            ? UiText.Get("overview.confirm-none")
            : UiText.Format("overview.confirm-some", ("names", new NameList(running)));
        return UiText.Get("overview.shutdown") + "\n" + what;
    }

    /// <summary>Arm on the first click; on a deliberate second click, shut down.</summary>
    private async void OnClick(object sender, RoutedEventArgs e)
    {
        if (!ClickConfirmation.IsArmed(ConfirmKey))
        {
            ClickConfirmation.Arm(ConfirmKey);
            Render();
            return;
        }
        if (!ClickConfirmation.TryConfirm(ConfirmKey))
        {
            return;   // second click of a double-click
        }

        await _service.ShutdownAllAsync(DetailsWindow.ConfirmForceShutdownAsync);
    }
}
