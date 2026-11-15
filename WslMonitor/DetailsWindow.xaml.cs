using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using WinUIAppKit.Theming;
using WinUIAppKit.Windowing;
using WslMonitor.Classes;
using WslMonitor.Controls;
using WslMonitor.Pages;

namespace WslMonitor;

/// <summary>
/// The full window (Mica + NavigationView, the same shape as the sibling apps' settings windows):
/// an Overview page, one page per distro, Settings and About. Opened from the flyout's Details
/// button, a distro row, or the tray menu. One instance at a time.
/// </summary>
public sealed partial class DetailsWindow : Window
{
    /// <summary>Navigation target for the built-in Settings item.</summary>
    public const string SettingsTarget = "settings";

    private const string OverviewTag = "overview";
    private const string AboutTag = "about";
    private const string DistroTagPrefix = "distro:";

    private static DetailsWindow? s_instance;
    private string _distroNames = "";
    private bool _rebuilding;

    /// <summary>
    /// Show the window (creating it if needed) and optionally navigate: a distro name, or
    /// <see cref="SettingsTarget"/>. Null keeps the current page (Overview for a new window).
    /// </summary>
    public static void ShowInstance(string? target = null)
    {
        s_instance ??= new DetailsWindow();
        if (target != null) s_instance.Select(target);
        WindowChrome.BringToFront(s_instance);
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

        RebuildDistroItems();
        Select(OverviewTag);

        MainWindow.Service.Changed += OnServiceChanged;
        MainWindow.Service.SetViewerActive(this, true);
    }

    // ── Navigation ──────────────────────────────────────────────────────
    /// <summary>Rebuild the menu when distros are added or removed, keeping the current selection.</summary>
    private void OnServiceChanged()
    {
        string names = string.Join("|", MainWindow.Service.Distros.Select(d => $"{d.Name}:{d.IsDefault}"));
        if (names == _distroNames) return;

        // Clearing the menu deselects the current item; suppress the resulting navigation and put the
        // same page back afterwards (Navigate skips it when it is already showing).
        string target = CurrentTarget();
        _rebuilding = true;
        try
        {
            RebuildDistroItems();
        }
        finally
        {
            _rebuilding = false;
        }
        Select(target);
    }

    /// <summary>The selected page as a <see cref="Select"/> target.</summary>
    private string CurrentTarget()
    {
        if (RootNavigation.SelectedItem == RootNavigation.SettingsItem) return SettingsTarget;
        string tag = (RootNavigation.SelectedItem as NavigationViewItem)?.Tag as string ?? OverviewTag;
        return tag.StartsWith(DistroTagPrefix) ? tag[DistroTagPrefix.Length..] : tag;
    }

    private void RebuildDistroItems()
    {
        var distros = MainWindow.Service.Distros;
        _distroNames = string.Join("|", distros.Select(d => $"{d.Name}:{d.IsDefault}"));

        RootNavigation.MenuItems.Clear();
        RootNavigation.MenuItems.Add(new NavigationViewItem
        {
            Content = "Overview",
            Tag = OverviewTag,
            Icon = new FontIcon { Glyph = "" },
        });
        if (distros.Count == 0) return;

        RootNavigation.MenuItems.Add(new NavigationViewItemHeader { Content = "Distros" });
        foreach (var d in distros)
        {
            RootNavigation.MenuItems.Add(new NavigationViewItem
            {
                Content = d.IsDefault ? $"{d.Name} (default)" : d.Name,
                Tag = DistroTagPrefix + d.Name,
                Icon = new FontIcon { Glyph = DistroVisuals.Glyph },
            });
        }
    }

    /// <summary>Select a page by target: overview, settings, about, or a distro name (unknown names fall back to Overview).</summary>
    private void Select(string target)
    {
        if (target == SettingsTarget)
        {
            RootNavigation.SelectedItem = RootNavigation.SettingsItem;
            return;
        }
        if (target == AboutTag)
        {
            RootNavigation.SelectedItem = RootNavigation.FooterMenuItems.OfType<NavigationViewItem>().First();
            return;
        }

        string tag = target == OverviewTag ? OverviewTag : DistroTagPrefix + target;
        var item = RootNavigation.MenuItems.OfType<NavigationViewItem>()
                       .FirstOrDefault(i => string.Equals(i.Tag as string, tag, StringComparison.OrdinalIgnoreCase))
                   ?? RootNavigation.MenuItems.OfType<NavigationViewItem>().First();
        RootNavigation.SelectedItem = item;
    }

    private void RootNavigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (_rebuilding) return;
        if (args.IsSettingsSelected)
        {
            Navigate(typeof(GeneralPage), null);
            return;
        }

        string tag = (args.SelectedItem as NavigationViewItem)?.Tag as string ?? OverviewTag;
        if (tag == AboutTag) Navigate(typeof(AboutPage), null);
        else if (tag.StartsWith(DistroTagPrefix)) Navigate(typeof(DistroPage), tag[DistroTagPrefix.Length..]);
        else Navigate(typeof(OverviewPage), null);
    }

    private void Navigate(Type page, string? parameter)
    {
        if (ContentFrame.Content is DistroPage current && page == typeof(DistroPage) && current.DistroName == parameter) return;
        if (ContentFrame.Content?.GetType() == page && page != typeof(DistroPage)) return;
        ContentFrame.Navigate(page, parameter);
    }

    /// <summary>Called by pages that want to jump to a distro's page (e.g. Overview's list).</summary>
    public static void ShowDistro(string name) => s_instance?.Select(name);

    private void OnClosed(object sender, WindowEventArgs e)
    {
        s_instance = null;
        MainWindow.Service.Changed -= OnServiceChanged;
        MainWindow.Service.SetViewerActive(this, false);

        if (WindowChrome.CapturePlacement(this) is { } placement)
        {
            SettingsManager.Current.DetailsWindow = placement;
            SettingsManager.Save();
        }
    }
}
