using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;
using WinUIAppKit.Localization;
using WslMonitor.Services;

namespace WslMonitor.Controls;

/// <summary>
/// The WSL 2 VM's CPU and memory as two small cards: a CPU figure over a one-minute sparkline, and
/// memory in use over a meter against the VM's limit. It only shows what it is given: the owner
/// calls <see cref="Show"/> after each reading of a <see cref="WslVmUsageSampler"/>, and the values
/// are updated in place, so the cards never flicker or lose their tooltip while open.
/// </summary>
public sealed partial class VmUsageStrip : UserControl
{
    private const double BytesPerGb = 1024.0 * 1024 * 1024;

    /// <summary>Shown until the second reading gives a CPU figure.</summary>
    private const string Pending = "…";

    private WslVmUsageTracker? _usage;
    private WslVmLimits _limits;

    public VmUsageStrip()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Show the latest reading. The strip hides itself while the VM isn't running (only WSL 1 distros,
    /// or nothing, running), since there is nothing to measure.
    /// </summary>
    public void Show(WslVmUsageTracker usage, WslVmLimits limits)
    {
        _usage = usage;
        _limits = limits;
        Visibility = usage.IsRunning ? Visibility.Visible : Visibility.Collapsed;
        if (!usage.IsRunning)
        {
            return;
        }

        ShowCpu(usage);
        ShowMemory(usage, limits);
    }

    /// <summary>Re-word the figures and tooltips in the current interface language.</summary>
    public void Relabel()
    {
        if (_usage != null)
        {
            Show(_usage, _limits);
        }
    }

    // ── CPU ─────────────────────────────────────────────────────────────
    private void ShowCpu(WslVmUsageTracker usage)
    {
        string value = usage.CpuPercent is { } percent ? Percent(percent) : Pending;
        CpuValue.Text = value;

        string tip = UiText.Get("usage.cpu-tip");
        SetTip(CpuCard, tip);
        AutomationProperties.SetHelpText(CpuValue, tip);

        DrawSparkline(usage.CpuHistory);
    }

    /// <summary>
    /// Draw the CPU history on a fixed 0 to 100% scale (never stretched to its own peak, which would make
    /// 3% look like a spike), newest reading at the right edge so the line grows leftwards from the
    /// moment the flyout opens.
    /// </summary>
    private void DrawSparkline(IReadOnlyList<double> history)
    {
        double width = CpuChart.ActualWidth;
        double height = CpuChart.ActualHeight;
        var line = new Microsoft.UI.Xaml.Media.PointCollection();
        var area = new Microsoft.UI.Xaml.Media.PointCollection();

        if (history.Count >= 2 && width > 0 && height > 0)
        {
            // Keep the stroke inside the chart: half its width clear of the top and bottom.
            double inset = CpuLine.StrokeThickness / 2;
            double step = width / (WslVmUsageTracker.HistoryLength - 1);
            double left = width - (history.Count - 1) * step;

            for (int i = 0; i < history.Count; i++)
            {
                double x = left + i * step;
                double y = inset + (height - 2 * inset) * (1 - history[i] / 100);
                line.Add(new Point(x, y));
            }

            area.Add(new Point(left, height));
            foreach (var point in line)
            {
                area.Add(point);
            }

            area.Add(new Point(width, height));
        }

        CpuLine.Points = line;
        CpuArea.Points = area;
    }

    private void CpuChart_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_usage != null)
        {
            DrawSparkline(_usage.CpuHistory);
        }
    }

    // ── Memory ──────────────────────────────────────────────────────────
    private void ShowMemory(WslVmUsageTracker usage, WslVmLimits limits)
    {
        string limitText = Gigabytes(limits.MemoryBytes);
        string used = Gigabytes(usage.MemoryBytes);
        string limit = UiText.Format("usage.of-limit", ("limit", limitText));
        MemoryValue.Text = used;
        MemoryLimit.Text = limit;

        // The VM can briefly hold a little more than its limit (its own overhead); the bar stops full.
        double share = Math.Clamp(usage.MemoryBytes / (double)limits.MemoryBytes, 0, 1);
        MemoryUsedColumn.Width = new GridLength(share, GridUnitType.Star);
        MemoryFreeColumn.Width = new GridLength(1 - share, GridUnitType.Star);

        string tip = UiText.Format(
            limits.MemoryFromConfig ? "usage.memory-tip-config" : "usage.memory-tip-default",
            ("limit", limitText));
        SetTip(MemoryCard, tip);
        AutomationProperties.SetHelpText(MemoryValue, limit + ". " + tip);
    }

    /// <summary>
    /// Set a card's tooltip only when its text changed. Setting it replaces the tooltip, which restarts
    /// the hover delay, so doing it on every one-second reading kept it from ever opening.
    /// </summary>
    private static void SetTip(FrameworkElement card, string tip)
    {
        if (ToolTipService.GetToolTip(card) as string != tip)
        {
            ToolTipService.SetToolTip(card, tip);
        }
    }

    // ── Formatting ──────────────────────────────────────────────────────
    /// <summary>A whole percentage in the current interface language ("12%", "12 %").</summary>
    private static string Percent(double percent) =>
        UiText.Format("usage.percent", ("percent", Math.Round(percent).ToString("0", CultureInfo.CurrentCulture)));

    /// <summary>
    /// Gigabytes (powers of 1024, as Task Manager and .wslconfig count them) with one decimal under 10
    /// and none above, so the figure stays short as it changes: "3.2 GB", "12 GB" (the unit from the
    /// shared <c>unit.gb</c> text). Always GB, even below 1, so the figure and its limit compare at a glance.
    /// </summary>
    private static string Gigabytes(long bytes)
    {
        double gb = bytes / BytesPerGb;
        string number = gb.ToString(gb < 10 ? "0.#" : "0", CultureInfo.CurrentCulture);
        return UiText.Format("unit.gb", ("value", number));
    }
}
