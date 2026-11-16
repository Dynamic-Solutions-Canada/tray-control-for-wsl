# WSL Monitor: Agent Instructions

WSL Monitor is a resident WinUI 3 tray app for Windows users of WSL: it shows which WSL distros are running and starts or stops them. It ships as an MSIX (`WslMonitorMSIX/`) built from WSL through the shared Windows build bridge. The tray, flyout, settings-file, startup and theme plumbing come from the shared WinUI App Kit submodule at `external/winui-app-kit`, and the "Our other apps" page from the apps-promo submodule at `external/promo`.

## Standards

This repo follows the shared standards at https://github.com/Dynamic-Solutions-Canada/app-standards. Read the index for this stack, https://github.com/Dynamic-Solutions-Canada/app-standards/blob/main/stacks/windows.md, then the topic file for the area you are changing. If `external/standards/` exists, it is the same content pinned for offline use. Where this file differs from the standards, this file wins and says why.

## Architecture

| Area | Files |
|---|---|
| Host (hidden, resident, owns tray + service) | `MainWindow.xaml(.cs)` |
| Tray flyout | `Windows/DistroFlyout.xaml(.cs)` |
| Tray icon artwork (idle / running / changing) | `Classes/TrayIconRenderer.cs` |
| Full window (Overview, per-distro pages, Settings, About) | `DetailsWindow.xaml(.cs)`, `Pages/*`; pages that follow live state derive from `Pages/LivePage.cs` |
| Distro model and state | `Models/Distro.cs` |
| Shared distro UI (list and empty states, row, Start/Stop button, labels, status line, Stop policy) | `Controls/DistroList.cs`, `Controls/DistroRow.cs`, `Controls/DistroPowerButton.cs`, `Controls/DistroVisuals.cs` |
| Two-click confirmation (Stop, Force stop, Shut down WSL) | `Controls/ClickConfirmation.cs`; flyout shutdown button `Controls/ShutdownButton.cs` |
| Shared page and button styles | `App.xaml` (`PageContentStyle`, `PageTitleStyle`, `SectionHeaderStyle`, `IconLabelStyle`, `CardBorderStyle`) |
| Polling, state machine, actions | `Services/WslMonitorService.cs` |
| `wsl.exe` calls | `Services/WslCli.cs` |
| Distro list and details (HKCU `...\Lxss`) | `Services/WslRegistry.cs` |
| `.wslconfig` idle timeouts | `Services/WslConfigFile.cs` |
| Settings | `ViewModels/UserSettings.cs`, `Classes/SettingsManager.cs` |
| Interface text (9 languages) | `Localization/Catalogs/*.json`, `Classes/AppLanguages.cs`; lookups via the kit's `UiText` / `Localize` |
| "Our other apps" page | `external/promo` submodule, imported in the csproj |

## Behaviour

