using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WslMonitor.Models;
using WslMonitor.Services;

namespace WslMonitor.Controls;

/// <summary>
/// The Start / Stop button for one distro, used in the tray flyout and the details window.
/// <para>
/// Stopping takes two clicks: the first turns the button into a red "Stop?" for a few seconds, the
/// second stops the distro. Everything running in it ends, and for this user's main distro that
/// includes their development tools, so a stray click must not do it. The pending confirmation is
/// remembered per distro (not per button) because lists are rebuilt as WSL state is polled.
/// </para>
/// </summary>
public sealed partial class DistroPowerButton : Button
{
    private static readonly TimeSpan ConfirmWindow = TimeSpan.FromSeconds(4);

    /// <summary>Distro id → when its pending "Stop?" confirmation lapses.</summary>
    private static readonly Dictionary<Guid, DateTime> PendingConfirmations = new();

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

    private bool IsConfirming =>
        PendingConfirmations.TryGetValue(_distro.Id, out var until) && until > DateTime.UtcNow;

    // ── Appearance ──────────────────────────────────────────────────────
    private void Render()
    {
        var res = Application.Current.Resources;
        ClearValue(BackgroundProperty);
        ClearValue(ForegroundProperty);
        IsEnabled = !_service.IsShuttingDown;

        switch (_distro.State)
        {
            case DistroState.Stopped:
                Content = "Start";
                ToolTipService.SetToolTip(this, $"Start {_distro.Name}");
                break;

            case DistroState.Running:
            case DistroState.StopTimedOut:
                bool force = _distro.State == DistroState.StopTimedOut;
                if (IsConfirming)
                {
                    Content = force ? "Force stop?" : "Stop?";
                    Background = (Brush)res["SystemFillColorCriticalBrush"];
                    Foreground = (Brush)res["TextOnAccentFillColorPrimaryBrush"];
                    ScheduleConfirmationLapse();
                }
                else
                {
                    Content = force ? "Force stop" : "Stop";
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
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        panel.Children.Add(new ProgressRing { IsActive = true, Width = 14, Height = 14 });
        panel.Children.Add(new TextBlock { Text = text });
        return panel;
    }

    private string StopTip()
    {
        if (_distro.HasSystemd)
            return $"Shuts {_distro.Name} down cleanly: services stop and programs get a chance to exit. Everything running in it stops.";
        if (_distro.InitSystem != null)
            return $"{_distro.Name} doesn't run systemd, so it is stopped immediately and programs get no warning.";
        return $"Stops {_distro.Name}. Everything running in it stops.";
    }

    private string ForceStopTip() =>
        $"{_distro.Name} didn't finish shutting down within {WslMonitorService.CleanStopLimit.TotalMinutes:0} minutes. " +
        "Force stop ends it immediately; programs still running get no warning.";

    /// <summary>Revert to the plain button when the confirmation lapses, if this button is still shown then.</summary>
    private void ScheduleConfirmationLapse()
    {
        var remaining = PendingConfirmations[_distro.Id] - DateTime.UtcNow;
        var timer = DispatcherQueue.CreateTimer();
        timer.Interval = remaining > TimeSpan.Zero ? remaining : TimeSpan.FromMilliseconds(1);
        timer.IsRepeating = false;
        timer.Tick += (_, _) => Render();
        timer.Start();
    }

    // ── Actions ─────────────────────────────────────────────────────────
    private async void OnClick(object sender, RoutedEventArgs e)
    {
        switch (_distro.State)
        {
            case DistroState.Stopped:
                await _service.StartAsync(_distro);
                break;

            case DistroState.Running:
            case DistroState.StopTimedOut:
                if (!IsConfirming)
                {
                    PendingConfirmations[_distro.Id] = DateTime.UtcNow + ConfirmWindow;
                    Render();
                    return;
                }

                PendingConfirmations.Remove(_distro.Id);
                if (_distro.State == DistroState.StopTimedOut) await _service.ForceStopAsync(_distro);
                else await _service.StopAsync(_distro);
                break;
        }
    }
}
