using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using WinUIAppKit.Localization;
using WinUIAppKit.Pages;
using WinUIAppKit.Windowing;
using WslMonitor.Classes;
using WslMonitor.Controls;
using WslMonitor.Models;
using WslMonitor.Services;
using WslMonitor.Pages;

namespace WslMonitor;

/// <summary>
/// The full window, hosted in the kit's <see cref="SettingsShell"/> (UI-1, UI-2): an Overview page, one
/// page per distro, Settings, Our other apps and About. Opened from the flyout's Details button, a distro
/// row, or the tray menu. One instance at a time.
/// </summary>
public sealed partial class DetailsWindow : Window
{
    // This window's own item tags. Distro items are prefixed, so a distro named like a page can't collide;
    // Settings, Our other apps and About use the shell's tags.
    private const string OverviewTag = "overview";
    private const string DistroTagPrefix = "distro:";

    private static DetailsWindow? _instance;

    private string _menuSignature = "";

    /// <summary>The open window, or null when it is closed. Pages use it to ask questions on it.</summary>
    internal static Window? Instance => _instance;

    // ── Opening ─────────────────────────────────────────────────────────
    /// <summary>Show the window (creating it if needed) on its current page (Overview for a new window).</summary>
    public static void ShowInstance() => Show(null);

    /// <summary>Show the window on the Settings page.</summary>
    public static void ShowSettings() => Show(SettingsShell.SettingsTag);

    /// <summary>Show the window on a distro's page (Overview if no such distro is listed).</summary>
    public static void ShowDistro(string name) => Show(DistroTagPrefix + name);

    /// <summary>
    /// Open the Settings page afresh after Reset settings, so the kit's rows, which read their state
    /// when created, show the defaults. Unlike a language change, a reset changes what the rows hold.
    /// </summary>
    public static void ReloadSettingsPage()
    {
        if (_instance?.Shell.ContentFrame.Content is GeneralPage)
        {
            _instance.Shell.ContentFrame.Navigate(typeof(GeneralPage), null);
        }
    }

    private static void Show(string? tag)
    {
        _instance ??= new DetailsWindow();
        if (tag != null)
        {
            _instance.Select(tag);
        }

        WindowChrome.BringToFront(_instance);
    }

    private DetailsWindow()
    {
        InitializeComponent();
        Closed += OnClosed;

        Shell.Caption = "Tray Control for WSL";
        Shell.IconPngPath = App.IconPngPath;
        WindowChrome.SetUpSettingsWindow(this, Shell.TitleBar, App.IconPath, SettingsManager.Current.DetailsWindow);
        Shell.PageSelected += OnPageSelected;

        RebuildMenu();
        Select(OverviewTag);

        MainWindow.Service.Changed += OnServiceChanged;
        UiText.Changed += OnLanguageChanged;

        // Fast polling only while the window can be seen: a minimized window doesn't need it.
        AppWindow.Changed += (_, _) => UpdateViewerState();
        UpdateViewerState();
    }

