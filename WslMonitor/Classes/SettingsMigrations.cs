using System.Text.Json.Nodes;

namespace WslMonitor.Classes;

/// <summary>
/// Upgrades for older settings files, one step per schema version (CONF-3). The kit's versioned
/// <see cref="WinUIAppKit.Settings.JsonSettingsStore{T}"/> backs the file up first, then runs the step at
/// index <c>n</c> to take it from version <c>n</c> to <c>n + 1</c>. A file with no
/// <c>SchemaVersion</c> is version 0. Steps are never removed or reordered once shipped.
/// </summary>
internal static class SettingsMigrations
{
    /// <summary>The steps, in order; the current schema version is their count.</summary>
    public static IReadOnlyList<Action<JsonObject>> Steps { get; } =
    [
        FromUnversioned,
    ];

    /// <summary>
    /// 0 to 1: files written before the kit's versioned store carried the app's own <c>"Version": 1</c>
    /// marker. The store's <c>SchemaVersion</c> replaces it; every other value is kept as it is. Saving
    /// would drop the marker anyway, since <see cref="ViewModels.UserSettings"/> no longer declares it;
    /// removing it here keeps the upgrade explicit.
    /// </summary>
    private static void FromUnversioned(JsonObject root) => root.Remove("Version");
}
