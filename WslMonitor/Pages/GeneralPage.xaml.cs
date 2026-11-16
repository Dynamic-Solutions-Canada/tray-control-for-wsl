using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinUIAppKit.Localization;
using WinUIAppKit.Startup;
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

            // The other controls are live from here; the startup switch has its own guard while it loads.
            _initializing = false;
            await LoadStartupAsync(() => MainWindow.Startup.GetStatusAsync());
        };
    }

    // ── Start with Windows ──────────────────────────────────────────────
    private async void StartupSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_updatingStartupSwitch || _initializing) return;
        bool requested = StartupSwitch.IsOn;
        await LoadStartupAsync(() => MainWindow.Startup.SetEnabledAsync(requested));
    }

    /// <summary>
    /// Run a startup query or change and show its outcome. Windows may refuse a change (the user
    /// turned the app off in Settings, or a policy decides); the switch then shows the real state,
    /// is disabled, and a note says where the decision lives. On an error the real state is re-read.
    /// </summary>
    private async Task LoadStartupAsync(Func<Task<StartupStatus>> operation)
    {
        _updatingStartupSwitch = true;
        try
        {
            ShowStartup(await operation());
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Couldn't read or change the startup state");
            try { ShowStartup(await MainWindow.Startup.GetStatusAsync()); } catch { }
        }
        finally
        {
            _updatingStartupSwitch = false;
        }
    }

    private void ShowStartup(StartupStatus status)
    {
        StartupSwitch.IsOn = status.IsOn();
        StartupSwitch.IsEnabled = status.CanChange();
        SettingsManager.Current.Startup = status.IsOn();
        SettingsManager.Save();

        string? note = status switch
        {
            StartupStatus.DisabledByUser => "settings.startup-user-off",
            StartupStatus.DisabledByPolicy => "settings.startup-policy-off",
            StartupStatus.EnabledByPolicy => "settings.startup-policy-on",
            _ => null,
        };
        StartupNote.Visibility = note != null ? Visibility.Visible : Visibility.Collapsed;
        if (note != null) Localize.SetText(StartupNote, note);
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
