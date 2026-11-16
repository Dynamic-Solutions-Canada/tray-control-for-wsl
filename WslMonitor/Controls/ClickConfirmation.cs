using Microsoft.UI.Dispatching;

namespace WslMonitor.Controls;

/// <summary>
/// Two-click confirmation for destructive buttons (Stop, Force stop, Shut down WSL): the first click
/// arms it for a few seconds (the button turns red and asks), the second click confirms.
/// <para>
/// State is kept per key, not per button, because the lists holding these buttons are rebuilt every
/// time WSL's state is polled; a rebuilt button picks up the pending confirmation by its key.
/// </para>
/// </summary>
public static class ClickConfirmation
{
    private static readonly TimeSpan ConfirmWindow = TimeSpan.FromSeconds(4);

    /// <summary>Key → when its armed confirmation lapses (UTC).</summary>
    private static readonly Dictionary<string, DateTime> ArmedUntil = new();

    /// <summary>
    /// A confirming click must come at least this long after the arming one: a double-click delivers
    /// two clicks within the system double-click time and must not arm and confirm in one go.
    /// </summary>
    private static TimeSpan MinConfirmDelay =>
        TimeSpan.FromMilliseconds(Math.Max(400, WinUIAppKit.Interop.NativeMethods.GetDoubleClickTime()));

    /// <summary>True while <paramref name="key"/>'s confirmation is showing.</summary>
    public static bool IsArmed(string key) =>
        ArmedUntil.TryGetValue(key, out var until) && until > DateTime.UtcNow;

    /// <summary>Show the confirmation for <paramref name="key"/>.</summary>
    public static void Arm(string key) => ArmedUntil[key] = DateTime.UtcNow + ConfirmWindow;

    /// <summary>
    /// Handle the click that arrives while armed. Returns true (and disarms) when it counts as a
    /// deliberate confirmation; false for the second half of a double-click, which is ignored.
    /// </summary>
    public static bool TryConfirm(string key)
    {
        if (!IsArmed(key)) return false;

        var armedAt = ArmedUntil[key] - ConfirmWindow;
        if (DateTime.UtcNow - armedAt < MinConfirmDelay) return false;

        ArmedUntil.Remove(key);
        return true;
    }

    /// <summary>Call <paramref name="render"/> once <paramref name="key"/>'s confirmation lapses, so the button reverts.</summary>
    public static void OnLapse(string key, DispatcherQueue dispatcher, Action render)
    {
        if (!ArmedUntil.TryGetValue(key, out var until)) return;

        var remaining = until - DateTime.UtcNow;
        var timer = dispatcher.CreateTimer();
        timer.Interval = remaining > TimeSpan.Zero ? remaining : TimeSpan.FromMilliseconds(1);
        timer.IsRepeating = false;
        timer.Tick += (_, _) => render();
        timer.Start();
    }
}
