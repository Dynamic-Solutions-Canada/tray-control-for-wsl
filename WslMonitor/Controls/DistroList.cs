using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinUIAppKit.Localization;
using WslMonitor.Models;
using WslMonitor.Services;

namespace WslMonitor.Controls;

/// <summary>
/// The distro list as shown in the tray flyout and on the Overview page: one <see cref="DistroRow"/>
/// per listed distro, or the same empty-state message in both places when WSL or distros are missing.
/// </summary>
public static class DistroList
{
    /// <summary>Replace <paramref name="panel"/>'s children with the current list.</summary>
    /// <param name="onOpen">Runs when a row (not its power button) is clicked, with that row's distro.</param>
    public static void Fill(Panel panel, WslMonitorService service, Action<Distro> onOpen)
    {
        panel.Children.Clear();

        if (!service.IsWslInstalled)
        {
            panel.Children.Add(EmptyMessage(UiText.Get("list.no-wsl")));
            return;
        }

        var distros = service.Distros;
        if (distros.Count == 0)
        {
            panel.Children.Add(EmptyMessage(UiText.Get("list.no-distros")));
            return;
        }

        foreach (var d in distros)
            panel.Children.Add(DistroRow.Build(d, service, () => onOpen(d)));
    }

    /// <summary>Secondary-coloured explanation in place of the rows, aligned with row content.</summary>
    private static TextBlock EmptyMessage(string text) => new()
    {
        Text = text,
        Margin = new Thickness(10, 6, 10, 8),
        TextWrapping = TextWrapping.Wrap,
        Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
    };
}
