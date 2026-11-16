using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinUIAppKit.Localization;
using WslMonitor.Models;
using WslMonitor.Services;

namespace WslMonitor.Controls;

/// <summary>
/// How a distro looks wherever it is listed (tray flyout, details window), so the wording and colours
/// can't drift between the two.
/// </summary>
public static class DistroVisuals
{
    /// <summary>Segoe Fluent "command prompt" glyph used as the distro icon.</summary>
    public const string Glyph = "";

    /// <summary>Short state label ("Running", "Stopping…") in the current interface language.</summary>
    public static string StateText(Distro d) => UiText.Get(d.State switch
    {
        DistroState.Running => "state.running",
        DistroState.Starting => "state.starting",
        DistroState.Stopping => "state.stopping",
        DistroState.StopTimedOut => "state.stop-timed-out",
        DistroState.Busy => d.BusyReason.Length > 0 ? d.BusyReason : "state.busy",
        _ => "state.stopped",
    });

    /// <summary>One-line summary: state · OS · WSL version · default.</summary>
    public static string Summary(Distro d)
    {
        var parts = new List<string> { StateText(d) };
        if (d.OsLabel.Length > 0) parts.Add(d.OsLabel);
        parts.Add($"WSL {d.WslVersion}");
        if (d.IsDefault) parts.Add(UiText.Get("distro.default"));
        return string.Join("  ·  ", parts);
    }

    /// <summary>
    /// What Stop will do, the one place this policy is decided for every label and tooltip:
    /// a clean systemd shutdown, an immediate terminate, or not known until the distro has run.
    /// </summary>
    public static StopKind StopKindOf(Distro d)
    {
        if (d.HasSystemd) return StopKind.Clean;
        if (d.InitSystem != null) return StopKind.Immediate;
        return StopKind.Unknown;
    }

    /// <summary>
    /// One-line status of all listed distros ("2 of 3 running", "Shutting down WSL…"), shared by the
    /// flyout footer and the tray tooltip. Empty when there are no distros to summarise.
    /// </summary>
    public static string StatusLine(WslMonitorService service)
    {
        if (service.IsShuttingDown) return UiText.Get("status.shutting-down");
        if (service.IsStateUnknown) return UiText.Get("status.unresponsive");
        if (!service.IsWslInstalled) return UiText.Get("list.no-wsl");

        var distros = service.Distros;
        if (distros.Count == 0) return UiText.Get("list.no-distros");

        int running = service.RunningCount;
        if (running == 0) return UiText.Get("status.nothing-running");
        return UiText.Format("status.some-running", ("running", running), ("total", distros.Count));
    }

    /// <summary>Round icon badge: accent-filled when the distro is on, subtle otherwise.</summary>
    public static Border Badge(Distro d, double size = 40)
    {
        var res = Application.Current.Resources;
        bool on = d.IsOn;
        return new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(size / 2),
            Background = (Brush)res[on ? "AccentFillColorDefaultBrush" : "SubtleFillColorSecondaryBrush"],
            Child = new FontIcon
            {
                Glyph = Glyph,
                FontSize = size * 0.45,
                Foreground = (Brush)res[on ? "TextOnAccentFillColorPrimaryBrush" : "TextFillColorPrimaryBrush"],
            },
        };
    }

    /// <summary>Caption under the name: the last error in the critical colour, else the summary.</summary>
    public static TextBlock Caption(Distro d)
    {
        var res = Application.Current.Resources;
        string? error = d.LastError?.Text;
        var caption = new TextBlock
        {
            Text = error ?? Summary(d),
            Style = (Style)res["CaptionTextBlockStyle"],
            Foreground = (Brush)res[error != null ? "SystemFillColorCriticalBrush" : "TextFillColorSecondaryBrush"],
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        if (error != null) ToolTipService.SetToolTip(caption, error);
        return caption;
    }
}

/// <summary>What Stop does for a distro (see <see cref="DistroVisuals.StopKindOf"/>).</summary>
public enum StopKind
{
    /// <summary>systemd shuts it down cleanly.</summary>
    Clean,

    /// <summary>No systemd: WSL ends it immediately.</summary>
    Immediate,

    /// <summary>Not known until it has run once.</summary>
    Unknown,
}
