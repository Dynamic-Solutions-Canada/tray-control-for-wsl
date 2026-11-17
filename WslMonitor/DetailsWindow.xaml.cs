using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using WinUIAppKit.Localization;
using WinUIAppKit.Theming;
using WinUIAppKit.Windowing;
using WslMonitor.Classes;
using WslMonitor.Controls;
using WslMonitor.Models;
using WslMonitor.Services;
using WslMonitor.Pages;

namespace WslMonitor;

/// <summary>
/// The full window (Mica + NavigationView, the same shape as the sibling apps' settings windows):
/// an Overview page, one page per distro, Settings, Our other apps and About. Opened from the
/// flyout's Details button, a distro row, or the tray menu. One instance at a time.
/// </summary>
public sealed partial class DetailsWindow : Window
{
    // Menu item tags. Distro items are prefixed, so a distro named like a page ("settings") can't collide.
    private const string OverviewTag = "overview";
    private const string SettingsTag = "settings";
    private const string AboutTag = "about";
    private const string PromoTag = "promo";
    private const string DistroTagPrefix = "distro:";

    private static DetailsWindow? _instance;

    private string _menuSignature = "";
    private bool _rebuilding;
    private string? _pendingTag;            // a selection asked for before the NavigationView loaded

    // ── Opening ─────────────────────────────────────────────────────────
    /// <summary>Show the window (creating it if needed) on its current page (Overview for a new window).</summary>
    public static void ShowInstance() => Show(null);

    /// <summary>Show the window on the Settings page.</summary>
    public static void ShowSettings() => Show(SettingsTag);

    /// <summary>Show the window on a distro's page (Overview if no such distro is listed).</summary>
    public static void ShowDistro(string name) => Show(DistroTagPrefix + name);

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

        SystemBackdrop = new MicaBackdrop();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        WindowIcon.Apply(this, App.IconPath);
        try { TitleBarIcon.Source = new BitmapImage(new Uri(App.IconPngPath)); } catch { }

        WindowChrome.RestorePlacement(this, SettingsManager.Current.DetailsWindow, 980, 720);
        AppTheme.Track(this);
        RootNavigation.Loaded += RootNavigation_Loaded;

        RebuildMenu();
        Select(OverviewTag);

        MainWindow.Service.Changed += OnServiceChanged;
        UiText.Changed += OnLanguageChanged;

        // Fast polling only while the window can be seen: a minimized window doesn't need it.
        AppWindow.Changed += (_, _) => UpdateViewerState();
        UpdateViewerState();
    }

    /// <summary>
    /// The built-in Settings item exists only once the NavigationView's template is applied. Give it
    /// our wording (its own label, tooltip and accessible name follow Windows' language) and apply any
    /// selection that was asked for before then.
    /// </summary>
    private void RootNavigation_Loaded(object sender, RoutedEventArgs e)
    {
        if (RootNavigation.SettingsItem is NavigationViewItem settingsItem)
        {
            Localize.SetContent(settingsItem, "nav.settings");
            Localize.SetToolTip(settingsItem, "nav.settings");
            Localize.SetName(settingsItem, "nav.settings");
        }

        if (_pendingTag is { } tag)
        {
            _pendingTag = null;
            Select(tag);
        }
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

    private void RebuildMenuKeepingSelection()
    {
        // Clearing the menu deselects the current item; suppress the resulting navigation and put the
        // same page back afterwards (Navigate skips it when it is already showing).
        string tag = CurrentTag();
        _rebuilding = true;
        try
        {
            RebuildMenu();
        }
        finally
        {
            _rebuilding = false;
        }
        Select(tag);
    }

    /// <summary>Overview, then a "Distros" header and one item per listed distro (marked if default).</summary>
    private void RebuildMenu()
    {
        var distros = MainWindow.Service.Distros;
        _menuSignature = MenuSignature();

        RootNavigation.MenuItems.Clear();
        RootNavigation.MenuItems.Add(new NavigationViewItem
        {
            Content = UiText.Get("nav.overview"),
            Tag = OverviewTag,
            Icon = new FontIcon { Glyph = "" },
        });
        if (distros.Count == 0)
        {
            return;
        }

        RootNavigation.MenuItems.Add(new NavigationViewItemHeader { Content = UiText.Get("nav.distros") });
        foreach (var d in distros)
        {
            RootNavigation.MenuItems.Add(new NavigationViewItem
            {
                Content = d.IsDefault ? UiText.Format("nav.default-suffix", ("name", d.Name)) : d.Name,
                Tag = DistroTagPrefix + d.Name,
                Icon = new FontIcon { Glyph = DistroVisuals.Glyph },
            });
        }
    }

    // ── Navigation ──────────────────────────────────────────────────────
    /// <summary>The selected item's tag.</summary>
    private string CurrentTag()
    {
        var selected = RootNavigation.SelectedItem;
        if (selected != null && selected == RootNavigation.SettingsItem)
        {
            return SettingsTag;
        }

        return (selected as NavigationViewItem)?.Tag as string ?? OverviewTag;
    }

    /// <summary>Select a menu item by tag. An unknown distro falls back to Overview.</summary>
    private void Select(string tag)
    {
        if (tag == SettingsTag)
        {
            if (RootNavigation.SettingsItem == null)
            {
                _pendingTag = tag;   // applied in RootNavigation_Loaded
                return;
            }
            RootNavigation.SelectedItem = RootNavigation.SettingsItem;
            return;
        }

        if (tag == AboutTag || tag == PromoTag)
        {
            RootNavigation.SelectedItem = RootNavigation.FooterMenuItems.OfType<NavigationViewItem>()
                .First(i => (i.Tag as string) == tag);
            return;
        }

        var items = RootNavigation.MenuItems.OfType<NavigationViewItem>();
        RootNavigation.SelectedItem =
            items.FirstOrDefault(i => string.Equals(i.Tag as string, tag, StringComparison.OrdinalIgnoreCase))
            ?? items.First();
    }

    private void RootNavigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (_rebuilding)
        {
            return;
        }

        if (args.IsSettingsSelected)
        {
            Navigate(typeof(GeneralPage), null);
            return;
        }

        string tag = (args.SelectedItem as NavigationViewItem)?.Tag as string ?? OverviewTag;
        if (tag == AboutTag)
        {
            Navigate(typeof(AboutPage), null);
        }
        else if (tag == PromoTag)
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
    private void Navigate(Type page, string? parameter)
    {
        if (ContentFrame.Content is DistroPage current && page == typeof(DistroPage) && current.DistroName == parameter)
        {
            return;
        }

        if (ContentFrame.Content?.GetType() == page && page != typeof(DistroPage))
        {
            return;
        }

        ContentFrame.Navigate(page, parameter);
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
        switch (ContentFrame.Content)
        {
            case LivePage page: page.Detach(); break;
        }

        if (WindowChrome.CapturePlacement(this) is { } placement)
        {
            SettingsManager.Current.DetailsWindow = placement;
            SettingsManager.Save();
        }
    }
}
