# VM usage harness

Checks the figures behind the flyout's CPU and memory cards. It links the production `WslVmUsage.cs` (the CPU share between readings, the sparkline's history, memory, and the VM's limits) and `WslConfigFile.cs` (reading `memory` and `processors` from `.wslconfig`); interface text and NLog are the stand-ins in `Stubs.cs`.

Covers: CPU use as a share of the processors the VM may use, clamped to 0 to 100%, and no figure until a second reading; the one-minute history, newest last; a restarted or stopped VM starting again from nothing; the default limits (half the PC's memory, every processor) and `.wslconfig` limits capped at what the PC has; `.wslconfig` sizes (`8GB`, `512MB`, `8G`, `1.5GB`, plain bytes, any case) and unreadable values read as unset; and `memory` and `processors` read from `[wsl2]` only, with the idle timeouts still read.

Does not prove:

- That Windows' process snapshot reports the VM (`vmmemWSL`) as `WslVmProcess` reads it, or that sampling runs only while the flyout is open. Those need Windows and the installed app; see the manual checks in [docs/development.md](../../docs/development.md).
- Anything on screen (the cards, sparkline and meter).

Run with `npm test`, or alone with `external/winui-app-kit/scripts/dotnet run --project tests/WslMonitor.Usage.Tests`.
