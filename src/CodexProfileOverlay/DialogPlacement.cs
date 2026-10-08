using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
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

        _ = new WindowInteropHelper(dialog).EnsureHandle();
        Forms.Screen screen = ownerHandle != IntPtr.Zero
            ? Forms.Screen.FromHandle(ownerHandle)
            : Forms.Screen.FromPoint(Forms.Cursor.Position);

        Rect workArea = DeviceToDip(dialog, screen.WorkingArea);
        dialog.Left = workArea.Left + Math.Max(0, (workArea.Width - dialog.Width) / 2);
        dialog.Top = workArea.Top + Math.Max(0, (workArea.Height - dialog.Height) / 2);
    }

    private static Rect DeviceToDip(Window window, System.Drawing.Rectangle rectangle)
    {
        HwndSource? source = (HwndSource?)PresentationSource.FromVisual(window);
        if (source?.CompositionTarget is null)
        {
            return new Rect(rectangle.Left, rectangle.Top, rectangle.Width, rectangle.Height);
        }

        Matrix transform = source.CompositionTarget.TransformFromDevice;
        Point topLeft = transform.Transform(new Point(rectangle.Left, rectangle.Top));
        Point bottomRight = transform.Transform(new Point(rectangle.Right, rectangle.Bottom));
        return new Rect(topLeft, bottomRight);
    }
}
