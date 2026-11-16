using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WslMonitor.Pages;

/// <summary>
/// A page that keeps itself current while it is shown: it subscribes to the service and to language
/// changes in <see cref="OnAttach"/> and drops them in <see cref="OnDetach"/>. Detaching happens on
/// Unloaded and, because WinUI doesn't reliably raise Unloaded when a window closes, also when the
/// details window closes (<see cref="Detach"/>). Without that, closed pages would stay subscribed to
/// the long-lived service and keep rebuilding.
/// </summary>
public partial class LivePage : Page
{
    private bool _attached;

    public LivePage()
    {
        Loaded += (_, _) => Attach();
        Unloaded += (_, _) => Detach();
    }

    /// <summary>Subscribe and draw the current state. Called once per showing.</summary>
    protected virtual void OnAttach() { }

    /// <summary>Unsubscribe everything <see cref="OnAttach"/> subscribed.</summary>
    protected virtual void OnDetach() { }

    /// <summary>Drop subscriptions now (safe to call more than once).</summary>
    public void Detach()
    {
        if (!_attached)
        {
            return;
        }

        _attached = false;
        OnDetach();
    }

    private void Attach()
    {
        if (_attached)
        {
            return;
        }

        _attached = true;
        OnAttach();
    }
}
