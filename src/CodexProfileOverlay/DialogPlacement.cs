using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using CodexProfileOverlay.Core.Services;
using Forms = System.Windows.Forms;

namespace CodexProfileOverlay;

internal static class DialogPlacement
{
    public static void CenterOnOwnerScreen(Window dialog, Window? owner, IntPtr nativeOwner)
    {
        IntPtr ownerHandle = nativeOwner;
        if (owner is not null)
        {
            ownerHandle = new WindowInteropHelper(owner).EnsureHandle();
        }

        dialog.WindowStartupLocation = WindowStartupLocation.Manual;
        IntPtr dialogHandle = new WindowInteropHelper(dialog).EnsureHandle();
        Place(dialog, dialogHandle, ownerHandle);
        // Recheck after WPF layout and a possible change to the owner's monitor DPI.
        RoutedEventHandler? loaded = null;
        loaded = (_, _) =>
        {
            dialog.Loaded -= loaded;
            Place(dialog, dialogHandle, ownerHandle);
        };
        dialog.Loaded += loaded;
    }

    private static void Place(Window dialog, IntPtr dialogHandle, IntPtr ownerHandle)
    {
        Forms.Screen screen = ownerHandle != IntPtr.Zero
            ? Forms.Screen.FromHandle(ownerHandle)
            : Forms.Screen.FromPoint(Forms.Cursor.Position);
        var workArea = new DialogRectangle(screen.WorkingArea.Left, screen.WorkingArea.Top, screen.WorkingArea.Width, screen.WorkingArea.Height);
        DialogRectangle? ownerBounds = null;
        if (ownerHandle != IntPtr.Zero && !NativeMethods.IsIconic(ownerHandle)
            && NativeMethods.GetWindowRect(ownerHandle, out NativeRect ownerRect))
        {
            ownerBounds = new DialogRectangle(ownerRect.Left, ownerRect.Top, ownerRect.Width, ownerRect.Height);
        }

        // FromVisual is unavailable before ShowDialog; the created HWND already has its DPI transform.
        HwndSource? source = HwndSource.FromHwnd(dialogHandle);
        Matrix toDevice = source?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
        Matrix fromDevice = source?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        int width = Math.Max(1, (int)Math.Ceiling(dialog.Width * toDevice.M11));
        int height = Math.Max(1, (int)Math.Ceiling(dialog.Height * toDevice.M22));
        if (NativeMethods.GetWindowRect(dialogHandle, out NativeRect dialogRect) && dialogRect.Width > 0 && dialogRect.Height > 0)
        {
            width = dialogRect.Width;
            height = dialogRect.Height;
        }
        DialogRectangle placement = DialogPlacementCalculator.CenterAndClamp(workArea, ownerBounds, width, height);
        Point location = fromDevice.Transform(new Point(placement.Left, placement.Top));
        dialog.Left = location.X;
        dialog.Top = location.Y;
        dialog.Width = placement.Width * fromDevice.M11;
        dialog.Height = placement.Height * fromDevice.M22;
        // Position in physical pixels too, avoiding mixed-monitor logical-origin rounding.
        const uint noZOrderOrActivation = 0x0004 | 0x0010;
        _ = NativeMethods.SetWindowPos(dialogHandle, IntPtr.Zero, placement.Left, placement.Top,
            placement.Width, placement.Height, noZOrderOrActivation);
    }
}
