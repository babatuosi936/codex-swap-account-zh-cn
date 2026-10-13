namespace CodexProfileOverlay.Core.Services;

// All rectangles use physical screen pixels, including on monitors left of the primary display.
public readonly record struct DialogRectangle(int Left, int Top, int Width, int Height);

public static class DialogPlacementCalculator
{
    public static DialogRectangle BelowAndClamp(DialogRectangle workArea, DialogRectangle anchor, int width, int height, int gap)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(workArea.Width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(workArea.Height);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNegative(gap);
        if (anchor.Width <= 0 || anchor.Height <= 0)
        {
            return CenterAndClamp(workArea, null, width, height);
        }

        width = Math.Min(width, workArea.Width);
        int left = Math.Clamp(anchor.Left + (anchor.Width - width) / 2, workArea.Left, workArea.Left + workArea.Width - width);
        int belowTop = Math.Clamp(anchor.Top + anchor.Height + gap, workArea.Top, workArea.Top + workArea.Height);
        int aboveBottom = Math.Clamp(anchor.Top - gap, workArea.Top, workArea.Top + workArea.Height);
        int belowSpace = workArea.Top + workArea.Height - belowTop;
        int aboveSpace = aboveBottom - workArea.Top;
        if (belowSpace >= height)
        {
            return new DialogRectangle(left, belowTop, width, height);
        }
        if (aboveSpace >= height)
        {
            return new DialogRectangle(left, aboveBottom - height, width, height);
        }
        if (belowSpace > 0 && belowSpace >= aboveSpace)
        {
            return new DialogRectangle(left, belowTop, width, belowSpace);
        }
        if (aboveSpace > 0)
        {
            return new DialogRectangle(left, workArea.Top, width, aboveSpace);
        }

        // A full-screen anchor leaves no adjacent visible area.
        return CenterAndClamp(workArea, null, width, height);
    }

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
