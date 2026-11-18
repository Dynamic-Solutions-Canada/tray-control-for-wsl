using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace WslMonitor.Controls;

/// <summary>
/// The red "armed" look of a <see cref="ClickConfirmation"/> button (Stop?, Force stop?, Shut down
/// WSL?): white text on a red that keeps at least 5.6:1 contrast with it in every state, in both themes,
/// inside a lighter 1px edge that keeps 3:1 against the dark and light backgrounds.
/// <para>
/// The red is drawn by the button's content, a panel that fills the button (whose own padding and
/// border are set to zero), and that panel darkens on hover and press itself. Setting the button's
/// own <c>Background</c> does not work: WinUI's pointer-over state replaces it, and right after the
/// first click the pointer is still on the button, so the red never showed. Changing the template's
/// lightweight-styling resources after the button has loaded does not reach its visual states either.
/// </para>
/// <para>
/// With a high-contrast theme on, the armed text is shown as plain content in the system's colours.
/// </para>
/// </summary>
internal static class ConfirmButtonLook
{
    // Windows' light-theme critical red, a darker hover and a darker press. With white text: 5.7:1,
    // 6.3:1 and 7.4:1. The dark theme's own critical red (#FF99A4) gives white text only 2:1.
    private static readonly SolidColorBrush Rest = Brush(0xC4, 0x2B, 0x1C);
    private static readonly SolidColorBrush Hover = Brush(0xB5, 0x2A, 0x1C);
    private static readonly SolidColorBrush Pressed = Brush(0xA3, 0x26, 0x1A);

    // A lighter red for the 1px edge, so the button's outline keeps 3:1 against what is around it:
    // 3.8:1 on a dark card, 4.4:1 on the dark flyout, 3.6:1 on a light card and 3.7:1 on white.
    private static readonly SolidColorBrush Edge = Brush(0xE5, 0x53, 0x4B);

    private static readonly AccessibilitySettings Accessibility = new();

    /// <summary>
    /// Show <paramref name="button"/> armed: <paramref name="text"/> in white on red. The button's
    /// accessible name becomes the text. Call <see cref="Clear"/> before setting ordinary content.
    /// </summary>
    public static void ShowArmed(Button button, string text)
    {
        AutomationProperties.SetName(button, text);
        if (Accessibility.HighContrast)
        {
            Clear(button);
            button.Content = text;
            return;
        }

        // The panel takes the button's whole face: no border of the button's own around it.
        button.BorderThickness = new Thickness(0);
        button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        button.VerticalContentAlignment = VerticalAlignment.Stretch;

        var panel = new Border
        {
            Background = Rest,
            BorderBrush = Edge,
            BorderThickness = new Thickness(1),
            Child = new TextBlock
            {
                Text = text,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        ShadeOnPointer(panel);
        button.Content = panel;

        // A button rebuilt while its confirmation is pending (the lists are rebuilt on every change)
        // is armed before it is in the tree, when its style's padding and corners are not applied yet.
        if (button.IsLoaded)
        {
            FitToButton(button, panel);
        }
        else
        {
            void OnLoaded(object sender, RoutedEventArgs e)
            {
                button.Loaded -= OnLoaded;
                if (ReferenceEquals(button.Content, panel))
                {
                    FitToButton(button, panel);
                }
            }

            button.Loaded += OnLoaded;
        }
    }

    /// <summary>
    /// Give the panel the button's own padding (so the text sits where the plain label did) and
    /// corners, zero the button's padding so the panel fills it, and start in the hover shade if the
    /// pointer is already over the button, as it is right after the first click. Needs the button's
    /// style applied, so only once it has loaded.
    /// </summary>
    private static void FitToButton(Button button, Border panel)
    {
        button.ClearValue(Control.PaddingProperty);
        panel.Padding = button.Padding;
        panel.CornerRadius = button.CornerRadius;
        button.Padding = new Thickness(0);
        panel.Background = button.IsPointerOver ? Hover : Rest;
    }

    /// <summary>Put back the button's own padding, border and alignment after <see cref="ShowArmed"/>.</summary>
    public static void Clear(Button button)
    {
        button.ClearValue(Control.PaddingProperty);
        button.ClearValue(Control.BorderThicknessProperty);
        button.ClearValue(Control.HorizontalContentAlignmentProperty);
        button.ClearValue(Control.VerticalContentAlignmentProperty);
    }

    /// <summary>
    /// Darken the panel while the pointer is over it and more while pressed, as the button itself
    /// would. Press events are observed even though the button handles them.
    /// </summary>
    private static void ShadeOnPointer(Border panel)
    {
        panel.PointerEntered += (_, _) => panel.Background = Hover;
        panel.PointerExited += (_, _) => panel.Background = Rest;
        panel.PointerCanceled += (_, _) => panel.Background = Rest;
        panel.PointerCaptureLost += (_, _) => panel.Background = Rest;
        panel.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler((_, _) => panel.Background = Pressed), true);
        panel.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler((_, _) => panel.Background = Hover), true);
    }

    private static SolidColorBrush Brush(byte r, byte g, byte b) => new(Color.FromArgb(0xFF, r, g, b));
}
