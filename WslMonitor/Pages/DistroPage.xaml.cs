using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using WinUIAppKit.Localization;
using WslMonitor.Controls;
using WslMonitor.Models;
using WslMonitor.Services;

namespace WslMonitor.Pages;

/// <summary>One distro: state with Start / Stop, quick launches, and its registration details.</summary>
public sealed partial class DistroPage : LivePage
{
    private static WslMonitorService Service => MainWindow.Service;

    /// <summary>The distro this page shows (navigation parameter).</summary>
    public string DistroName { get; private set; } = "";

    public DistroPage() => InitializeComponent();

    protected override void OnAttach()
    {
        Service.Changed += Refresh;
        UiText.Changed += Refresh;   // the status and details are built in code
        Refresh();
    }

    protected override void OnDetach()
    {
        Service.Changed -= Refresh;
        UiText.Changed -= Refresh;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        DistroName = e.Parameter as string ?? "";
        base.OnNavigatedTo(e);
    }

    private void Refresh()
    {
        TitleText.Text = DistroName;
        if (Service.Find(DistroName) is not { } d)
        {
            // Unregistered while open; the window's menu rebuild will move away from this page.
            StateText.Text = UiText.Get("distro.missing");
            PowerHost.Content = null;
            return;
        }

        BadgeHost.Content = DistroVisuals.Badge(d, 48);
        StateText.Text = DistroVisuals.StateText(d);
        StopBehaviourText.Text = StopBehaviour(d);
        ErrorText.Text = d.LastError?.Text ?? "";
        ErrorText.Visibility = d.LastError != null ? Visibility.Visible : Visibility.Collapsed;
        PowerHost.Content = new DistroPowerButton(d, Service);
        DefaultButton.IsEnabled = !d.IsDefault && d.State != DistroState.Busy;

        BuildDetails(d);
    }

    /// <summary>What Stop will do for this distro, in plain words.</summary>
    private static string StopBehaviour(Distro d)
    {
        // Force stop is offered after a failed clean-stop check too; the error explains which case it is.
        if (d.State == DistroState.StopTimedOut && d.LastError != null)
        {
            return UiText.Get("distro.force-offered");
        }

        if (d.State == DistroState.StopTimedOut)
        {
            return UiText.Format("distro.stop-timed-out", ("minutes", (int)WslMonitorService.CleanStopLimit.TotalMinutes));
        }

        return UiText.Get(DistroVisuals.StopKindOf(d) switch
        {
            StopKind.Clean => "distro.stop-systemd",
            StopKind.Immediate => "distro.stop-no-systemd",
            _ => "distro.stop-unknown",
        });
    }

    private void BuildDetails(Distro d)
    {
        DetailsGrid.Children.Clear();
        DetailsGrid.RowDefinitions.Clear();

        AddDetail("distro.wsl-version", $"WSL {d.WslVersion}");
        if (d.OsLabel.Length > 0)
        {
            AddDetail("distro.os", d.OsLabel);
        }

        AddDetail("distro.default-label", UiText.Get(d.IsDefault ? "common.yes" : "common.no"));
        AddDetail("distro.init", d.InitSystem ?? UiText.Get("distro.init-unknown"));
        if (d.BasePath.Length > 0)
        {
            AddDetail("distro.location", d.BasePath);
        }

        if (WslRegistry.DiskSize(d.DiskPath) is { } bytes)
        {
            AddDetail("distro.disk-size", FormatBytes(bytes));
        }
    }

    /// <param name="labelKey">Interface text key of the label.</param>
    /// <param name="value">The value, shown as is (paths and names are never translated).</param>
    private void AddDetail(string labelKey, string value)
    {
        int row = DetailsGrid.RowDefinitions.Count;
        DetailsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var name = new TextBlock
        {
            Text = UiText.Get(labelKey),
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        };
        var text = new TextBlock { Text = value, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };

        Grid.SetRow(name, row);
        Grid.SetRow(text, row);
        Grid.SetColumn(text, 1);
        DetailsGrid.Children.Add(name);
        DetailsGrid.Children.Add(text);
    }

    /// <summary>
    /// Disk size as Windows shows it (binary units, labelled GB/MB/KB, or Go/Mo/Ko in French), with
    /// the device's decimal separator.
    /// </summary>
    private static string FormatBytes(long bytes) => bytes switch
    {
        >= 1L << 30 => UiText.Format("unit.gb", ("value", Math.Round(bytes / (double)(1L << 30), 1))),
        >= 1L << 20 => UiText.Format("unit.mb", ("value", Math.Round(bytes / (double)(1L << 20)))),
        _ => UiText.Format("unit.kb", ("value", Math.Round(bytes / 1024.0))),
    };

    // ── Actions ─────────────────────────────────────────────────────────
    private void Terminal_Click(object sender, RoutedEventArgs e) => WslCli.OpenTerminal(DistroName);

    private void Files_Click(object sender, RoutedEventArgs e) => WslCli.OpenFiles(DistroName);

    private async void Default_Click(object sender, RoutedEventArgs e)
    {
        if (Service.Find(DistroName) is { } d)
        {
            await Service.SetDefaultAsync(d);
        }
    }
}
