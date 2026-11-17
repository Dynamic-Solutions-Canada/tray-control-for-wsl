using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinUIAppKit.Localization;
using WinUIAppKit.Settings;
using WslMonitor.Classes;

namespace WslMonitor.Pages;

/// <summary>
/// App-wide settings (the NavigationView's built-in Settings item). The startup, language and theme rows
/// are the kit's shared Settings rows (UI-4); this page saves what they report. Edits apply and save
/// immediately.
/// </summary>
public sealed partial class GeneralPage : Page
{
    private bool _initializing = true;

    public GeneralPage()
    {
        InitializeComponent();

        StartupRow.Registration = MainWindow.Startup;
        StartupRow.StatusChanged += isOn =>
        {
            SettingsManager.Current.Startup = isOn;
            SettingsManager.Save();
        };
        LanguageRow.ChoiceChanged += identity =>
        {
            SettingsManager.Current.Language = identity;
            SettingsManager.Save();
        };
        ThemeRow.ChoiceChanged += choice =>
        {
            SettingsManager.Current.AppTheme = choice;
            SettingsManager.Save();
        };

        SystemDistrosSwitch.IsOn = SettingsManager.Current.ShowSystemDistros;
        _initializing = false;

        // Shown once, on the launch that found the settings file unreadable (CONF-2, UI-15).
        if (SettingsManager.TakeResetNotice() is { } notice)
        {
            NoticeHost.Content = SettingsResetNotice.Create(notice.BackupPath);
        }
    }

    private void SystemDistrosSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_initializing)
        {
            return;
        }

        SettingsManager.Current.ShowSystemDistros = SystemDistrosSwitch.IsOn;
        SettingsManager.Save();
        MainWindow.Service.SettingsChanged();
    }

    // ── Reset ───────────────────────────────────────────────────────────
    /// <summary>
    /// "Reset settings" (CONF-4): asks first (the kit's dialog defaults to Cancel), then resets, lets
    /// the service re-apply the Docker distros setting, and opens this page again so every row shows
    /// the defaults. Start with Windows is Windows' state; its row reads it again.
    /// </summary>
    private async void ResetSettings_Click(object sender, RoutedEventArgs e)
    {
        bool confirmed = await DetailsWindow.ConfirmAsync(
            UiText.Get("settings.reset-title"),
            UiText.Get("settings.reset-body"),
            UiText.Get("settings.reset-confirm"));
        if (!confirmed)
        {
            return;
        }

        SettingsManager.Reset();
        MainWindow.Service.SettingsChanged();
        DetailsWindow.ReloadSettingsPage();
    }
}
