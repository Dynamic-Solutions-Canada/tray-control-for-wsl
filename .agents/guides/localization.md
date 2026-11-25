> **Read when:** adding or changing any user-visible text, a language, or the language setting.
> **Governs:** `WslMonitor/Localization/**`, `WslMonitor/Classes/AppLanguages.cs`, `release-notes/**`, `store-assets/microsoft/listings.json`

# Localization

The app ships the nine-locale set from the standards' [locales.md](https://github.com/Dynamic-Solutions-Canada/app-standards/blob/main/standards/locales.md): nine locale identities in `Classes/AppLanguages.cs`, read from seven wording catalogs (en-CA and fr-CA reuse en-US and fr-FR wording).

## Interface text

- UI text lives in `Localization/Catalogs/*.json`, keyed by the English text and embedded in the exe. Use `UiText` in code and `l10n:Localize.*` in XAML.
- Add every new key to all seven catalogs, then run `npm run check:l10n` (winui-app-kit's check, configured by the `localization` block in `windows-build.json`). Keys the code builds from constants (the About page's `DescriptionKey`) are listed there as `extra_key_patterns`.
- Shared labels are winui-app-kit's `kit.*` keys, translated in the kit; use them and never define a `kit.` key here.
- `AppDescription` is the one identifier key: the package manifest's `ms-resource:Resources/<Key>` syntax needs one. It is both the About page description and the manifest description, which `manifest_strings` in `windows-build.json` writes into `resources.pri` for every language at packaging.
- Models and services store keys, not text: `Distro.BusyReason`, and `LocalizedMessage` for `Distro.LastError` and `ShutdownStatus`; pass a `WslResult` or `NameList` as a value and let the view format it.

## Language choice

- The language follows Windows unless `UserSettings.Language` is set with the kit's Language row on the Settings page (empty means "Use system setting").
- Language changes apply live: code-built UI refreshes on `UiText.Changed` (live pages in `LivePage.OnAttach`, the details menu, the flyout, the tray tooltip), and pages are never re-navigated for it. Reset settings is the one re-navigation, because the kit's rows read their state when created.
- The "Our other apps" page translates itself, app descriptions included, and follows the app's language through `PromoLocalization.LocaleProvider`, set in `MainWindow`. The app keeps no promo translations of its own.

## What stays English

These are deliberate, not gaps:

- Distro names, paths, and WSL's own output (`wsl.exe` messages shown as errors), which come from the user's system.
- Log files and settings values, which are for diagnosis and must read the same on every machine.
- The product name, "Tray Control for WSL", in every language.
- WinUI's own control text (for example a text box's Cut, Copy and Paste menu), which follows the Windows display language rather than the in-app choice: the kit does not set `PrimaryLanguageOverride`, because that would make "Use system setting" resolve to the last chosen language.

## Adding a locale

A new locale lands in one change: the catalog, `AppLanguages`, `Package.appxmanifest` `<Resources>`, every record in the Store `listings.json`, the release notes for the next version, and the README `## Languages` section.

## Review status

No catalog has had a native-speaker review yet (LOC-12). Update the row when a review happens.

| Wording catalog | Interface, listing and release notes |
| --- | --- |
| `en-US` | Source text |
| `es-ES` | AI-drafted, unreviewed |
| `fr-FR` | AI-drafted, unreviewed |
| `de-DE` | AI-drafted, unreviewed |
| `zh-CN` | AI-drafted, unreviewed |
| `pt-BR` | AI-drafted, unreviewed |
| `ja-JP` | AI-drafted, unreviewed |
