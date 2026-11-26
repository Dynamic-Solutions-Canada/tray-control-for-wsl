using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using WinUIAppKit.Localization;
using WslMonitor.Models;
using WslMonitor.Services;

namespace WslMonitor.Controls;

/// <summary>
/// One distro as a list row (tray flyout and Overview page): badge, name and summary as a borderless
/// button that opens the distro's details, with the Start / Stop button beside it so power clicks
/// never also open anything. The hover and press highlight belongs to the whole row (power button
/// included), so it reads as one item; the inner button's own highlight is switched off.
/// </summary>
public static class DistroRow
{
    /// <param name="onOpen">Runs when the row itself (not the power button) is clicked.</param>
    public static Grid Build(Distro d, WslMonitorService service, Action onOpen)
    {
        var res = Application.Current.Resources;

        var text = new StackPanel { Margin = new Thickness(12, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock
        {
            Text = d.Name,
            Style = (Style)res["BodyStrongTextBlockStyle"],
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        text.Children.Add(DistroVisuals.Caption(d));

        var info = new Grid();
        info.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        info.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        info.Children.Add(DistroVisuals.Badge(d));
        Grid.SetColumn(text, 1);
        info.Children.Add(text);

        var open = new Button
        {
            Content = info,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(10, 8, 8, 8),
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0),
        };
        ClearButtonHighlight(open);
        ToolTipService.SetToolTip(open, UiText.Format("row.details-tip", ("name", d.Name)));
        // The content is a badge and text in a grid, which gives the button no accessible name of its own.
        AutomationProperties.SetName(open, d.Name);
        AutomationProperties.SetAutomationId(open, "DistroRow_" + d.Name);
        open.Click += (_, _) => onOpen();

        var power = new DistroPowerButton(d, service) { Margin = new Thickness(0, 0, 10, 0) };

        var row = new Grid
        {
            CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),   // hit-testable everywhere
        };
        HighlightOnHover(row);
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(open);
        Grid.SetColumn(power, 1);
        row.Children.Add(power);
        return row;
    }

    /// <summary>Make a button's own hover and press backgrounds transparent (the row draws them instead).</summary>
    private static void ClearButtonHighlight(Button button)
    {
        var clear = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        foreach (string key in new[]
                 {
                     "ButtonBackgroundPointerOver", "ButtonBackgroundPressed",
                     "ButtonBorderBrushPointerOver", "ButtonBorderBrushPressed",
                 })
        {
            button.Resources[key] = clear;
        }
    }

    /// <summary>
    /// The standard subtle list-item highlight on the whole row: secondary fill on hover, tertiary
    /// while pressed. Press events are observed even when a child button handles them.
    /// </summary>
    private static void HighlightOnHover(Grid row)
    {
        var res = Application.Current.Resources;
        var rest = row.Background;
        var hover = (Brush)res["SubtleFillColorSecondaryBrush"];
        var pressed = (Brush)res["SubtleFillColorTertiaryBrush"];

        row.PointerEntered += (_, _) => row.Background = hover;
        row.PointerExited += (_, _) => row.Background = rest;
        row.PointerCanceled += (_, _) => row.Background = rest;
        row.PointerCaptureLost += (_, _) => row.Background = rest;
        row.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler((_, _) => row.Background = pressed), true);
        row.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler((_, _) => row.Background = hover), true);
    }
}
