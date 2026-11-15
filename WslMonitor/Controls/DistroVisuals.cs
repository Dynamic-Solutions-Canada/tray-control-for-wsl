using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinUIAppKit.Localization;
using WslMonitor.Models;

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

    /// <summary>True when the distro counts as on (shown with the accent colour).</summary>
    public static bool IsOn(Distro d) =>
        d.State is DistroState.Running or DistroState.Stopping or DistroState.StopTimedOut;

    /// <summary>Round icon badge: accent-filled when the distro is on, subtle otherwise.</summary>
    public static Border Badge(Distro d, double size = 40)
    {
        var res = Application.Current.Resources;
        bool on = IsOn(d);
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
