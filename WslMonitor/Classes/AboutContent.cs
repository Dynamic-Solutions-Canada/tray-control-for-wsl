using WinUIAppKit.Pages;
using WinUIAppKit.Updates;

namespace WslMonitor.Classes;

/// <summary>
/// What the kit's shared About page shows for Tray Control for WSL (UI-5, BRAND-8), and the one place the
/// repository and the privacy policy are named (BRAND-5): every link is built from these values.
/// </summary>
internal static class AboutContent
{
    /// <summary>The repository's current owner (BRAND-10).</summary>
    public const string RepoOwner = "Dynamic-Solutions-Canada";

    /// <summary>The repository name.</summary>
    public const string RepoName = "tray-control-for-wsl";

    /// <summary>
    /// Where the generated notices live in the repo, relative to its root. `npm run check:notices`
    /// fails if this stops matching the notices output (`notices.about_source` in windows-build.json).
    /// </summary>
    public const string NoticesPath = "WslMonitor/THIRD-PARTY-NOTICES.txt";

    /// <summary>The product's registered privacy policy (DOCS-8). Permanent once the app ships (BRAND-11).</summary>
    public static readonly Uri PrivacyPolicy = new("https://www.dynamic-solutions.ca/privacy/tray-control-for-wsl");

    /// <summary>
    /// "Check for updates" (DIST-6): the Store's updates page when packaged, this repo's GitHub Releases
    /// otherwise. It only checks when the user asks.
    /// </summary>
    public static UpdateService Updates { get; } = new(RepoOwner, RepoName);

    /// <summary>Build the page's content; called each time the About page opens.</summary>
    public static AboutInfo Create()
    {
        var (source, support, license) = AboutInfo.GitHubLinks(RepoOwner, RepoName);
        return new AboutInfo
        {
            ProductName = "Tray Control for WSL",
            DescriptionKey = "AppDescription",
            IconPngPath = App.IconPngPath,
            PrivacyPolicy = PrivacyPolicy,
            Updates = Updates,
            SourceCode = source,
            Support = support,
            License = license,

            // The generated notices ship in the package (HYG-3); the link opens the same file in the
            // repo, since a packaged app cannot hand a file inside its install folder to the browser.
            ThirdPartyNotices = new Uri(source + "/blob/main/" + NoticesPath),
        };
    }
}
