using WinUIAppKit.Localization;

namespace WslMonitor.Models;

/// <summary>
/// Distro names for a <see cref="LocalizedMessage"/> placeholder. Joined with the current language's
/// list separator when the message is shown ("Ubuntu, Debian", "Ubuntu、Debian").
/// </summary>
public sealed class NameList(IReadOnlyList<string> names)
{
    /// <summary>The names in order.</summary>
    public IReadOnlyList<string> Names { get; } = names;

    /// <inheritdoc />
    public override string ToString() => string.Join(UiText.Get("list.separator"), Names);
}
