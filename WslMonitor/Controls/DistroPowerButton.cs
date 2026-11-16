using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinUIAppKit.Localization;
using WslMonitor.Models;
using WslMonitor.Services;

namespace WslMonitor.Controls;

/// <summary>
/// The Start / Stop button for one distro, used in the tray flyout and the details window.
/// <para>
/// Stopping takes two clicks (<see cref="ClickConfirmation"/>): the first turns the button into a red
/// "Stop?" for a few seconds, the second stops the distro. Everything running in it ends, and for
/// this user's main distro that includes their development tools, so a stray click must not do it.
/// </para>
/// </summary>
public sealed partial class DistroPowerButton : Button
{
    private readonly Distro _distro;
    private readonly WslMonitorService _service;

    public DistroPowerButton(Distro distro, WslMonitorService service)
    {
        _distro = distro;
        _service = service;
        MinWidth = 104;
        VerticalAlignment = VerticalAlignment.Center;
        Click += OnClick;
        Render();
    }

    /// <summary><see cref="ClickConfirmation"/> key for this distro's stop.</summary>
    private string ConfirmKey => "stop:" + _distro.Id;

    // ── Appearance ──────────────────────────────────────────────────────
    /// <summary>Set the label, colours, tooltip and enabled state for the distro's current state.</summary>
    private void Render()
    {
        var res = Application.Current.Resources;
        ClearValue(BackgroundProperty);
        ClearValue(ForegroundProperty);
        IsEnabled = !_service.IsShuttingDown;

        switch (_distro.State)
        {
            case DistroState.Stopped:
                Content = UiText.Get("power.start");
                ToolTipService.SetToolTip(this, UiText.Format("power.start-tip", ("name", _distro.Name)));
                break;

            case DistroState.Running:
            case DistroState.StopTimedOut:
                bool force = _distro.State == DistroState.StopTimedOut;
                if (ClickConfirmation.IsArmed(ConfirmKey))
                {
                    Content = UiText.Get(force ? "power.force-confirm" : "power.stop-confirm");
                    Background = (Brush)res["SystemFillColorCriticalBrush"];
                    Foreground = (Brush)res["TextOnAccentFillColorPrimaryBrush"];
                    ClickConfirmation.OnLapse(ConfirmKey, DispatcherQueue, Render);
                }
                else
                {
                    Content = UiText.Get(force ? "power.force" : "power.stop");
                }
                ToolTipService.SetToolTip(this, force ? ForceStopTip() : StopTip());
                break;

            case DistroState.Starting:
            case DistroState.Stopping:
                Content = Busy(DistroVisuals.StateText(_distro));
                IsEnabled = false;
                break;

            default:
                Content = DistroVisuals.StateText(_distro);
                IsEnabled = false;
                break;
        }
    }

    private static StackPanel Busy(string text)
    {
        var panel = new StackPanel { Style = (Style)Application.Current.Resources["IconLabelStyle"] };
        panel.Children.Add(new ProgressRing { IsActive = true, Width = 14, Height = 14 });
        panel.Children.Add(new TextBlock { Text = text });
        return panel;
    }

    /// <summary>What Stop will do: clean with systemd, immediate without, or unknown until first run.</summary>
    private string StopTip()
    {
        string key = DistroVisuals.StopKindOf(_distro) switch
        {
            StopKind.Clean => "power.stop-tip-systemd",
            StopKind.Immediate => "power.stop-tip-no-systemd",
            _ => "power.stop-tip-unknown",
        };
        return UiText.Format(key, ("name", _distro.Name));
    }

    /// <summary>
    /// Why Force stop is offered: the clean stop ran out of time, or (with an error on the distro) it
    /// couldn't be attempted.
    /// </summary>
    private string ForceStopTip()
    {
        if (_distro.LastError != null)
        {
            return UiText.Format("power.force-tip-generic", ("name", _distro.Name));
        }

        return UiText.Format("power.force-tip",
            ("name", _distro.Name),
            ("minutes", (int)WslMonitorService.CleanStopLimit.TotalMinutes));
    }

    // ── Actions ─────────────────────────────────────────────────────────
    /// <summary>Start a stopped distro; for a running one, arm the confirmation or, if armed, stop it.</summary>
    private async void OnClick(object sender, RoutedEventArgs e)
    {
        switch (_distro.State)
        {
            case DistroState.Stopped:
                await _service.StartAsync(_distro);
                break;

            case DistroState.Running:
            case DistroState.StopTimedOut:
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

                if (_distro.State == DistroState.StopTimedOut)
                {
                    await _service.ForceStopAsync(_distro);
                }
                else
                {
                    await _service.StopAsync(_distro);
                }

                break;
        }
    }
}
