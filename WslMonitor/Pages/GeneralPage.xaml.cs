using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinUIAppKit.Theming;
using WslMonitor.Classes;

namespace WslMonitor.Pages;

/// <summary>App-wide settings (the NavigationView's built-in Settings item). Edits apply and save immediately.</summary>
public sealed partial class GeneralPage : Page
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    private bool _initializing = true;
    private bool _updatingStartupSwitch;

    public GeneralPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            var settings = SettingsManager.Current;
            ThemeCombo.SelectedIndex = Math.Clamp((int)settings.AppTheme, 0, 2);
            SystemDistrosSwitch.IsOn = settings.ShowSystemDistros;

            _updatingStartupSwitch = true;
            try { StartupSwitch.IsOn = await MainWindow.Startup.IsEnabledAsync(); }
            catch (Exception ex) { Logger.Warn(ex, "Couldn't read startup state"); }
            finally { _updatingStartupSwitch = false; }

            _initializing = false;
        };
    }

    private async void StartupSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_updatingStartupSwitch || _initializing) return;
        _updatingStartupSwitch = true;
        try
        {
            bool result = await MainWindow.Startup.SetEnabledAsync(StartupSwitch.IsOn);
            StartupSwitch.IsOn = result;   // the user or policy may refuse
            SettingsManager.Current.Startup = result;
            SettingsManager.Save();
        }
        catch (Exception ex) { Logger.Warn(ex, "Couldn't change startup state"); }
        finally { _updatingStartupSwitch = false; }
    }

    private void SystemDistrosSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        SettingsManager.Current.ShowSystemDistros = SystemDistrosSwitch.IsOn;
        SettingsManager.Save();
        MainWindow.Service.SettingsChanged();
    }

    private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing) return;
        var choice = (AppThemeChoice)ThemeCombo.SelectedIndex;
        SettingsManager.Current.AppTheme = choice;
        SettingsManager.Save();
        AppTheme.Set(choice);
    }
}
