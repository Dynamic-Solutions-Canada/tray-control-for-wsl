using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WslMonitor.Models;
using WslMonitor.Services;

namespace WslMonitor.Controls;

/// <summary>
/// One distro as a list row (tray flyout and Overview page): badge, name and summary as a borderless
/// button that opens the distro's details, with the Start / Stop button beside it so power clicks
/// never also open anything.
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
        ToolTipService.SetToolTip(open, $"Show details for {d.Name}");
        open.Click += (_, _) => onOpen();

        var power = new DistroPowerButton(d, service) { Margin = new Thickness(0, 0, 10, 0) };

        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(open);
        Grid.SetColumn(power, 1);
        row.Children.Add(power);
        return row;
    }
}
