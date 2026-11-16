# Microsoft Store listing text

`listings.json` is the draft listing text in the shared `schemaVersion: 1` / `locales` shape (LIST-3), for the app's nine locale identities: `en-US`, `en-CA`, `es-ES`, `fr-FR`, `fr-CA`, `de-DE`, `zh-CN`, `pt-BR` and `ja-JP` (the same set the package manifest declares). Seven records hold wording; `en-CA` is `{ "copyFrom": "en-US" }` and `fr-CA` is `{ "copyFrom": "fr-FR" }`, matching the app's own catalogs. Fields are semantic names, not Partner Center CSV field IDs: when importing, merge into the real Partner Center export and keep its opaque `Field`/`ID` cells. No publisher reads this file yet, so it is a draft (LIST-9).

Release notes are not in this file. Each version's "What's new" text is `release-notes/<version>.md` (`en-US`) plus one `release-notes/<version>.<identity>.md` per other wording catalog, the same text the GitHub Release uses (REL-6, LIST-10).

Translated wording uses the same terms as the in-app catalogs (`WslMonitor/Localization/Catalogs`) so the listing matches what people see. Every translation, in the listing and in the release notes, was drafted by AI without native-speaker review (LOC-12).

`npm run check:listing` (part of `npm run validate`) checks this file and the current version's notes: the locales and aliases, every field, the limits below, matching caption counts, no em dashes, and that the README tagline is the first sentence of the `en-US` short description.

## Limits

| Text | Limit | Longest draft |
| --- | --- | --- |
| Full description | 4,000 characters | 2,715 (de-DE) |
| Short description | 270 characters | 237 (de-DE) |
| Features | Up to 20, each up to 200 characters | 13, longest 116 (de-DE) |
| Keywords | Up to 7, each up to 40 characters, 21 words combined | 7 per locale, at most 16 words (es-ES, pt-BR) |
| Screenshot captions | One per screenshot, up to 200 characters | 4 per locale |
| Release notes (`release-notes/`) | 1,500 characters | 674 (1.0.0, fr-FR) |

## Claims to keep true

- Stop takes two clicks; distros running systemd stop with `systemctl poweroff`, others with `wsl --terminate`. Force stop is offered after 2 minutes, or at once if the clean stop could not be attempted. Don't promise that every distro stops cleanly.
- Shut down WSL stops running distros cleanly, asks before forcing any that miss the limit, then runs `wsl --shutdown`.
- Start runs a no-op in the distro; whether it stays running depends on WSL's idle timeout, which the Overview page explains. Don't promise that a started distro keeps running.
- "Make default" changes WSL's default distro for every program.
- Docker Desktop's internal distros are hidden unless turned on in Settings.
- Privacy: no account, ads, analytics or telemetry. Settings and logs are local. The only network request is the "Our other apps" page fetching the public promo list from GitHub when it opens.
- The "Our other apps" page follows the app's language; only the app descriptions it downloads (from the shared apps-promo catalog) are in English.
- The interface is in English, Spanish, French, German, Simplified Chinese, Brazilian Portuguese and Japanese, following Windows or a choice in Settings. Distro names, paths and WSL's own messages are never translated.
- Requires Windows 11 (the manifest's minimum version) and WSL with at least one installed distro.

## Console-only fields

| Field | Value |
| --- | --- |
| Store ID | 9MTQWTX87B8J (https://apps.microsoft.com/detail/9MTQWTX87B8J, live once the first submission is published) |
| Package identity | `DynamicSolutionsCA.WSLMonitor`, publisher `CN=C21E6CEF-D0D1-4497-93F9-3718D054DA0E` (the manifest switches to it in a later change; until then it carries a placeholder) |
| Category | Not set; suggested Developer tools |
| Pricing | Paid, US $0.99 (price tier Tier1012), with the same free trial as the other paid apps; the trial length is set in Partner Center (D-014) |
| Age rating | Questionnaire not completed |
| Privacy policy URL | Not available yet; it comes from the maintainer (D-017) |

## Screenshots (not captured yet)

Planned set, in caption order: the tray list above the taskbar, a distro's Stop button asking for the second click, the Overview page, and a distro's page. English captures can serve every listing, or capture each language by switching Language in Settings. Capture the real installed app with no personal distro names, user names or paths visible (use stock distro names such as Ubuntu and Debian), and alternate light and dark themes. Files, their order and alt text go in `store-assets/manifest.json` when they exist (LIST-8). Microsoft requires at least one 1366 x 768 or larger PNG; see [screenshots and images](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/screenshots-and-images).

## Open questions before submission

- Category, age rating and the privacy policy URL (above).
- Add the product ID to the shared promo catalog (apps-promo) once the listing is live.
