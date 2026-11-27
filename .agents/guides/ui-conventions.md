> **Read when:** adding or changing any window, page, flyout, distro row, button or card.
> **Governs:** `WslMonitor/**/*.xaml`, `WslMonitor/**/*.xaml.cs`, `WslMonitor/Controls/**`, `WslMonitor/Windows/**`, `WslMonitor/Pages/**`, `WslMonitor/App.xaml`

# UI conventions

This app's own interface conventions. The shared family rules are in the standards' [ui.md](https://github.com/Dynamic-Solutions-Canada/app-standards/blob/main/standards/ui.md); this file records only what is specific to this app.

## Details window and pages

- `DetailsWindow` hosts the kit's `SettingsShell` (title bar, navigation, built-in Settings, and the fixed footer: "Our other apps", then About). The window adds its own menu items in code: Overview, then a "Distros" header with one item per distro. Never add footer items or restyle the shell.
- Every page is `ScrollViewer` then `StackPanel Style="{StaticResource PageContentStyle}"`, a `PageTitleStyle` title, then sections. A section is a `SectionHeaderStyle` heading (`FirstSectionHeaderStyle` for the first one under the title) above a `CardBorderStyle` card. These styles come from winui-app-kit's shared XAML (merged in `App.xaml`); use them rather than repeating margins or brushes.
- The Settings page uses the kit's `StartupSetting`, `LanguageSetting` and `ThemeSetting` rows and saves what they report; app-specific controls sit in the same cards with their own `Header`. A note under a control is a secondary-coloured `CaptionTextBlockStyle` text block.
- About is the kit's `AboutPage`, filled from `AboutContent.Create()`; never build an app About page or restyle its cards. Product-specific cards go in `AboutInfo.ExtraCards`.
- Pages that follow WSL's live state derive from `LivePage` and redraw from its `OnAttach` handlers, both on the service's `Changed` and on a language change. Pages are never re-navigated to refresh.
- Questions that block an action go through `DetailsWindow.ConfirmAsync` (the kit's `ConfirmDialog`), never a dialog owned by one page, so they work whichever page is showing.

## Tray flyout

- `DistroFlyout` is four rows: a `BodyStrongTextBlockStyle` header, the VM usage cards (`VmUsageStrip`, hidden while the VM is off), the distro rows (built in code), and a footer on `LayerOnAcrylicFillColorDefaultBrush` with a top `CardStrokeColorDefaultBrush` border holding the status line, the Shut down WSL button and Details.
- The flyout's distro rows are rebuilt from scratch on every service change and every language change; anything stateful (a pending confirmation) is keyed outside the controls. The usage cards are the exception: they are declared in XAML and updated in place on each reading, since they change every second and a rebuild would flicker and drop an open tooltip. Re-place the flyout (`UpdateSize`) only when they show or hide.
- The usage cards follow one figure rule: a `CaptionTextBlockStyle` secondary label and a right-aligned `BodyStrongTextBlockStyle` figure, over a 28 px graphic in the accent colour (`AccentFillColorDefaultBrush`), with the meter's track and the sparkline's fill as lighter opacities of the same brush. Scales are fixed (CPU 0 to 100%), never fitted to the data. They are measurements, not status: no warning or error colours. Each card's tooltip says what the figure measures, and its help text repeats the figure for screen readers.

## Distro rows and buttons

- The distro list is `DistroList` in both the flyout and the Overview page, including the same empty state when WSL or distros are missing. Do not build a second list.
- A row is `DistroRow`: badge, name and summary as one borderless button that opens the distro, with `DistroPowerButton` beside it. The whole row owns the hover and press highlight (secondary fill on hover, tertiary while pressed); the inner button's own highlight is switched off so the row reads as one item.
- The state badge, labels, summary and status line come from `DistroVisuals`; take them from there rather than choosing brushes per view.
- Destructive buttons (Stop, Force stop, the flyout's Shut down WSL) use `ClickConfirmation`: the first click turns the button red and asks, the second within 4 s confirms. The red comes from `ConfirmButtonLook`: a panel in the button's content with white text on a red that passes contrast in both themes, a lighter edge, and its own pointer-over and pressed shades (plain text in system colours with high contrast on). Never a local `Background`, which the pointer-over state hides, nor lightweight-styling resources changed after the button loads, which its states never read. A second click inside the double-click time is ignored, so a double-click cannot arm and confirm at once. Confirmation state is keyed (per distro, or for shutdown) so a rebuilt list keeps it.
- Button content with an icon uses `IconLabelStyle` (a horizontal panel) with a `FontIcon` in `ButtonGlyphStyle` (14 px), then the label. A panel gives the button no accessible name, so name it too: `l10n:Localize.Name` with the label's key in XAML, `AutomationProperties.SetName` in code. The same goes for any button whose content isn't plain text, such as the distro row and the power button's spinner.

## Text

- Visible text follows [localization.md](localization.md).
