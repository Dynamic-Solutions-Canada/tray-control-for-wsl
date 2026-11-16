using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinUIAppKit.Localization;

namespace TechnicallyReal.Promo;

/// <summary>
/// Translates the shared "Our other apps" page (external/promo, English only) without changing it.
/// The shared page still loads, caches and orders the catalog; this adapter only swaps copy whose
/// English text exactly matches a catalogued <c>promo.*</c> source string. App names, links and icons
/// stay as they are, and copy the catalog doesn't know (a newly added app) stays in English.
/// Same shape as Persistent's adapter, with the same keys and wording.
/// </summary>
public sealed partial class PromotedAppsPage
{
    private static readonly string[] PromoCopyKeys =
    [
        "promo.title", "promo.store-subtitle", "promo.website", "promo.view-store",
        "promo.printstream", "promo.little-launcher", "promo.immichdrive", "promo.repilot", "promo.persistent",
    ];

    private object? _promoOriginalItems;
    private object? _promoPresentedItems;
    private bool _promoApplying;
    private bool _promoAttached;
    private long _promoSourceCallback;

    /// <summary>Start translating this page's copy, and keep it translated while it is loaded. Safe to call repeatedly.</summary>
    internal void EnableLocalization()
    {
        Loaded -= PromoLoaded;
        Loaded += PromoLoaded;
        Unloaded -= PromoUnloaded;
        Unloaded += PromoUnloaded;
        AttachPromoLocalization();
    }

    private void PromoLoaded(object sender, RoutedEventArgs args) => AttachPromoLocalization();

    private void AttachPromoLocalization()
    {
        if (_promoAttached)
        {
            return;
        }

        _promoAttached = true;

        // The shared page replaces its ItemsSource after a refresh; keep the English original each time.
        if (!ReferenceEquals(AppList.ItemsSource, _promoPresentedItems))
        {
            _promoOriginalItems = AppList.ItemsSource;
        }

        _promoSourceCallback = AppList.RegisterPropertyChangedCallback(ItemsControl.ItemsSourceProperty, (_, _) =>
        {
            if (_promoApplying)
            {
                return;
            }

            _promoOriginalItems = AppList.ItemsSource;
            RefreshPromoCopy();
        });
        UiText.Changed += RefreshPromoCopy;

        // The page's own two headings (title and subtitle), matched by their exact English text.
        if (Content is ScrollViewer viewer && viewer.Content is Panel panel)
        {
            foreach (var text in panel.Children.OfType<TextBlock>())
            {
                if (KnownPromoKey(text.Text) is { } key)
                {
                    Localize.SetText(text, key);
                }
            }
        }

        RefreshPromoCopy();
    }

    /// <summary>Present a translated copy of the original English items, so a later change never translates a translation.</summary>
    private void RefreshPromoCopy()
    {
        if (_promoOriginalItems is not IEnumerable<PromotedApp> apps)
        {
            return;
        }

        _promoApplying = true;
        try
        {
            _promoPresentedItems = apps.Select(app => new PromotedApp
            {
                Name = app.Name,
                Destination = app.Destination,
                Icon = app.Icon,
                Blurb = PromoCopy(app.Blurb),
                ActionLabel = PromoCopy(app.ActionLabel),
            }).ToList();
            AppList.ItemsSource = _promoPresentedItems;
        }
        finally
        {
            _promoApplying = false;
        }
    }

    private static string? KnownPromoKey(string value) => PromoCopyKeys.FirstOrDefault(key => UiText.Source(key) == value);

    private static string PromoCopy(string value) => KnownPromoKey(value) is { } key ? UiText.Get(key) : value;

    private void PromoUnloaded(object sender, RoutedEventArgs args) => DisableLocalization();

    /// <summary>
    /// Stop following language changes (Unloaded, or the window closing, which doesn't reliably raise
    /// Unloaded). Safe to call more than once.
    /// </summary>
    internal void DisableLocalization()
    {
        if (!_promoAttached)
        {
            return;
        }

        UiText.Changed -= RefreshPromoCopy;
        AppList.UnregisterPropertyChangedCallback(ItemsControl.ItemsSourceProperty, _promoSourceCallback);
        _promoAttached = false;
    }
}
