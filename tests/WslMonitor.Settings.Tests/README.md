# Settings file harness

Checks how the settings file loads, upgrades and resets (CONF-3, CONF-4). It links winui-app-kit's real `JsonSettingsStore` and `AtomicFile`, and the app's real schema steps (`SettingsMigrations.cs`) and settings class (`UserSettings.cs`), and runs them against real files in a temporary folder. The kit's theme and window types are the stand-ins in `Stubs.cs`.

Covers: a first run (file written with the current schema version); a file written by 1.0.0 (every value kept, the old `Version` marker dropped, the original kept as `settings.json.v0.bak`, and not reported as a first run, so the app doesn't register startup or open its window again); a saved `false` for a setting that defaults to `true`; an unreadable file (kept as `.bad`, defaults written, not a first run); a current file (no backup); a file from a newer version (backed up, loaded as far as this version understands); and Reset (old file kept as `.reset`, defaults written).

Does not prove: what `SettingsManager` and the Settings page do with each outcome (theme and language applied after a reset, controls reloaded), the Reset button and its confirmation, or where a packaged install keeps the file. Those need the installed app; see the manual checks in [docs/development.md](../../docs/development.md).

Run with `npm test`, or alone with `external/winui-app-kit/scripts/dotnet run --project tests/WslMonitor.Settings.Tests`.
