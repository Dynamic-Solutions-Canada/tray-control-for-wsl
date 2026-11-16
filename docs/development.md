> **Scope:** Building, checking and installing WSL Monitor, and where it stores data.
> **Governs:** `Directory.Build.props`, `package.json`, `windows-build.json`, `scripts/**`, `WslMonitorMSIX/**`

# WSL Monitor development

## Prerequisites

- WSL with this repository cloned inside it, including submodules: clone with `--recurse-submodules`, or run `git submodule update --init` (the app builds on `external/winui-app-kit` and `external/promo`).
- [lefthook](https://lefthook.dev) for the git hooks in `lefthook.yml`: run `lefthook install` once after cloning.
- Node.js for the root `npm run` commands, and Python 3 for the catalog check.
- The [shared Windows build tools](https://github.com/Dynamic-Solutions-Canada/windows-build-tools), which run the Windows build from WSL and document their own Windows-side requirements, the disposable Windows TEMP copy and the local build VM. The link resolves once that repository is published.

## Build and install

Run these from the repository root in WSL:

| Command | What it does | What it proves |
| --- | --- | --- |
| `npm run verify:desktop` | Debug compile on Windows | The app compiles, XAML included |
| `npm run build:desktop` | Signed dev MSIX | The package builds; not that it installs or runs |
| `npm run install:desktop` | Builds the dev MSIX, updates the installed copy in place and launches it | That it installs and starts; check behaviour in the app |
| `npm run package:desktop` | Unsigned Store MSIX | The Store package builds, with manifest text resolved for every language |

Never uninstall to get past an update error.

`<Version>` in `Directory.Build.props` is the only version source. Dev builds raise the revision only in the staged copy, so the tracked version changes only for a release.

## Checks

`npm run check:strings` checks the interface text: every catalog has the same keys and `{{placeholders}}`, no text contains an em dash, and the languages declared in the package manifest match the catalogs. Run it with `npm run verify:desktop` before finishing a change. There are no automated tests yet.

## Releases

There is no release process yet. The app is not yet in the Microsoft Store, and its package identity is a placeholder until the name is reserved.

## Where the app stores data

WSL Monitor collects no telemetry or crash reports, and sends nothing anywhere. It only reads WSL's distro list from the registry (`HKCU\Software\Microsoft\Windows\CurrentVersion\Lxss`) and `%UserProfile%\.wslconfig`; it never changes either. Starting and stopping distros goes through `wsl.exe`.

It writes:

| What | Where | After uninstall |
| --- | --- | --- |
| Settings (`settings.json`) | `%AppData%\WslMonitor\` | Removed, see below |
| Logs (`logs.<start time>.txt`, the last few kept) | `%AppData%\WslMonitor\` | Removed, see below |
| Start at sign-in | The package's startup task | Removed with the package |

Every install, including dev installs from `install:desktop`, is an MSIX package, so Windows redirects `%AppData%\WslMonitor\` into the package's own data folder and deletes it on uninstall. The About page's "Open logs folder" opens the real location. A copy run without a package (straight from a build output folder) writes to the real `%AppData%\WslMonitor\` and, for start at sign-in, a `HKCU\...\CurrentVersion\Run` value; both remain until removed by hand. A packaged copy removes such Run values when it starts.

This list has not yet been confirmed against a real uninstall.

There is nothing to back up or export beyond `settings.json`. To delete all data, uninstall the app.
