using WinUIAppKit.Theming;
using WinUIAppKit.Windowing;

namespace WslMonitor.ViewModels;

/// <summary>
/// User settings, saved as settings.json by <see cref="Classes.SettingsManager"/>. The kit's store owns
/// the file's <c>SchemaVersion</c>; never declare one here. A change to the shape needs a step in
/// <see cref="Classes.SettingsMigrations"/>.
/// </summary>
public sealed class UserSettings
{
    public AppThemeChoice AppTheme { get; set; }

    /// <summary>Interface language identity (for example "fr-CA"), or null to follow Windows.</summary>
    public string? Language { get; set; }

    /// <summary>Start at sign-in. Registered once on first run; afterwards the startup task/Run key is the truth.</summary>
    public bool Startup { get; set; } = true;

    /// <summary>Also list Docker Desktop's internal distros.</summary>
    public bool ShowSystemDistros { get; set; }

    /// <summary>Last normal position and size of the details window.</summary>
    public WindowPlacement? DetailsWindow { get; set; }
}
