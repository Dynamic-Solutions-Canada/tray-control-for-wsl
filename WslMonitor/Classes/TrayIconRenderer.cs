using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

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
/// Draws the tray icon: a terminal window with a prompt, outlined in the taskbar's text colour, plus a
/// status dot (green running, amber changing, none when idle). Rendered at the exact pixel size the
/// tray uses (supersampled 4×) and returned as PNG bytes for <see cref="WinUIAppKit.Tray.TrayIcon.Update"/>.
/// </summary>
internal static class TrayIconRenderer
{
    private static readonly Color Green = Color.FromArgb(34, 197, 94);
    private static readonly Color Amber = Color.FromArgb(245, 158, 11);

    /// <param name="size">Target pixel size (<see cref="WinUIAppKit.Tray.TrayIcon.IconPixelSize"/>).</param>
    /// <param name="darkTaskbar">True to draw in white for a dark taskbar.</param>
    public static byte[] Render(TrayStatus status, bool darkTaskbar, int size)
    {
        size = Math.Clamp(size, 16, 256);
        int m = size * 4;
        var ink = darkTaskbar ? Color.White : Color.FromArgb(32, 32, 32);
        var dim = Color.FromArgb(status == TrayStatus.Idle ? 150 : 255, ink);

        using var big = new Bitmap(m, m, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(big))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            DrawTerminal(g, m, dim);
            if (status != TrayStatus.Idle) DrawDot(g, m, status == TrayStatus.Running ? Green : Amber, darkTaskbar);
        }

        using var small = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(small))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);
            g.DrawImage(big, 0, 0, size, size);
        }

        using var png = new MemoryStream();
        small.Save(png, ImageFormat.Png);
        return png.ToArray();
    }

    /// <summary>Rounded window outline with a "&gt;_" prompt.</summary>
    private static void DrawTerminal(Graphics g, float m, Color ink)
    {
        var frame = new RectangleF(m * 0.06f, m * 0.14f, m * 0.88f, m * 0.72f);
        using (var pen = new Pen(ink, m * 0.085f))
        using (var path = Rounded(frame, m * 0.12f))
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
        var ring = darkTaskbar ? Color.FromArgb(32, 32, 32) : Color.FromArgb(243, 243, 243);
        using (var b = new SolidBrush(ring)) g.FillEllipse(b, cx - r - m * 0.05f, cy - r - m * 0.05f, 2 * r + m * 0.1f, 2 * r + m * 0.1f);
        using (var b = new SolidBrush(color)) g.FillEllipse(b, cx - r, cy - r, 2 * r, 2 * r);
    }

    private static GraphicsPath Rounded(RectangleF r, float radius)
    {
        var p = new GraphicsPath();
        float d = Math.Max(0.1f, radius * 2);
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}
