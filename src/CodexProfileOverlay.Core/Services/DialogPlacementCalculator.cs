namespace CodexProfileOverlay.Core.Services;

// All rectangles use physical screen pixels, including on monitors left of the primary display.
public readonly record struct DialogRectangle(int Left, int Top, int Width, int Height);

public static class DialogPlacementCalculator
{
    public static DialogRectangle CenterAndClamp(DialogRectangle workArea, DialogRectangle? owner, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(workArea.Width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(workArea.Height);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        width = Math.Min(width, workArea.Width);
        height = Math.Min(height, workArea.Height);
        DialogRectangle anchor = owner is { Width: > 0, Height: > 0 } rectangle ? rectangle : workArea;
        int left = Math.Clamp(anchor.Left + (anchor.Width - width) / 2, workArea.Left, workArea.Left + workArea.Width - width);
        int top = Math.Clamp(anchor.Top + (anchor.Height - height) / 2, workArea.Top, workArea.Top + workArea.Height - height);
        return new DialogRectangle(left, top, width, height);
    }
}
