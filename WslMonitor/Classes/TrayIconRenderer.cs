using System.Drawing;
using System.Drawing.Drawing2D;
using WinUIAppKit.Tray;

namespace WslMonitor.Classes;

/// <summary>What the tray icon should say at a glance.</summary>
public enum TrayStatus
{
    /// <summary>No distro is running.</summary>
    Idle,

    /// <summary>At least one distro is running.</summary>
    Running,

    /// <summary>Something is starting or stopping.</summary>
    Changing,
}

/// <summary>
/// The tray icon's artwork: a terminal window with a prompt, outlined in the taskbar's text colour, plus a
/// status dot (green running, amber changing, none when idle). The kit's <see cref="TrayIconRasterizer"/>
/// turns it into PNG bytes at exactly the pixel size the tray uses, for <see cref="TrayIcon.Update"/>.
/// </summary>
internal static class TrayIconRenderer
{
    private static readonly Color Green = Color.FromArgb(34, 197, 94);
    private static readonly Color Amber = Color.FromArgb(245, 158, 11);

    /// <summary>Renders the icon as PNG bytes.</summary>
    /// <param name="status">What the icon shows at a glance.</param>
    /// <param name="darkTaskbar">From <see cref="TrayIcon.TaskbarIsDark"/>; picks the outline and ring colours.</param>
    /// <param name="size">The icon's pixel size, normally <see cref="TrayIcon.IconPixelSize"/>.</param>
    public static byte[] Render(TrayStatus status, bool darkTaskbar, int size)
    {
        var ink = TrayIconRasterizer.Ink(darkTaskbar);
        var outline = Color.FromArgb(status == TrayStatus.Idle ? 150 : 255, ink);

        return TrayIconRasterizer.Render(size, (g, side) =>
        {
            DrawTerminal(g, side, outline);
            if (status != TrayStatus.Idle)
            {
                DrawDot(g, side, status == TrayStatus.Running ? Green : Amber, darkTaskbar);
            }
        });
    }

    /// <summary>Rounded window outline with a "&gt;_" prompt, on a canvas <paramref name="m"/> pixels square.</summary>
    private static void DrawTerminal(Graphics g, float m, Color ink)
    {
        var frame = new RectangleF(m * 0.06f, m * 0.14f, m * 0.88f, m * 0.72f);
        using (var pen = new Pen(ink, m * 0.085f))
        using (var path = TrayIconRasterizer.RoundedRectangle(frame, m * 0.12f))
        {
            g.DrawPath(pen, path);
        }

        using var stroke = new Pen(ink, m * 0.09f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };

        // Chevron
        g.DrawLines(stroke, new[]
        {
            new PointF(m * 0.24f, m * 0.36f),
            new PointF(m * 0.40f, m * 0.50f),
            new PointF(m * 0.24f, m * 0.64f),
        });

        // Cursor
        g.DrawLine(stroke, m * 0.48f, m * 0.64f, m * 0.66f, m * 0.64f);
    }

    /// <summary>Status dot in the bottom-right corner, ringed in the taskbar colour so it reads on the outline.</summary>
    private static void DrawDot(Graphics g, float m, Color color, bool darkTaskbar)
    {
        float r = m * 0.20f, cx = m * 0.80f, cy = m * 0.78f;
        using (var b = new SolidBrush(TrayIconRasterizer.Surface(darkTaskbar)))
        {
            g.FillEllipse(b, cx - r - m * 0.05f, cy - r - m * 0.05f, 2 * r + m * 0.1f, 2 * r + m * 0.1f);
        }

        using (var b = new SolidBrush(color))
        {
            g.FillEllipse(b, cx - r, cy - r, 2 * r, 2 * r);
        }
    }
}
