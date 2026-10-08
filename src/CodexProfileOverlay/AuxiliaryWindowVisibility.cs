using System.Windows;
using System.Windows.Threading;

namespace CodexProfileOverlay;

internal static class AuxiliaryWindowVisibility
{
    public static void HideWhenMinimized(Window window)
    {
        bool closed = false;
        window.Closed += (_, _) => closed = true;
        // Owned windows without taskbar entries otherwise minimize to a small
        // desktop caption. Keep the instance and its controls for reopening.
        window.StateChanged += (_, _) =>
        {
            // Hide after WPF finishes applying the native minimize command.
            window.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
            {
                if (!closed && window.WindowState == WindowState.Minimized)
                {
                    window.Hide();
                }
            }));
        };
    }
}
