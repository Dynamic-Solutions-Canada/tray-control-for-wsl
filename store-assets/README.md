# Store assets

| Folder | Holds |
| --- | --- |
| `microsoft/listings.json`, `listings.md` | The Store listing text in every locale, and the claims it makes (see `listings.md`) |
| `manifest.json` | Every Store screenshot in listing order: its raw source, theme, crop, en-US caption and alt text (LIST-8) |
| `capture/shots.ps1` | The capture script, run in the test VM once per listing locale |
| `raw/microsoft/<locale>/` | The raw captures, one folder per listing locale: the test VM's whole desktop, each with the `.json` sidecar the capture helper writes |
| `microsoft/screenshots/<locale>/` | The listing screenshots, rendered from that locale's raw captures, with its generated first screenshot (`00-hero-dark.png`) |
| `microsoft/text-free/` | The hero without its text, for the website (`npm run hero` writes it) |

## Screenshots

They are real captures of the installed app on the Windows 11 test VM with stock Ubuntu and Debian distros (WSL 1, since the test VM can't run WSL 2) installed under `C:\WSL`, so no personal distro names, user names or paths show. Every capture is dark, with window effects on and the dark wallpaper. The manifest names a capture per listing locale (below); only the English ones, at 100% scale, exist so far. A new or changed screenshot is approved by the maintainer before it is committed.

- **Render** with `npm run shots:render` (winui-app-kit's renderer; its README has the rules): each locale's captures in `raw/microsoft/<locale>/` are cropped to 1366 x 768 (a 200% capture in logical pixels, then halved) and written to `microsoft/screenshots/`. Recaptioning needs no recapture; a new look does.
- **Check** with `npm run check:screenshots` (part of `npm run validate`): every listing locale has its captures, each with its sidecar and following the capture rules above, every screenshot is the renderer's output, and the captions match the en-US `screenshotCaptions` in `listings.json`.
- **Sync** with the next Store release: the Store workflow's `screenshots` input uploads every locale's set, its hero first, with that locale's captions from `listings.json`.

A UI change that shows in a screenshot means recapturing that shot.

## Localized captures

Every screenshot is moving to a capture per listing locale (en-US, es-ES, fr-FR, de-DE, zh-CN, pt-BR and ja-JP; en-CA and fr-CA reuse en-US's and fr-FR's through `copyFrom`), so Windows and the app show the listing's language, at 200% display scale (3840 x 2160 for the 1920 x 1080 desktop). The kit's renderer crops a 200% capture in logical pixels and halves it to 1366 x 768. Status: run on the VM at 200% in en-US and ja-JP (November 26, 2026); `npm run validate` fails until every locale's captures are committed (the Store workflow runs it before publishing); the en-US and one other locale go to the maintainer for approval, the rest must pass the checks.

- **Sample data:** the distros are stock Ubuntu and Debian, whose names, versions (Ubuntu 26.04, Debian 13) and location (`C:\WSL\Ubuntu`) are product names and paths, the same in every language, so there is no per-locale fixture. The installed WSL's version details on the Overview page come from WSL itself.
- **What the app does not translate:** nothing in these screens; every catalog string has its translation in all six languages (a few, such as Details, Terminal and GB, are the same word in some languages).
- **The app's language** is set by the script in `settings.json` (`Language`), so it never depends on Windows' language list.
- **No translated text in the script:** `capture/shots.ps1` finds everything by automation ID (`FlyoutHeader`, `DistroPower_<distro>`, `DetailsButton`, `DistroNavItem_<distro>`) or the tray tooltip's app name, which is not translated.

For each locale, one locale per VM session (a display language change needs a new sign-in, so the host loops over locales with a cold VM cycle between them; the script captures one locale per run). Run the host commands from the repo root with `source /development/windows-build-tools/vm/app-test/app-test.sh` and `app_test_use win11`:

1. **Language:** on the base disk, install the language pack once (`Install-Language <locale> -ExcludeFeatures`, about 26 minutes and 3.4 GB at peak, then `guest-slim.ps1 -Step Clean`), switch Windows to the locale with `app_test_language <locale>` (display language, input, home location and format; `en-US` restores), then cold-cycle the VM (`vm/remote.sh down`, then the `-snapshot` start: about 80 seconds to a desktop in that language).
2. **Desktop:** in the throwaway session (an uncommitted compose override adds `-snapshot`, which must show in QEMU's command line before Windows boots, and a 3840 x 2160 display), install WSL 1 with the two distros under `C:\WSL` and the Release dev package, run `screenshot-desktop.ps1 -Mode Prepare`, then set the display to 200% through the display API (DisplaySwitchr's `store-assets/capture/set-display-scale.ps1` does this in any language).
3. **Capture:** `app_test_put store-assets/capture/shots.ps1`, then, since `app_test_ui` passes no arguments, `app_test_run '& C:\apptest\shots.ps1 -Locale <locale>'`. Copy each `.png` and `.json` back with `app_test_get out/<locale>/<name>.png` into `raw/microsoft/<locale>/` under the manifest's names. Afterwards run `app_test_clean`, stop the VM and recreate it from the base compose.
4. **Render and check** with `npm run shots:render`, `npm run hero` and `npm run validate`: the check fails for any listing locale without its capture, so a locale is done only when all of its captures are committed.

## The first screenshot: a generated hero

Store search shows only the first screenshot, at about 320 x 180, so it is a generated hero (D-123): a green panel with the icon, name, headline and subline, beside the real tray list from that locale's `raw/microsoft/<locale>/00-tray-dark.png`, enlarged whole. Each listing locale has its own, drawn from its own capture and its `heroHeadline` and `heroSubline` in `listings.json`; en-CA and fr-CA use en-US's and fr-FR's. Its caption is the subline. `npm run hero` draws them (devkit, through winui-app-kit's `scripts/devkit`), and `npm run check:hero` in `validate` fails when one no longer matches its inputs. New or changed headlines are approved by the maintainer, like any screenshot.
