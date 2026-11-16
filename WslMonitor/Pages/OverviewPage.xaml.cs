using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinUIAppKit.Localization;
using WslMonitor.Controls;
using WslMonitor.Models;
using WslMonitor.Services;

namespace WslMonitor.Pages;

/// <summary>
/// Every distro with Start / Stop, what "start" means on this PC (WSL's idle timeout), WSL's version
/// details, and "Shut down WSL".
/// </summary>
public sealed partial class OverviewPage : LivePage
{
    private static WslMonitorService Service => MainWindow.Service;

    public OverviewPage() => InitializeComponent();

    protected override void OnAttach()
    {
        Service.Changed += Refresh;
        UiText.Changed += OnLanguageChanged;
        OnLanguageChanged();
    }

    protected override void OnDetach()
    {
        Service.Changed -= Refresh;
        UiText.Changed -= OnLanguageChanged;
    }

    /// <summary>Everything on the page that is built in code, redrawn in the current language.</summary>
    private async void OnLanguageChanged()
    {
        Refresh();
        StartBehaviourText.Text = WslConfigFile.Read().StartBehaviour;
        VersionText.Text = await WslCli.VersionAsync() ?? "";
    }

    private void Refresh()
    {
        DistroList.Fill(DistroRows, Service, d => DetailsWindow.ShowDistro(d.Name));

        // The service owns shutdown progress and its outcome, so they survive leaving this page.
        ShutdownText.Text = Service.ShutdownStatus?.Text ?? UiText.Get("overview.shutdown-desc");
        ShutdownButton.IsEnabled = Service.IsWslInstalled && !Service.IsShuttingDown;
    }

    // ── Shut down WSL ───────────────────────────────────────────────────
    private async void Shutdown_Click(object sender, RoutedEventArgs e)
    {
        // Every running distro, including hidden Docker Desktop ones: the shutdown stops them all.
        var running = Service.AllDistros.Where(d => d.IsOn).Select(d => d.Name).ToList();
        string what = running.Count == 0
            ? UiText.Get("overview.confirm-none")
            : UiText.Format("overview.confirm-some", ("names", new NameList(running)));

        if (!await DetailsWindow.ConfirmAsync(UiText.Get("overview.confirm-title"), what, UiText.Get("overview.confirm-button"))) return;

        // Asked from the window, not this page: the user may have moved to another page meanwhile.
        await Service.ShutdownAllAsync(stuck => DetailsWindow.ConfirmAsync(
            UiText.Get("overview.force-title"),
            UiText.Format("overview.force-body",
                ("names", new NameList(stuck)),
                ("minutes", (int)WslMonitorService.CleanStopLimit.TotalMinutes)),
            UiText.Get("overview.force-button")));
    }
}
