# Microsoft Store listing text

`listings.json` is the listing text in the shared `schemaVersion: 1` / `locales` shape (LIST-3), for the app's nine locale identities: `en-US`, `en-CA`, `es-ES`, `fr-FR`, `fr-CA`, `de-DE`, `zh-CN`, `pt-BR` and `ja-JP` (the same set the package manifest declares). Seven records hold wording; `en-CA` is `{ "copyFrom": "en-US" }` and `fr-CA` is `{ "copyFrom": "fr-FR" }`, matching the app's own catalogs. Fields are semantic names, not Partner Center CSV field IDs: when importing, merge into the real Partner Center export and keep its opaque `Field`/`ID` cells. The Store workflow uploads packages, release notes, and the screenshots with each locale's captions from this file; the rest of the listing text is entered by hand in Partner Center from it (LIST-9).

Release notes are not in this file. Each version's "What's new" text is `release-notes/<version>.md` (`en-US`) plus one `release-notes/<version>.<identity>.md` per other wording catalog, the same text the GitHub Release uses (REL-6, LIST-10).

Translated wording uses the same terms as the in-app catalogs (`WslMonitor/Localization/Catalogs`) so the listing matches what people see. Every translation, in the listing and in the release notes, was drafted by AI without native-speaker review (LOC-12).

`npm run check:listing` (part of `npm run validate`) checks this file and the current version's notes: the locales and aliases, every field, the limits below, matching caption counts, no em dashes, and that the README tagline is the first sentence of the `en-US` short description.

## Limits

| Text | Limit | Longest draft |
| --- | --- | --- |
| Full description | 4,000 characters | 2,338 (de-DE) |
| Short description | 270 characters | 248 (fr-FR) |
| Features | Up to 20, each up to 200 characters | 13, longest 116 (de-DE) |
| Keywords | Up to 7, each up to 40 characters, 21 words combined | 7 per locale, at most 15 words (es-ES, pt-BR) |
| Screenshot captions | One per screenshot, up to 200 characters | 5 per locale |
| Release notes (`release-notes/`) | 1,500 characters | 704 (1.0.0, fr-FR) |

## Claims to keep true

- Stop takes two clicks; distros running systemd stop with `systemctl poweroff`, others with `wsl --terminate`. Force stop is offered after 2 minutes, or at once if the clean stop could not be attempted. Don't promise that every distro stops cleanly.
- Shut down WSL stops every running distro at once, the same way Stop does (so only systemd distros stop cleanly), asks before forcing any that miss the limit, then runs `wsl --shutdown`. Don't call it a clean shutdown of every distro.
- Start runs a no-op in the distro; whether it stays running depends on WSL's idle timeout, which the Overview page explains. Don't promise that a started distro keeps running.
- "Make default" changes WSL's default distro for every program.
- Docker Desktop's internal distros are hidden unless turned on in Settings.
- Privacy: no account, ads, analytics or telemetry. Settings and logs are local. The only network request a Store install makes is the "Our other apps" page fetching the public promo list from GitHub when it opens; Check for updates opens the Store instead (a copy run without a package asks GitHub Releases).
- The "Our other apps" page follows the app's language, including the app descriptions it downloads from the shared apps-promo catalog.
- The interface is in English, Spanish, French, German, Simplified Chinese, Brazilian Portuguese and Japanese, following Windows or a choice in Settings. Distro names, paths and WSL's own messages are never translated.
- Requires Windows 11 (the manifest's minimum version) and WSL with at least one installed distro.

## Console-only fields

| Field | Value |
| --- | --- |
| Product name | Tray Control for WSL in every language, untranslated (reserved 2026-11-23). "WSL Status" was rejected under policy 10.1.1.1 because a name leading with "WSL" contains the title of Microsoft's software; before it, "WSL Monitor" was rejected as too close to an existing app, "WSL Tray Monitor" (D-118). The identifiers below keep the oldest name so installs and settings carry over |
| Store ID | 9MTQWTX87B8J (https://apps.microsoft.com/detail/9MTQWTX87B8J, live) |
| Package identity | `DynamicSolutionsCA.WSLMonitor`, publisher `CN=B55B96A5-F10C-49D3-8D0B-7260EDBE7625` (dev builds use `TechnicallyReal.WslMonitor`) |
| Category | Developer tools (live Store page, checked 2026-11-24) |
| Pricing | Paid, US $0.99 (price tier Tier1012, sent by `store-publish.yml`), with a 7-day free trial, matching ImmichDrive; not offered in Lebanon or Western Sahara. The trial and markets are set in Partner Center, not by the workflow (D-014) |
| Age rating | ESRB Everyone (live Store page, checked 2026-11-24) |
| Privacy policy URL | https://www.dynamic-solutions.ca/privacy/tray-control-for-wsl (BRAND-10; the About page links it; the old /privacy/wsl-status address redirects there permanently) |

## Screenshots

Five dark-mode screenshots, in caption order. First, a generated hero in each locale: the headline and subline from `heroHeadline` and `heroSubline`, beside the real tray list, enlarged; its caption is the subline, and it claims only what the listing says (starting and stopping distros from the tray, and seeing which are running). Then the tray list above the taskbar, a distro's Stop button asking for the second click, the Overview page, and a distro's page. Caption 5 names what a WSL 1 distro's page shows (no disk size). English captures serve every listing; the hero's text is translated. How they are captured, rendered and checked, and how the hero is drawn, is in [store-assets/README.md](../README.md).
