# Store assets

| Folder | Holds |
| --- | --- |
| `microsoft/listings.json`, `listings.md` | The Store listing text in every locale, and the claims it makes (see `listings.md`) |
| `manifest.json` | Every Store screenshot in listing order: its raw source, theme, crop, en-US caption and alt text (LIST-8) |
| `raw/microsoft/` | The raw captures: the test VM's whole desktop, each with the `.json` sidecar the capture helper writes |
| `microsoft/screenshots/` | The listing screenshots, rendered from the raw captures; the generated first screenshot is in a folder per locale (`<locale>/00-hero-dark.png`) |
| `microsoft/text-free/` | The hero without its text, for the website (`npm run hero` writes it) |

## Screenshots

They are real captures of the installed app on the Windows 11 test VM with stock Ubuntu and Debian distros (WSL 1, since the test VM can't run WSL 2) installed under `C:\WSL`, so no personal distro names, user names or paths show. Every capture is dark, at 100% scale, with window effects on and the dark wallpaper. English captures serve every listing; the hero's text is translated. A new or changed screenshot is approved by the maintainer before it is committed.

- **Render** with `npm run shots:render` (winui-app-kit's renderer; its README has the rules): each capture in `raw/microsoft/` is cropped to 1366 x 768 and written to `microsoft/screenshots/`. Recaptioning needs no recapture; a new look does.
- **Check** with `npm run check:screenshots` (part of `npm run validate`): every capture has its sidecar and follows the capture rules above, every screenshot is the renderer's output, and the captions match the en-US `screenshotCaptions` in `listings.json`.
- **Sync** with the next Store release: the Store workflow's `screenshots` input uploads every locale's set, its hero first, with that locale's captions from `listings.json`.

A UI change that shows in a screenshot means recapturing that shot.

## The first screenshot: a generated hero

Store search shows only the first screenshot, at about 320 x 180, so it is a generated hero (D-123): a green panel with the icon, name, headline and subline, beside the real tray list from `raw/microsoft/00-tray-dark.png`, enlarged whole. Each listing locale has its own, drawn from its `heroHeadline` and `heroSubline` in `listings.json`; en-CA and fr-CA use en-US's and fr-FR's. Its caption is the subline. `npm run hero` draws them (devkit, through winui-app-kit's `scripts/devkit`), and `npm run check:hero` in `validate` fails when one no longer matches its inputs. New or changed headlines are approved by the maintainer, like any screenshot.
