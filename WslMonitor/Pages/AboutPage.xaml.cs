using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using WinUIAppKit;
using WinUIAppKit.Localization;
using WslMonitor.Classes;

namespace WslMonitor.Pages;

/// <summary>Version, description and the logs folder.</summary>
public sealed partial class AboutPage : LivePage
{
    public AboutPage()
    {
        InitializeComponent();
        LogsPath.Text = SettingsManager.PhysicalDataDirectory;
        try { AppIcon.Source = new BitmapImage(new Uri(App.IconPngPath)); } catch { }
    }

    protected override void OnAttach()
    {
        UiText.Changed += ShowVersion;
        ShowVersion();
    }

    protected override void OnDetach() => UiText.Changed -= ShowVersion;

    private void ShowVersion() =>
        VersionText.Text = UiText.Format(AppEnvironment.IsPackaged ? "about.version" : "about.version-unpackaged",
            ("version", AppEnvironment.Version));

    private void OpenLogs_Click(object sender, RoutedEventArgs e) => MainWindow.OpenLogsFolder();
}
