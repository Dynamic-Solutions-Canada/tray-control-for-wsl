# WSL Monitor

A Windows 11 tray app that shows whether your WSL distros are running, and starts or stops them with a click.

## Using it

- **Tray icon:** a green dot means at least one distro is running, amber means one is starting or stopping, and no dot means nothing is running. Hover for a summary.
- **Click (or double-click) the icon:** a list of your distros with a Start or Stop button for each, a power button to shut down WSL (click twice), and a Details button for the full window.
- **Stopping** asks for a second click. Distros that use systemd (Ubuntu's default) shut down cleanly: services stop in order and programs get a chance to exit. If one hasn't stopped after 2 minutes, you can force it.
- **Details window:** an overview with **Shut down WSL** (stops every distro cleanly, then frees WSL's memory), a page per distro with its version, location, disk size and shortcuts to a terminal or its files, plus Settings, About, and our other apps.
- **Right-click the icon:** open the window, settings, logs, or exit.

The app is available in English (US and Canada), French (France and Canada), Spanish, German, Portuguese (Brazil), Japanese and Simplified Chinese, the same languages as Persistent. It follows Windows' language unless you pick one in Settings.

Whether a distro you start keeps running with nothing open in it depends on WSL's idle timeout. The Overview page tells you how yours is set, and how to keep distros running (`instanceIdleTimeout=-1` in `.wslconfig`).

## Build

From WSL in this folder (see the [Windows build tools README](https://github.com/Dynamic-Solutions-Canada/windows-build-tools)):

```bash
git submodule update --init
npm run verify:desktop -- --platform x64     # Debug compile, including XAML
npm run install:desktop -- --platform x64    # signed dev MSIX, update in place, launch
```
