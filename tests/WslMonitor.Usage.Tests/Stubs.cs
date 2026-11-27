// Stand-ins for the kit's interface text and NLog, which WslConfigFile uses. Namespaces match the real
// types so the linked file compiles unchanged.
#pragma warning disable IDE0130

namespace WinUIAppKit.Localization
{
    /// <summary>Returns keys instead of text; the harness asserts on values, never on wording.</summary>
    public static class UiText
    {
        public static string Get(string key) => key;

        public static string Format(string key, params (string Name, object? Value)[] values) => key;
    }
}

namespace NLog
{
    /// <summary>Discards log lines.</summary>
    public sealed class Logger
    {
        public void Warn(Exception ex, string message, params object?[] args) { }
    }

    public static class LogManager
    {
        public static Logger GetCurrentClassLogger() => new();
    }
}

#pragma warning restore IDE0130
