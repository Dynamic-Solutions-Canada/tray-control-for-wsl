using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinUIAppKit.Localization;
using WslMonitor.Controls;
using WslMonitor.Services;

namespace WslMonitor.Pages;

/// <summary>
/// Every distro with Start / Stop, what "start" means on this PC (WSL's idle timeout), WSL's version
/// details, and "Shut down WSL".
/// </summary>
public sealed partial class OverviewPage : Page
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
    private static WslMonitorService Service => MainWindow.Service;

    public OverviewPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            Service.Changed += Refresh;
            Refresh();
            ShutdownText.Text = UiText.Get("overview.shutdown-desc");
            StartBehaviourText.Text = WslConfigFile.Read().StartBehaviour;
            VersionText.Text = await WslCli.VersionAsync() ?? "";
        };
        Unloaded += (_, _) => Service.Changed -= Refresh;
    }

    private void Refresh()
    {
        DistroList.Children.Clear();
        if (!Service.IsWslInstalled)
        {
            DistroList.Children.Add(Message(UiText.Get("list.no-wsl")));
        }
        else if (Service.Distros.Count == 0)
        {
            DistroList.Children.Add(Message(UiText.Get("list.no-distros")));
        }
        else
        {
            foreach (var d in Service.Distros)
                DistroList.Children.Add(DistroRow.Build(d, Service, () => DetailsWindow.ShowDistro(d.Name)));
        }

        ShutdownButton.IsEnabled = Service.IsWslInstalled && !Service.IsShuttingDown;
    }

    private static TextBlock Message(string text) => new()
    {
        Text = text,
        Margin = new Thickness(10, 8, 10, 8),
        TextWrapping = TextWrapping.Wrap,
        Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
    };

    // ── Shut down WSL ───────────────────────────────────────────────────
    private async void Shutdown_Click(object sender, RoutedEventArgs e)
    {
        var running = Service.Distros.Where(DistroVisuals.IsOn).Select(d => d.Name).ToList();
        string what = running.Count == 0
            ? UiText.Get("overview.confirm-none")
            : UiText.Format("overview.confirm-some", ("names", WslMonitorService.JoinNames(running)));

        if (!await ConfirmAsync(UiText.Get("overview.confirm-title"), what, UiText.Get("overview.confirm-button"))) return;

        var progress = new Progress<string>(line => ShutdownText.Text = line);
        string? problem = await Service.ShutdownAllAsync(progress, ConfirmForceAsync);
        Logger.Info("Shut down WSL: {Result}", problem ?? "done");
        ShutdownText.Text = problem ?? UiText.Get("overview.shutdown-desc");
    }

    private Task<bool> ConfirmForceAsync(IReadOnlyList<string> stuck) => ConfirmAsync(
        UiText.Get("overview.force-title"),
        UiText.Format("overview.force-body",
            ("names", WslMonitorService.JoinNames(stuck)),
            ("minutes", (int)WslMonitorService.CleanStopLimit.TotalMinutes)),
        UiText.Get("overview.force-button"));

    private async Task<bool> ConfirmAsync(string title, string body, string confirmText)
    {
        // The window may have closed while a shutdown waited; with nowhere to ask, don't force anything.
        if (XamlRoot == null) return false;

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = new TextBlock { Text = body, TextWrapping = TextWrapping.Wrap },
            PrimaryButtonText = confirmText,
            CloseButtonText = UiText.Get("dialog.cancel"),
            DefaultButton = ContentDialogButton.Close,
            RequestedTheme = ActualTheme,
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }
}
