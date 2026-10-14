using CodexProfileOverlay.Core.Models;

namespace CodexProfileOverlay.Core.Services;

public sealed class OverlayLayoutService
{
    public const double CaptionSafetyGap = 8;
    private const double CompactThreshold = 760;
    private const double ExpandedThreshold = 840;
    private const double AfterMenuX = OverlaySettings.DefaultOffsetX;
    private const double AfterMenuY = OverlaySettings.DefaultOffsetY;

    public OverlayDisplayMode ResolveDisplayMode(OverlayDisplayMode configuredMode, double clientWidth, OverlayDisplayMode previousAutoMode, double expandedWidth = 0)
    {
        if (configuredMode != OverlayDisplayMode.Auto)
        {
            return configuredMode;
        }

        double requiredWidth = double.IsFinite(expandedWidth) ? Math.Max(0, expandedWidth) : 0;
        double compactThreshold = Math.Max(CompactThreshold, requiredWidth);
        double expandedThreshold = Math.Max(ExpandedThreshold, requiredWidth + 80);
        if (previousAutoMode == OverlayDisplayMode.Expanded && clientWidth < compactThreshold)
        {
            return OverlayDisplayMode.Compact;
        }

        if (previousAutoMode != OverlayDisplayMode.Expanded && clientWidth > expandedThreshold)
        {
            return OverlayDisplayMode.Expanded;
        }

        return previousAutoMode == OverlayDisplayMode.Expanded ? OverlayDisplayMode.Expanded : OverlayDisplayMode.Compact;
    }

    public OverlayPlacement CalculatePlacement(
        PositionPreset preset,
        double clientWidth,
        double clientHeight,
        double overlayWidth,
        double overlayHeight,
        double customOffsetX,
        double customOffsetY,
        OverlayCaptionBounds? captionButtons = null)
    {
        const double safeTop = 34;
        const double safeSide = 14;
        const double safeBottom = 14;
        double x = preset switch
        {
            PositionPreset.AfterMenu => customOffsetX > 0 ? customOffsetX : AfterMenuX,
            PositionPreset.TopLeft => safeSide,
            PositionPreset.TopCenter => (clientWidth - overlayWidth) / 2,
            PositionPreset.TopRight => clientWidth - overlayWidth - safeSide,
            PositionPreset.Custom => customOffsetX,
            _ => safeSide,
        };

        double y = preset switch
        {
            PositionPreset.AfterMenu => customOffsetY >= 0 ? customOffsetY : AfterMenuY,
            PositionPreset.TopLeft or PositionPreset.TopCenter or PositionPreset.TopRight => clientHeight - overlayHeight - safeBottom,
            PositionPreset.Custom => customOffsetY,
            _ => safeTop,
        };
        var placement = new OverlayPlacement(
            Clamp(x, 0, Math.Max(0, clientWidth - overlayWidth)),
            Clamp(y, 0, Math.Max(0, clientHeight - overlayHeight)));
        if (!OverlapsCaption(placement, overlayWidth, overlayHeight, captionButtons)) return placement;

        double left = Clamp(captionButtons!.Left - CaptionSafetyGap, 0, clientWidth);
        double right = Clamp(captionButtons.Left + captionButtons.Width + CaptionSafetyGap, 0, clientWidth);
        double top = Clamp(captionButtons.Top - CaptionSafetyGap, 0, clientHeight);
        double bottom = Clamp(captionButtons.Top + captionButtons.Height + CaptionSafetyGap, 0, clientHeight);
        // Preserve the menu anchor and complete expanded row instead of covering
        // the app menu when its right-hand slot is too narrow.
        if (preset == PositionPreset.AfterMenu && bottom + overlayHeight <= clientHeight)
            return placement with { OffsetY = bottom };
        if (overlayWidth <= left)
            return placement with { OffsetX = Math.Min(placement.OffsetX, left - overlayWidth) };
        if (overlayWidth <= clientWidth - right)
            return placement with { OffsetX = Math.Max(placement.OffsetX, right) };
        if (bottom + overlayHeight <= clientHeight)
            return placement with { OffsetY = bottom };
        if (overlayHeight <= top)
            return placement with { OffsetY = top - overlayHeight };
        return placement; // The caller hides the surface if no clear area exists.
    }

    public double AvailableDisplayWidth(PositionPreset preset, double clientWidth, double clientHeight,
        double overlayHeight, double offsetX, double offsetY, OverlayCaptionBounds? captionButtons)
    {
        var placement = CalculatePlacement(preset, clientWidth, clientHeight, 0, overlayHeight, offsetX, offsetY);
        if (captionButtons is not { IsValid: true }
            || placement.OffsetY >= captionButtons.Top + captionButtons.Height + CaptionSafetyGap
            || placement.OffsetY + overlayHeight <= captionButtons.Top - CaptionSafetyGap) return clientWidth;

        double left = Clamp(captionButtons.Left - CaptionSafetyGap, 0, clientWidth);
        double right = Clamp(captionButtons.Left + captionButtons.Width + CaptionSafetyGap, 0, clientWidth);
        if (captionButtons.Left >= clientWidth / 2)
            return Math.Max(0, left - (preset == PositionPreset.AfterMenu ? placement.OffsetX : 0));
        return Math.Max(0, clientWidth - Math.Max(right, preset == PositionPreset.AfterMenu ? placement.OffsetX : 0));
    }

    public static bool OverlapsCaption(OverlayPlacement placement, double width, double height, OverlayCaptionBounds? buttons)
        => buttons is { IsValid: true }
            // DIP/device conversion can round a boundary by a fraction of a pixel.
            && placement.OffsetX < buttons.Left + buttons.Width + CaptionSafetyGap - 0.01
            && placement.OffsetX + width > buttons.Left - CaptionSafetyGap + 0.01
            && placement.OffsetY < buttons.Top + buttons.Height + CaptionSafetyGap - 0.01
            && placement.OffsetY + height > buttons.Top - CaptionSafetyGap + 0.01;

    public static double Clamp(double value, double minimum, double maximum)
    {
        if (!double.IsFinite(value))
        {
            return minimum;
        }

        if (!double.IsFinite(minimum))
        {
            minimum = 0;
        }

        if (!double.IsFinite(maximum) || maximum < minimum)
        {
            maximum = minimum;
        }

        return Math.Min(Math.Max(value, minimum), maximum);
    }
}

public sealed record OverlayPlacement(double OffsetX, double OffsetY);

public sealed record OverlayCaptionBounds(double Left, double Top, double Width, double Height)
{
    public bool IsValid => double.IsFinite(Left) && double.IsFinite(Top)
        && double.IsFinite(Width) && double.IsFinite(Height) && Width > 0 && Height > 0;
}
