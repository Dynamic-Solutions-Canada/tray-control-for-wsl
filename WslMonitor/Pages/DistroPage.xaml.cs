using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using WslMonitor.Controls;
using WslMonitor.Models;
using WslMonitor.Services;

namespace WslMonitor.Pages;

/// <summary>One distro: state with Start / Stop, quick launches, and its registration details.</summary>
public sealed partial class DistroPage : Page
{
    private static WslMonitorService Service => MainWindow.Service;

    /// <summary>The distro this page shows (navigation parameter).</summary>
    public string DistroName { get; private set; } = "";

    public DistroPage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            Service.Changed += Refresh;
            Refresh();
        };
        Unloaded += (_, _) => Service.Changed -= Refresh;
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
            StateText.Text = "This distro is no longer installed.";
            PowerHost.Content = null;
            return;
        }

        BadgeHost.Content = DistroVisuals.Badge(d, 48);
        StateText.Text = DistroVisuals.StateText(d);
        StopBehaviourText.Text = StopBehaviour(d);
        ErrorText.Text = d.LastError ?? "";
        ErrorText.Visibility = d.LastError != null ? Visibility.Visible : Visibility.Collapsed;
        PowerHost.Content = new DistroPowerButton(d, Service);
        DefaultButton.IsEnabled = !d.IsDefault && d.State != DistroState.Busy;

        BuildDetails(d);
    }

    /// <summary>What Stop will do for this distro, in plain words.</summary>
    private static string StopBehaviour(Distro d)
    {
        if (d.State == DistroState.StopTimedOut)
            return $"It didn't finish shutting down within {WslMonitorService.CleanStopLimit.TotalMinutes:0} minutes. Force stop ends it immediately.";
        if (d.HasSystemd)
            return "Stop shuts it down cleanly with systemd: services stop in order and programs get a chance to exit.";
        if (d.InitSystem != null)
            return "It doesn't run systemd, so Stop ends it immediately and programs get no warning.";
        return "Whether Stop can shut it down cleanly is checked the first time it runs.";
    }

    private void BuildDetails(Distro d)
    {
        DetailsGrid.Children.Clear();
        DetailsGrid.RowDefinitions.Clear();

        AddDetail("WSL version", $"WSL {d.WslVersion}");
        if (d.OsLabel.Length > 0) AddDetail("Operating system", d.OsLabel);
        AddDetail("Default distro", d.IsDefault ? "Yes" : "No");
        AddDetail("Init system", d.InitSystem ?? "Shown once it has run");
        if (d.BasePath.Length > 0) AddDetail("Location", d.BasePath);
        if (WslRegistry.DiskSize(d.DiskPath) is { } bytes) AddDetail("Disk size", FormatBytes(bytes));
    }

    private void AddDetail(string label, string value)
    {
        int row = DetailsGrid.RowDefinitions.Count;
        DetailsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var name = new TextBlock
        {
            Text = label,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        };
        var text = new TextBlock { Text = value, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };

        Grid.SetRow(name, row);
        Grid.SetRow(text, row);
        Grid.SetColumn(text, 1);
        DetailsGrid.Children.Add(name);
        DetailsGrid.Children.Add(text);
    }

    /// <summary>Disk size as Windows shows it (binary units, labelled GB/MB).</summary>
    private static string FormatBytes(long bytes) => bytes switch
    {
        >= 1L << 30 => $"{bytes / (double)(1L << 30):0.0} GB",
        >= 1L << 20 => $"{bytes / (double)(1L << 20):0} MB",
        _ => $"{bytes / 1024.0:0} KB",
    };

    // ── Actions ─────────────────────────────────────────────────────────
    private void Terminal_Click(object sender, RoutedEventArgs e) => WslCli.OpenTerminal(DistroName);

    private void Files_Click(object sender, RoutedEventArgs e) => WslCli.OpenFiles(DistroName);

    private async void Default_Click(object sender, RoutedEventArgs e)
    {
        if (Service.Find(DistroName) is { } d) await Service.SetDefaultAsync(d);
    }
}
