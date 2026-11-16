using WinUIAppKit.Theming;
using WinUIAppKit.Windowing;

namespace WslMonitor.ViewModels;

/// <summary>User settings, saved as settings.json by <see cref="Classes.SettingsManager"/>.</summary>
public sealed class UserSettings
{
    /// <summary>Settings file format, for future migrations.</summary>
    public int Version { get; set; } = 1;

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
