# Distro state rules harness

Checks the action and polling rules in `WslMonitorService` (see "Behaviour" and "Actions and polling" in [AGENTS.md](../../AGENTS.md)). It links the production service, `Distro`, `NameList`, `WslResult`, `RegisteredDistro` and the kit's `LocalizedMessage`, and replaces `wsl.exe`, the registry, settings, interface text and NLog with the stand-ins in `Fakes.cs`. Each scenario runs on one thread, as the app runs the service on its UI thread, and fails after 10 seconds instead of hanging.

Covers: polling (running and stopped, Changed only on a visible change, Docker Desktop's distros hidden, registry busy states, WSL not answering, the WSL version from the registration's flags), Start (Starting held until WSL lists it, failure), the two race rules (a poll that began before an action leaves that distro alone; an action writes its outcome only if the distro is still in the state it set), Stop (systemd poweroff, terminate otherwise, never a terminate when the init check fails, a failed terminate), Force stop, and Shut down WSL (clean, and asking before forcing, declined or confirmed).

Does not prove:

- The 2 minute clean-stop limit and 30 second force-stop limit expiring: they wait on the real clock, so every stand-in reports a stopped distro at once. The init-check failure reaches the same "Force stop" state without waiting.
- That `wsl.exe` and the registry behave as the stand-ins do, the polling intervals, or anything on screen (two-click confirmation, the flyout, the details window). Those need the installed app; see the manual checks in [docs/development.md](../../docs/development.md).

Run with `npm test`, or alone with `external/winui-app-kit/scripts/dotnet run --project tests/WslMonitor.State.Tests`.
