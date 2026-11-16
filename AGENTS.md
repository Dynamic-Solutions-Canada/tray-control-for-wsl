# WSL Monitor: Agent Instructions

## Project overview

A resident WinUI 3 tray app that shows which WSL distros are running and starts or stops them. Ships as an **MSIX** (`WslMonitorMSIX/`), built from WSL through the shared Windows build bridge. Same conventions as Hub Display Switch; the tray, flyout, settings-file, startup and theme plumbing come from the shared **WinUI App Kit** submodule at `external/winui-app-kit` (read its `AGENTS.md` before changing it).

## Behaviour

- **Tray click or double-click** opens the flyout: one row per distro with Start / Stop, and a Details button. Double-click deliberately does the same as a click (the kit swallows the trailing click so it doesn't close again).
- **Stop needs two clicks** (red "Stop?" for 4 s). Distros running systemd stop with `systemctl poweroff` (clean); others with `wsl --terminate`. After 2 minutes still running, the distro shows "Force stop" (also two clicks).
- **Shut down WSL** (Overview page, confirmation dialog): clean-stops every running distro in parallel, asks before forcing any that miss the limit, then `wsl --shutdown`.
- **Start** runs a no-op in the distro. Whether it stays up depends on `instanceIdleTimeout` in `%UserProfile%\.wslconfig`; the Overview page explains the current setting.
- Docker Desktop's `docker-desktop*` distros are hidden unless enabled in Settings.

## Architecture

| Area | Files |
|---|---|
| Host (hidden, resident, owns tray + service) | `MainWindow.xaml(.cs)` |
| Tray flyout | `Windows/DistroFlyout.xaml(.cs)` |
| Tray icon artwork (idle / running / changing) | `Classes/TrayIconRenderer.cs` |
| Full window (Overview, per-distro pages, Settings, About) | `DetailsWindow.xaml(.cs)`, `Pages/*`; pages that follow live state derive from `Pages/LivePage.cs` |
| Distro model and state | `Models/Distro.cs` |
| Shared distro UI (list and empty states, row, Start/Stop button, labels, status line, Stop policy) | `Controls/DistroList.cs`, `Controls/DistroRow.cs`, `Controls/DistroPowerButton.cs`, `Controls/DistroVisuals.cs` |
| Shared page and button styles | `App.xaml` (`PageContentStyle`, `PageTitleStyle`, `SectionHeaderStyle`, `IconLabelStyle`, `CardBorderStyle`) |
| Polling, state machine, actions | `Services/WslMonitorService.cs` |
| `wsl.exe` calls | `Services/WslCli.cs` |
| Distro list and details (HKCU `...\Lxss`) | `Services/WslRegistry.cs` |
| `.wslconfig` idle timeouts | `Services/WslConfigFile.cs` |
| Settings | `Models/UserSettings.cs`, `Classes/SettingsManager.cs` |
| Interface text (9 languages) | `Strings/*.json`, `Classes/AppLanguages.cs`; lookups via the kit's `UiText` / `Localize` |
| "Our other apps" page | `external/promo` submodule (shared with the sibling apps), imported in the csproj |

## Conventions / gotchas

- `wsl.exe` writes UTF-16 for `--list` unless `WSL_UTF8=1` (set by `WslCli`). Parse `--quiet` output, never the localized `-v` table.
- The service mutates `Distro` objects on the UI thread only and raises `Changed` only when visible state changes; views rebuild on `Changed`. Pending stop confirmations live in `DistroPowerButton` (per distro), so rebuilds don't lose them.
- Never run `wsl --terminate` or `--shutdown` while testing from inside WSL: it ends the session doing the testing.
- Namespace `WslMonitor.Windows` shadows WinRT `Windows.*`: always write `global::Windows...`.
- Package identity is a **placeholder** (`TechnicallyReal.WslMonitor`) until reserved in Partner Center; update `Package.appxmanifest` and `windows-build.json` together.
- **Every user-visible string goes through the catalogs.** Add the key to all seven `Strings/*.json` (en-CA and fr-CA reuse en-US and fr-FR), then run `npm run check:strings` (keys, `{{placeholders}}`, no em dashes, manifest languages). XAML uses `l10n:Localize.Text="key"` (also `Content`, `Header`, `ToolTip`, `OnContent`/`OffContent`); code uses `UiText.Get` / `UiText.Format`. Store keys, not finished text, on models and services (`Distro.BusyReason`, `LocalizedMessage` for `Distro.LastError` and `ShutdownStatus`; pass a `WslResult` or `NameList` as a value, not its text) so a language change relabels them. Names, paths and WSL's own output are never translated. Promo copy keys match Persistent's, and so does the wording except `promo.little-launcher`, which follows the newer catalog text (Persistent still has the old one). Translations are unreviewed machine translations.
- **Language changes apply live:** `Localize` relabels XAML; anything built in code must also refresh on `UiText.Changed` (live pages redraw in `LivePage.OnAttach` handlers, the details window rebuilds its menu, the flyout rebuilds, the tray tooltip redraws). Pages are never re-navigated for a language change.
- **Actions and polling:** every state change made by an action calls `Stamp(d)`; a poll that started before the latest stamp leaves that distro's state alone. Actions only write their outcome if the distro is still in the state they set. "Shut down WSL" progress lives in the service (`ShutdownStatus`) and its questions go through `DetailsWindow.ConfirmAsync`, never a specific page.
- **Manifest strings** use `ms-resource:Resources/<Key>`; `build-msix.ps1` writes them from the catalogs into `resources.pri` and fails the build unless every declared language resolves. Keep resource splitting off (single package, not a bundle).
- **PowerShell build scripts must be ASCII.** App icon and MSIX images come from `WslMonitorMSIX/generate-msix-images.ps1` (committed output).

## Building

Source is authoritative in WSL. Read `.codex/docs/windows-development.md` and use the root npm commands (`npm run verify:desktop`, `build:desktop`, `install:desktop`, `package:desktop`). This PC is x64: pass `-- --platform x64`. Clone with `--recurse-submodules` (or run `git submodule update --init`) so `external/winui-app-kit` and `external/promo` are present.
