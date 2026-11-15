using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinUIAppKit.Localization;
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
            FillLanguages();
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

    // ── Language ────────────────────────────────────────────────────────
    /// <summary>
    /// "Use system setting" followed by every supported language under its own name (so a user who
    /// can't read the current language can still find theirs). Item tags hold the identity, or null.
    /// </summary>
    private void FillLanguages()
    {
        LanguageCombo.Items.Clear();
        var system = new ComboBoxItem { Tag = null };
        Localize.SetContent(system, "settings.use-system");
        LanguageCombo.Items.Add(system);

        foreach (string identity in UiText.Languages.Identities)
        {
            LanguageCombo.Items.Add(new ComboBoxItem
            {
                Content = UiText.Languages.NativeName(identity),
                Tag = identity,
                Language = identity,   // so the font and line breaking suit that language's script
            });
        }

        LanguageCombo.SelectedItem = LanguageCombo.Items.OfType<ComboBoxItem>()
            .FirstOrDefault(item => (item.Tag as string) == UiText.Override) ?? system;
    }

    private void LanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing || LanguageCombo.SelectedItem is not ComboBoxItem item) return;
        string? choice = item.Tag as string;
        SettingsManager.Current.Language = choice;
        SettingsManager.Save();
        UiText.SetOverride(choice);   // open windows relabel on UiText.Changed
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
