> **Scope:** Building, checking and installing WSL Status, and where it stores data.
> **Governs:** `Directory.Build.props`, `package.json`, `windows-build.json`, `scripts/**`, `WslMonitorMSIX/**`

# WSL Status development

## Prerequisites

- WSL with this repository cloned inside it, including submodules: clone with `--recurse-submodules`, or run `git submodule update --init` (the app builds on `external/winui-app-kit` and `external/promo`).
- [lefthook](https://lefthook.dev) for the git hooks in `lefthook.yml`: run `lefthook install` once after cloning. Its secrets check is the kit's `scripts/devkit precommit`, which needs Node 22.12 or later, below 25, and installs its pinned tools inside the kit on first use.
- Node.js for the root `npm run` commands, and Python 3 for the catalog check.
- The [shared Windows build tools](https://github.com/Dynamic-Solutions-Canada/windows-build-tools), which run the Windows build from WSL and document their own Windows-side requirements, the disposable Windows TEMP copy and the local build VM. The link resolves once that repository is published.

## Build and install

Run these from the repository root in WSL:

| Command | What it does | What it proves |
| --- | --- | --- |
| `npm run verify:desktop` | Debug compile on Windows | The app compiles, XAML included |
| `npm run package:desktop:dev` | Signed dev MSIX | The package builds; not that it installs or runs |
| `npm run install:desktop` | Builds the dev MSIX, updates the installed copy in place and launches it | That it installs and starts; check behaviour in the app |
| `npm run package:desktop` | Unsigned Store MSIX | The Store package builds, with manifest text resolved for every language |

Signed dev builds (`package:desktop:dev`, `install:desktop`) install under the separate dev identity `TechnicallyReal.WslMonitor`, so they never replace a Store install; `npm run install:desktop -- --store-identity` would build a signed bundle with the Store identity to test an update over one (Windows rejects a plain MSIX over an installed bundle), but the kit refuses it unless the signing certificate's subject is the Store publisher, `CN=B55B96A5-F10C-49D3-8D0B-7260EDBE7625`, and the shared dev certificate's is not. `npm run package:desktop` keeps the Store identity, `DynamicSolutionsCA.WSLMonitor`, for upload. Never uninstall to get past an update error.

Packaging runs winui-app-kit's `build/build-msix.ps1`, configured by `windows-build.json` (the manifest folder, the dev and Store identities, and `manifest_strings`, which writes the manifest's translated text from the catalogs). `<Version>` in `Directory.Build.props` is the only version source. Dev builds raise the revision only in the staged copy, so the tracked version changes only for a release.

## Checks

`npm run validate` is the Linux gate. It runs, in order:

- `npm run format:check`: every C# project outside `external/` matches the root `.editorconfig`, which is winui-app-kit's file copied unchanged from the tag the app pins; never hand-edit it. `npm run format` rewrites the files.
- `npm run check:gitignore`: `.gitignore` ends with winui-app-kit's shared block, unchanged.
- `npm run check:l10n`: winui-app-kit's catalog check, set up by the `localization` block in `windows-build.json`. Every catalog has the same keys and `{{placeholders}}`, every key is used and every used key exists, no text contains an em dash, the app defines no `kit.` key, and the manifest languages match the family's list.
- `npm run check:listing`: the Store listing text and the current version's release notes fit the Store's limits and match the README tagline.
- `npm run check:notices`: `WslMonitor/THIRD-PARTY-NOTICES.txt` matches the packages the project restores, and `AboutContent.NoticesPath` (the About page's link) names that file. After adding, removing or updating a NuGet package, run `npm run generate:notices` and commit the file. The script lists every package except build tooling, with its full licence text, and fails on a licence it has no text for.
- `npm run check:screenshots`: the Store screenshots in `store-assets/microsoft/screenshots/` are winui-app-kit's renders of the raw test-VM captures in `store-assets/raw/microsoft/` (dark, 100% scale, window effects, the dark wallpaper, each with its `.json` sidecar), and their captions match `listings.json`. `npm run shots:render` re-crops them after a recapture; see `store-assets/microsoft/listings.md`.
- `npm test`: every portable harness under `tests/`, found by winui-app-kit's `scripts/run-tests` without a hand-kept list. Each has a README saying what it covers and does not prove: the distro state rules, and the settings file (the kit's real store and this app's schema steps, against real files).

Formatting and tests run through winui-app-kit's `scripts/dotnet`, which uses the .NET SDK pinned in `global.json` when it is installed and otherwise its Docker image, capped at 3 GB (`KIT_DOTNET_MEMORY`). Run them as a non-root user. Run `npm run validate` and `npm run verify:desktop` before finishing a change.

### CI

`ci.yml` runs `npm run validate` on Linux for every push to `main` and every pull request. It also compiles the app on Windows for x64 and ARM64 (Debug, the same compile as `verify:desktop`) when app source, packaging, submodules or build configuration changed. `codeql.yml` scans the C# code and the workflows on each push, each pull request and weekly, leaving out `external/`. Dependabot proposes grouped NuGet and GitHub Actions updates weekly; the Windows App SDK and NLog move with winui-app-kit instead.

### Manual checks in the installed app

The harness cannot see the screen, WSL or Windows. Before a release, install the build (`npm run install:desktop`) and check:

- The tray icon: no dot with nothing running, amber while a distro starts or stops, green once one runs; the tooltip summary.
- Click and double-click both open the flyout once; it lists the distros with Start or Stop, and Details opens the window.
- Start a stopped distro; it shows Starting, then Running.
- Stop asks for a second click (red "Stop?" for 4 seconds) and a double-click does not stop it. A systemd distro stops cleanly; Force stop appears only after the clean stop fails or times out.
- Shut down WSL from the flyout (two clicks) and from the Overview page (dialog); a distro that will not stop cleanly makes it ask before forcing.
- The Overview page's idle timeout explanation matches `%UserProfile%\.wslconfig`.
- Settings: start at sign-in, Docker Desktop's distros, theme, and a language change that relabels the flyout, the window and the tray tooltip without reopening them. Reset settings asks first, then puts theme, language and Docker distros back to their defaults without changing Start with Windows.
- Settings from an earlier version survive the update, and a second launch opens the window of the copy already running instead of starting another.
- Exit from the tray menu removes the tray icon and ends the app at once, without a pause, and the log for that run ends with "Exit requested" and has no "Orderly shutdown took longer" warning.
- About: the identity card with the icon and version, Check for updates (opens the Store's updates page when installed from a package), each link (the privacy policy page must be live before the first release), and "Open logs folder" opening the folder with the current log.
- A settings file that can't be read (replace `settings.json` with text that isn't JSON, then start the app): the window opens on Settings with the "settings were reset" notice naming the `.bad` backup, also when started at sign-in.
- Light, dark and high contrast.

## Releases

Releases follow the `release` skill ([.agents/skills/release/SKILL.md](../.agents/skills/release/SKILL.md)). In short:

1. Write the notes for the new version as user-facing bullets in `release-notes/<version>.md`, plus one translated file per other wording catalog, and get them approved before anything else changes.
2. Set `<Version>` in `Directory.Build.props`, run `npm run validate` and `npm run verify:desktop`, and commit the version and notes together as `Bump version to v<version>`.
3. Tag `v<version>` (annotated) and push the branch and tag together.
4. The "Build and Package" workflow (`build-msix.yml`) runs the gate, builds x64 and ARM64 packages as a check, and creates the GitHub Release from the notes. The Release carries no package: WSL Status is a paid Microsoft Store app, so the Store is the only download.
5. The same tag runs "Publish to Microsoft Store" (`store-publish.yml`): the gate, then winui-app-kit's reusable Store workflow builds both architectures, uploads them to Partner Center with each locale's "What's new" from the notes files, and stops at a draft (REL-7). Its summary names the draft ID.
6. Submitting the draft for certification is a separate, deliberate step: run "Publish to Microsoft Store" manually on the same tag with `submit_draft` set to that ID and `no_commit` unchecked. A manual run with `dry_run` checks everything without any Store call.

The app is reserved as `DynamicSolutionsCA.WSLMonitor` (Store ID 9MTQWTX87B8J), paid at Tier1012, and not yet published. `credential-expiry.yml` fails weekly from 60 days before the Store secret expires (the organisation variable `STORE_SECRET_EXPIRES`). Both Store workflows call the kit's reusable ones; their `uses:` tag must equal the `external/winui-app-kit` pin.

The Store listing text is in `store-assets/microsoft/listings.json`, with its limits, the claims it must keep true and the console-only fields in [listings.md](../store-assets/microsoft/listings.md). `npm run check:listing` checks it and the current version's notes.

## Where the app stores data

WSL Status collects no telemetry or crash reports. It reads WSL's distro list from the registry (`HKCU\Software\Microsoft\Windows\CurrentVersion\Lxss`) and `%UserProfile%\.wslconfig`, and never edits either file or key directly. Starting and stopping distros goes through `wsl.exe`.

The app makes two network requests, both only when the user acts, and sends nothing about the user or their distros:

- **"Our other apps":** when the page opens, it downloads the app catalogue (`apps.json`) and its icons from the apps-promo repository on GitHub (`raw.githubusercontent.com`), so GitHub sees the request's IP address.
- **Check for updates (About page):** a packaged install opens the Microsoft Store's updates page and makes no request itself. A copy run without a package asks this repo's GitHub Releases (`api.github.com`) for the latest version.

The About page's links (source, privacy policy, support, licence, third-party notices) open in the browser only when clicked.

One action changes a system setting outside the app: "Make default" on a distro's page runs `wsl --set-default`, which changes WSL's default distro for every program, not just this app. It stays changed after uninstall.

It writes:

| What | Where | After uninstall |
| --- | --- | --- |
| Settings (`settings.json`) | `%AppData%\WslMonitor\` | Removed, see below |
| Settings backups: `settings.json.v<version>.bak` before an upgrade, `.bad` for an unreadable file, `.<time>.reset` after Reset settings | `%AppData%\WslMonitor\` | Removed, see below |
| Logs (`logs.<start time>.txt`, the last few kept) | `%AppData%\WslMonitor\` | Removed, see below |
| Start at sign-in | The package's startup task | Removed with the package |
| "Our other apps" cache (`apps.json` and icons) | The package's `LocalState\PromotedApps\` | Removed with the package |

Every install, including dev installs from `install:desktop`, is an MSIX package, so Windows redirects `%AppData%\WslMonitor\` into the package's own data folder and deletes it on uninstall. The About page's "Open logs folder" opens the real location. A copy run without a package (straight from a build output folder) writes to the real `%AppData%\WslMonitor\`, keeps the promo cache in `%LocalAppData%\TechnicallyReal\PromotedApps\` and, for start at sign-in, sets a `HKCU\...\CurrentVersion\Run` value; all three remain until removed by hand. A packaged copy removes such Run values when it starts.

Windows redirects only folders the packaged app creates itself: if `%AppData%\WslMonitor\` already existed when the package was first installed (from a copy run without a package), the packaged app keeps using the real folder, and it remains after uninstall. Hub Display Switch saw this on 2026-11-16; it applies to this app the same way.

This list has not yet been confirmed against a real uninstall.

There is nothing to back up or export beyond `settings.json`; the promo cache is downloaded again when needed. Reset settings (Settings page) starts again from the defaults and keeps the old file as a `.reset` backup. To delete all data, uninstall the app.
