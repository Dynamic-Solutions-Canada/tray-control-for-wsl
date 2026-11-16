using WinUIAppKit.Localization;

namespace WslMonitor.Services;

/// <summary>Outcome of one <c>wsl.exe</c> run.</summary>
/// <param name="Launched">False when wsl.exe couldn't be started at all (WSL not installed).</param>
/// <param name="TimedOut">True when the run was killed for exceeding its time limit.</param>
public sealed record WslResult(bool Launched, bool TimedOut, int ExitCode, string Output, string Error)
{
    /// <summary>
    /// <see cref="Message"/>, so a result can be a <see cref="WinUIAppKit.Localization.LocalizedMessage"/>
    /// value and the app's own wording in it (timeout, not installed) follows a language change.
    /// </summary>
    public override string ToString() => Message;

    public bool Succeeded => Launched && !TimedOut && ExitCode == 0;

    /// <summary>
    /// The most useful single line for a user-facing error: WSL's own first line (already in Windows'
    /// language), or the app's explanation when WSL said nothing.
    /// </summary>
    public string Message
    {
        get
        {
            if (!Launched)
            {
                return UiText.Get("error.wsl-missing");
            }

            if (TimedOut)
            {
                return UiText.Get("error.wsl-timeout");
            }

            string text = Error.Trim().Length > 0 ? Error : Output;
            return text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                       .FirstOrDefault() ?? UiText.Format("error.exit-code", ("code", ExitCode));
        }
    }
}
