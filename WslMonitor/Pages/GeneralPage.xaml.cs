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

        // The kit's Reset row asks on the details window, then runs the reset below (CONF-4). The window
        // is read once the page is shown, since the page never outlives the window that hosts it.
        Loaded += (_, _) => ResetRow.Window = DetailsWindow.Instance;
        ResetRow.ResetAsync = ResetSettingsAsync;
        ResetRow.ResetCompleted += DetailsWindow.ReloadSettingsPage;

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
    /// The app's part of "Reset settings", run by the kit's row once the user confirms: reset the store
    /// (which keeps the old file and applies the default theme and language), then let the service
    /// re-apply the Docker distros setting. The row then raises ResetCompleted, which opens this page
    /// again so every row shows the defaults. Start with Windows is Windows' state and is left alone.
    /// </summary>
    private static Task ResetSettingsAsync()
    {
        SettingsManager.Reset();
        MainWindow.Service.SettingsChanged();
        return Task.CompletedTask;
    }
}
