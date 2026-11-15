using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using WinUIAppKit;
using WinUIAppKit.Localization;
using WslMonitor.Classes;

namespace WslMonitor.Pages;

/// <summary>Version, description and the logs folder.</summary>
public sealed partial class AboutPage : Page
{
    public AboutPage()
    {
        InitializeComponent();
        VersionText.Text = UiText.Format(AppEnvironment.IsPackaged ? "about.version" : "about.version-unpackaged",
            ("version", AppEnvironment.Version));
        LogsPath.Text = SettingsManager.PhysicalDataDirectory;
        try { AppIcon.Source = new BitmapImage(new Uri(App.IconPngPath)); } catch { }
    }

    private void OpenLogs_Click(object sender, RoutedEventArgs e) => MainWindow.OpenLogsFolder();
}
