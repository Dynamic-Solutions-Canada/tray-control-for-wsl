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
| Full window (Overview, per-distro pages, Settings, About) | `DetailsWindow.xaml(.cs)`, `Pages/*` |
| Shared distro UI (row, Start/Stop button, labels) | `Controls/DistroRow.cs`, `Controls/DistroPowerButton.cs`, `Controls/DistroVisuals.cs` |
| Polling, state machine, actions | `Services/WslMonitorService.cs` |
| `wsl.exe` calls | `Services/WslCli.cs` |
| Distro list and details (HKCU `...\Lxss`) | `Services/WslRegistry.cs` |
| `.wslconfig` idle timeouts | `Services/WslConfigFile.cs` |
| Settings | `Models/UserSettings.cs`, `Classes/SettingsManager.cs` |

## Conventions / gotchas

- `wsl.exe` writes UTF-16 for `--list` unless `WSL_UTF8=1` (set by `WslCli`). Parse `--quiet` output, never the localized `-v` table.
- The service mutates `Distro` objects on the UI thread only and raises `Changed` only when visible state changes; views rebuild on `Changed`. Pending stop confirmations live in `DistroPowerButton` (per distro), so rebuilds don't lose them.
- Never run `wsl --terminate` or `--shutdown` while testing from inside WSL: it ends the session doing the testing.
- Namespace `WslMonitor.Windows` shadows WinRT `Windows.*`: always write `global::Windows...`.
- Package identity is a **placeholder** (`TechnicallyReal.WslMonitor`) until reserved in Partner Center; update `Package.appxmanifest` and `windows-build.json` together.
- **PowerShell build scripts must be ASCII.** App icon and MSIX images come from `WslMonitorMSIX/generate-msix-images.ps1` (committed output).

## Building

Source is authoritative in WSL. Read `.codex/docs/windows-development.md` and use the root npm commands (`npm run verify:desktop`, `build:desktop`, `install:desktop`, `package:desktop`). This PC is x64: pass `-- --platform x64`. Clone with `--recurse-submodules` (or run `git submodule update --init`) so `external/winui-app-kit` is present.