    private void UpdateViewerState()
    {
        bool minimized = AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized };
        MainWindow.Service.SetViewerActive(this, AppWindow.IsVisible && !minimized);
    }

    // ── Menu ────────────────────────────────────────────────────────────
    /// <summary>Rebuild the menu when distros are added, removed or renamed, or the default changes.</summary>
    private void OnServiceChanged()
    {
        if (MenuSignature() != _menuSignature)
        {
            RebuildMenuKeepingSelection();
        }
    }

    /// <summary>
    /// The interface language changed. Static labels and the pages relabel themselves; the distro
    /// menu is built in code, so rebuild it.
    /// </summary>
    private void OnLanguageChanged() => RebuildMenuKeepingSelection();

    /// <summary>What the distro menu shows, to tell whether it needs rebuilding.</summary>
    private static string MenuSignature() =>
        string.Join("|", MainWindow.Service.Distros.Select(d => $"{d.Name}:{d.IsDefault}"));

    /// <summary>
    /// Rebuild the menu and select the same item again. Clearing the menu leaves nothing selected, which
    /// the shell does not report; selecting the item again finds its page already showing (see Navigate).
    /// </summary>
    private void RebuildMenuKeepingSelection()
    {
        string tag = Shell.SelectedTag ?? OverviewTag;
        RebuildMenu();
        Select(tag);
    }

    /// <summary>Overview, then a "Distros" header and one item per listed distro (marked if default).</summary>
    private void RebuildMenu()
    {
        var distros = MainWindow.Service.Distros;
        _menuSignature = MenuSignature();

        Shell.MenuItems.Clear();
        // Each item gets a language-neutral automation id for UI automation (the Store capture script):
        // its label is translated, and a distro's carries a translated "(default)" suffix.
        var overview = new NavigationViewItem
        {
            Content = UiText.Get("nav.overview"),
            Tag = OverviewTag,
            Icon = new FontIcon { Glyph = "\uE80F" },
        };
        AutomationProperties.SetAutomationId(overview, "OverviewNavItem");
        Shell.MenuItems.Add(overview);
        if (distros.Count == 0)
        {
            return;
        }

        Shell.MenuItems.Add(new NavigationViewItemHeader { Content = UiText.Get("nav.distros") });
        foreach (var d in distros)
        {
            var item = new NavigationViewItem
            {
                Content = d.IsDefault ? UiText.Format("nav.default-suffix", ("name", d.Name)) : d.Name,
                Tag = DistroTagPrefix + d.Name,
                Icon = new FontIcon { Glyph = DistroVisuals.Glyph },
            };
            AutomationProperties.SetAutomationId(item, "DistroNavItem_" + d.Name);
            Shell.MenuItems.Add(item);
        }
    }

    // ── Navigation ──────────────────────────────────────────────────────
    /// <summary>
    /// Select an item by tag; the shell applies it once loaded if the window is still opening. An
    /// unknown distro falls back to Overview.
    /// </summary>
    private void Select(string tag)
    {
        if (!Shell.Select(tag))
        {
            Shell.Select(OverviewTag);
        }
    }

    private void OnPageSelected(string tag)
    {
        if (tag == SettingsShell.SettingsTag)
        {
            Navigate(typeof(GeneralPage), null);
        }
        else if (tag == SettingsShell.AboutTag)
        {
            Navigate(typeof(AboutPage), AboutContent.Create());
        }
        else if (tag == SettingsShell.OtherAppsTag)
        {
            Navigate(typeof(TechnicallyReal.Promo.PromotedAppsPage), null);
        }
        else if (tag.StartsWith(DistroTagPrefix))
        {
            Navigate(typeof(DistroPage), tag[DistroTagPrefix.Length..]);
        }
        else
        {
            Navigate(typeof(OverviewPage), null);
        }
    }

    /// <summary>Show a page, unless that exact page (and distro) is already showing.</summary>
    /// <param name="parameter">The distro name for a distro page, the <see cref="AboutInfo"/> for About, otherwise null.</param>
    private void Navigate(Type page, object? parameter)
    {
        var frame = Shell.ContentFrame;
        if (frame.Content is DistroPage current && page == typeof(DistroPage) && current.DistroName == parameter as string)
        {
            return;
        }

        if (frame.Content?.GetType() == page && page != typeof(DistroPage))
        {
            return;
        }

        frame.Navigate(page, parameter);
    }

    // ── Dialogs ─────────────────────────────────────────────────────────
    /// <summary>
    /// "Shut down WSL" found distros that didn't stop cleanly: open this window (wherever the shutdown
    /// was started, including the tray flyout) and ask whether to cut them off. False means don't.
    /// </summary>
    public static async Task<bool> ConfirmForceShutdownAsync(IReadOnlyList<string> stuck)
    {
        ShowInstance();
        if (_instance is { } window)
        {
            await window.WhenContentLoadedAsync();
        }

        return await ConfirmAsync(
            UiText.Get("overview.force-title"),
            UiText.Format("overview.force-body",
                ("names", new NameList(stuck)),
                ("minutes", (int)WslMonitorService.CleanStopLimit.TotalMinutes)),
            UiText.Get("overview.force-button"));
    }

    /// <summary>Completes once the window's content has a XamlRoot (immediately if it already has).</summary>
    private Task WhenContentLoadedAsync()
    {
        var root = (FrameworkElement)Content;
        if (root.XamlRoot != null && root.IsLoaded)
        {
            return Task.CompletedTask;
        }

        var loaded = new TaskCompletionSource();
        void OnLoaded(object sender, RoutedEventArgs e)
        {
            root.Loaded -= OnLoaded;
            loaded.TrySetResult();
        }
        root.Loaded += OnLoaded;
        return loaded.Task;
    }

    /// <summary>
    /// Ask a yes/no question on this window, whichever page is showing, through the kit's
    /// <see cref="ConfirmDialog"/>. Returns false (no) when the window isn't open, when it already
    /// shows a question, or if it closes while the question is up, so a waiting operation never
    /// proceeds on a guess.
    /// </summary>
    public static Task<bool> ConfirmAsync(string title, string body, string confirmText)
    {
        if (_instance is not { } window)
        {
            return Task.FromResult(false);
        }

        return ConfirmDialog.ConfirmAsync(window, title, body, confirmText);
    }

    // ── Closing ─────────────────────────────────────────────────────────
    private void OnClosed(object sender, WindowEventArgs e)
    {
        _instance = null;
        MainWindow.Service.Changed -= OnServiceChanged;
        UiText.Changed -= OnLanguageChanged;
        MainWindow.Service.SetViewerActive(this, false);

        // Unloaded isn't reliably raised when a window closes; drop the current page's subscriptions here.
        if (Shell.ContentFrame.Content is LivePage page)
        {
            page.Detach();
        }

        if (WindowChrome.CapturePlacement(this) is { } placement)
        {
            SettingsManager.Current.DetailsWindow = placement;
            SettingsManager.Save();
        }
    }
}
