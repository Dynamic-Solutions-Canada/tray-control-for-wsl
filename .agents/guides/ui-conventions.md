> **Read when:** adding or changing any window, page, flyout, distro row, button or card.
> **Governs:** `WslMonitor/**/*.xaml`, `WslMonitor/**/*.xaml.cs`, `WslMonitor/Controls/**`, `WslMonitor/Windows/**`, `WslMonitor/Pages/**`, `WslMonitor/App.xaml`

# UI conventions

This app's own interface conventions. The shared family rules are in the standards' [ui.md](https://github.com/Dynamic-Solutions-Canada/app-standards/blob/main/standards/ui.md); this file records only what is specific to this app.

## Details window and pages

- `DetailsWindow` has a custom 48 px title bar (16 px icon, caption-style title) over a left `NavigationView`. The menu is built in code: Overview, then a "Distros" header with one item per distro; Settings is the built-in settings item; "Our other apps" and About are footer items, in that order.
- Every page is `ScrollViewer` then `StackPanel Style="{StaticResource PageContentStyle}"`, a `PageTitleStyle` title, then sections. A section is a `SectionHeaderStyle` heading (`FirstSectionHeaderStyle` for the first one under the title) above a `CardBorderStyle` card. These styles live in `App.xaml`; use them rather than repeating margins or brushes.
- Settings controls sit inside the card with their own `Header` (`ToggleSwitch`, `ComboBox`); a note under a control is a secondary-coloured `CaptionTextBlockStyle` text block.
- Pages that follow WSL's live state derive from `LivePage` and redraw from its `OnAttach` handlers, both on the service's `Changed` and on a language change. Pages are never re-navigated to refresh.
- Questions that block an action go through `DetailsWindow.ConfirmAsync` (a `ContentDialog`), never a dialog owned by one page, so they work whichever page is showing.

## Tray flyout

- `DistroFlyout` is three rows: a `BodyStrongTextBlockStyle` header, the distro rows (built in code), and a footer on `LayerOnAcrylicFillColorDefaultBrush` with a top `CardStrokeColorDefaultBrush` border holding the status line, the Shut down WSL button and Details.
- The flyout is rebuilt from scratch on every service change and every language change; anything stateful (a pending confirmation) is keyed outside the controls.

## Distro rows and buttons

- The distro list is `DistroList` in both the flyout and the Overview page, including the same empty state when WSL or distros are missing. Do not build a second list.
- A row is `DistroRow`: badge, name and summary as one borderless button that opens the distro, with `DistroPowerButton` beside it. The whole row owns the hover and press highlight (secondary fill on hover, tertiary while pressed); the inner button's own highlight is switched off so the row reads as one item.
- The state badge, labels, summary and status line come from `DistroVisuals`; take them from there rather than choosing brushes per view.
- Destructive buttons (Stop, Force stop, the flyout's Shut down WSL) use `ClickConfirmation`: the first click turns the button red and asks, the second within 4 s confirms. A second click inside the double-click time is ignored, so a double-click cannot arm and confirm at once. Confirmation state is keyed (per distro, or for shutdown) so a rebuilt list keeps it.
- Button content with an icon uses `IconLabelStyle` (a horizontal panel) with a `FontIcon` in `ButtonGlyphStyle` (14 px), then the label.

## Text

- Visible text follows the catalog rule under "Conventions and gotchas" in [AGENTS.md](../../AGENTS.md).
