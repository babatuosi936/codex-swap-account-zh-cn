using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CodexProfileOverlay;

internal sealed partial class OverlayWindow
{
    private void MinimizeToBall()
    {
        if (isBallMinimized || isSwitching) return;
        FinishDrag();
        ballOffset = ownerHwnd != System.IntPtr.Zero && TryGetClientBounds(ownerHwnd, out Rect bounds)
            ? new Point(Left - bounds.Left, Top - bounds.Top)
            : new Point(settings.OffsetX, settings.OffsetY);
        isBallMinimized = true;
        RebuildContent();
        UpdateLayout();
        if (ownerHwnd != System.IntPtr.Zero) UpdatePlacement(ownerHwnd);
    }

    private void RestoreFromBall()
    {
        if (!isBallMinimized) return;
        FinishDrag();
        isBallMinimized = false;
        RebuildContent();
        UpdateLayout();
        if (ownerHwnd != System.IntPtr.Zero) UpdatePlacement(ownerHwnd);
    }

    private void BuildFloatingBall()
    {
        profileButtons.Clear();
        shell.Height = shell.MinHeight = 32;
        shell.Padding = new Thickness(0);
        shell.CornerRadius = new CornerRadius(16);
        shell.Background = new SolidColorBrush(Color.FromRgb(124, 58, 237));
        shell.BorderBrush = FindBrush("OverlayBorderBrush");
        shell.BorderThickness = new Thickness(1);
        shell.Child = new TextBlock
        {
            Text = "◈",
            FontSize = 22,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = Cursors.Hand,
            ToolTip = Localizer?["RestoreFromBall"] ?? "Click to restore; drag to move",
        };
        UpdateWidth();
    }
}