- **Tray click or double-click** opens the flyout: one row per distro with Start / Stop, and a Details button. Double-click deliberately does the same as a click (the kit swallows the trailing click so it doesn't close again).
- **Stop needs two clicks** (red "Stop?" for 4 s). Distros running systemd stop with `systemctl poweroff` (clean); others with `wsl --terminate`. After 2 minutes still running, the distro shows "Force stop" (also two clicks).
- **Shut down WSL** (Overview page with a confirmation dialog, or the flyout's power button with two clicks like Stop): clean-stops every running distro in parallel, asks before forcing any that miss the limit (in the details window, opened if needed), then `wsl --shutdown`.
- **Start** runs a no-op in the distro. Whether it stays up depends on `instanceIdleTimeout` in `%UserProfile%\.wslconfig`; the Overview page explains the current setting.
- Docker Desktop's `docker-desktop*` distros are hidden unless enabled in Settings.

## Conventions and gotchas

- `wsl.exe` writes UTF-16 for `--list` unless `WSL_UTF8=1` (set by `WslCli`). Parse `--quiet` output, never the localized `-v` table.
- The service mutates `Distro` objects on the UI thread only and raises `Changed` only when visible state changes; views rebuild on `Changed`. Pending stop confirmations live in `DistroPowerButton` (per distro), so rebuilds don't lose them.
- Never run `wsl --terminate` or `--shutdown` while testing from inside WSL: it ends the session doing the testing.
- Namespace `WslMonitor.Windows` shadows WinRT `Windows.*`: always write `global::Windows...`.
- Package identity is a **placeholder** (`TechnicallyReal.WslMonitor`) until reserved in Partner Center; update `Package.appxmanifest` and `windows-build.json` together.
- **Every user-visible string goes through the catalogs.** Add the key to all seven `Localization/Catalogs/*.json` (en-CA and fr-CA reuse en-US and fr-FR), then run `npm run check:l10n` (keys, `{{placeholders}}`, no em dashes, manifest languages). XAML uses `l10n:Localize.Text="key"` (also `Content`, `Header`, `ToolTip`, `OnContent`/`OffContent`); code uses `UiText.Get` / `UiText.Format`. Store keys, not finished text, on models and services (`Distro.BusyReason`, `LocalizedMessage` for `Distro.LastError` and `ShutdownStatus`; pass a `WslResult` or `NameList` as a value, not its text) so a language change relabels them. Names, paths and WSL's own output are never translated. The "Our other apps" page translates its own text through `PromoLocalization.LocaleProvider`, set in `MainWindow`; the catalogs hold only its navigation label (`promo.title`), and the page itself is never patched. Translations are unreviewed machine translations.
- **Language changes apply live:** `Localize` relabels XAML; anything built in code must also refresh on `UiText.Changed` (live pages redraw in `LivePage.OnAttach` handlers, the details window rebuilds its menu, the flyout rebuilds, the tray tooltip redraws). Pages are never re-navigated for a language change.
- **Actions and polling:** every state change made by an action calls `Stamp(d)`; a poll that started before the latest stamp leaves that distro's state alone. Actions only write their outcome if the distro is still in the state they set. "Shut down WSL" progress lives in the service (`ShutdownStatus`) and its questions go through `DetailsWindow.ConfirmAsync`, never a specific page.
- **Manifest strings** use `ms-resource:Resources/<Key>`; `build-msix.ps1` writes them from the catalogs into `resources.pri` and fails the build unless every declared language resolves. Keep resource splitting off (single package, not a bundle).
- **PowerShell build scripts must be ASCII.** App icon and MSIX images come from `WslMonitorMSIX/generate-msix-images.ps1` (committed output).

## Commands

Source is authoritative in WSL, with submodules initialised (`git submodule update --init`). Setup is in `docs/development.md`; the Windows commands go through `scripts/windows-build` (see the build tools README in the guidance map).

| Command | Proves | Does not prove |
| --- | --- | --- |
| `npm run verify:desktop` | The app compiles on Windows (Debug), XAML included | That it packages or runs |
| `npm run package:desktop:dev` | A signed dev package builds | That it installs or runs |
| `npm run install:desktop` | The dev package updates in place and launches | Behaviour; check it in the running app |
| `npm run package:desktop` | An unsigned Store package builds, manifest strings resolved for every language | Signing; that it runs |
| `npm run check:l10n` | Every catalog has the same keys and placeholders, no em dashes, and the manifest languages match | That the text is translated well |
| `npm test` | Every portable harness under `tests/` passes on Linux (host .NET SDK or Docker): today the distro state rules | Anything that needs WSL, Windows or the installed app; each harness README says what it leaves out |
| `npm run check:listing` | Listing text and the current version's notes fit the Store's limits; the README tagline matches | That the listing is live or accurate |
| `npm run validate` | The whole Linux gate passes: `format:check`, `check:l10n`, `check:listing`, then `npm test` | A Windows compile; also run `verify:desktop` |
| `npm run format` | Rewrites every C# project (not `external/`) to the root `.editorconfig` (the kit's, unchanged) | Behaviour; review the diff |

Before finishing a change, run `npm run validate` and `npm run verify:desktop`. Use the `desktop` skill for build and install requests.

## Release

Releases are tag-driven: a `v*` tag makes `build-msix.yml` run the gate, build both architectures as a check, and create a notes-only GitHub Release (WSL Monitor is a paid Store app, so no package is attached). Store submission is manual for now. Use the `release` skill.

## Guidance map

| Guide, doc, or skill | Read or use when | Governs |
| --- | --- | --- |
| [docs/development.md](docs/development.md) | Building, checking, versioning, or changing where the app writes data | Build setup, data locations |
| [.agents/guides/ui-conventions.md](.agents/guides/ui-conventions.md) | Any window, page, flyout, row or card | `WslMonitor/**/*.xaml(.cs)`, `WslMonitor/Controls/**` |
| [WinUI App Kit `AGENTS.md`](external/winui-app-kit/AGENTS.md) | Using the kit's tray, flyout, settings, startup, theme or localization APIs | `external/winui-app-kit/**` (read only) |
| [Windows build tools README](https://github.com/Dynamic-Solutions-Canada/windows-build-tools) | A Windows build, install or packaging problem | `scripts/windows-build`, `windows-build.json` |
| [store-assets/microsoft/listings.md](store-assets/microsoft/listings.md) | Changing Store listing text or anything a listing claim depends on | `store-assets/microsoft/**` |
| `desktop` skill | Building, verifying or installing locally | Local Windows builds |
| `release` skill | Cutting a release | `Directory.Build.props`, `release-notes/**`, tags |

## Keeping docs current

| What changed | Update |
| --- | --- |
| A file, service or control added, moved or removed | The Architecture table above |
| A UI pattern, shared style or control | `.agents/guides/ui-conventions.md` |
| An npm command | The Commands table above, `docs/development.md`, the `desktop` skill, `paseo.json` |
| Where the app writes data, or a new kind of data | The data table in `docs/development.md` |
| A state rule in `WslMonitorService` (actions, polling, Shut down WSL) | Behaviour above, the scenarios and README in `tests/WslMonitor.State.Tests/` |
| A workflow, or what CI runs | The CI section of `docs/development.md` |
| A test harness added or changed | Its README; `npm test` finds it by itself |
| winui-app-kit pin bumped | Copy the kit's `.editorconfig` at that tag unchanged, then `npm run format` |
| A language or catalog rule | Conventions above, `scripts/check-localization.py` |
| A feature, requirement, language or privacy behaviour users can see | Behaviour above, `README.md`, `store-assets/microsoft/listings.json` and the claims in `listings.md` |
| Release workflow or packaging | The `release` skill and the Releases section of `docs/development.md` |
| A guide or skill moved or removed | The guidance map above and every skill that names it |
