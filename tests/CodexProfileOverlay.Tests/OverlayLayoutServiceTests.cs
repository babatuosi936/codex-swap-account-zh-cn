using CodexProfileOverlay.Core.Models;
using CodexProfileOverlay.Core.Services;

namespace CodexProfileOverlay.Tests;

public sealed class OverlayLayoutServiceTests
{
    [Theory]
    [InlineData(1400, 340, 56)]
    [InlineData(1800, 340, 0)]
    public void MenuRow_PreservesAnchorAndMovesBelowButtonsOnlyWhenNecessary(double width, double x, double y)
    {
        var caption = new OverlayCaptionBounds(width - 160, 0, 160, 48);
        var placement = new OverlayLayoutService().CalculatePlacement(PositionPreset.AfterMenu, width, 800, 950, 44, 340, 0, caption);
        Assert.Equal(new OverlayPlacement(x, y), placement);
        Assert.False(OverlayLayoutService.OverlapsCaption(placement, 950, 44, caption));
    }

    [Theory]
    [InlineData(350, 1100, 0, 682, 0)]
    [InlineData(32, 1180, 0, 1000, 0)]
    [InlineData(1100, 100, 0, 100, 56)]
    [InlineData(350, 1100, 80, 850, 80)]
    public void CustomPlacement_ProtectsButtonsForBarsAndFloatingBalls(double width, double x, double y, double expectedX, double expectedY)
    {
        var caption = new OverlayCaptionBounds(1040, 0, 160, 48);
        var placement = new OverlayLayoutService().CalculatePlacement(PositionPreset.Custom, 1200, 800, width, 44, x, y, caption);
        Assert.Equal(new OverlayPlacement(expectedX, expectedY), placement);
        Assert.False(OverlayLayoutService.OverlapsCaption(placement, width, 44, caption));
    }

    [Fact]
    public void LeftHandCaption_MovesBarToItsRight()
    {
        var caption = new OverlayCaptionBounds(0, 0, 140, 48);
        var placement = new OverlayLayoutService().CalculatePlacement(PositionPreset.Custom, 1200, 800, 350, 44, 0, 0, caption);
        Assert.Equal(new OverlayPlacement(148, 0), placement);
    }

    [Fact]
    public void AutoLayout_UsesTheSpaceBetweenMenuAndCaptionControls()
    {
        var service = new OverlayLayoutService();
        double available = service.AvailableDisplayWidth(PositionPreset.AfterMenu, 1400, 800, 44, 340, 0, new(1240, 0, 160, 48));
        Assert.Equal(892, available);
        Assert.Equal(OverlayDisplayMode.Compact, service.ResolveDisplayMode(OverlayDisplayMode.Auto, available, OverlayDisplayMode.Expanded, 950));
        Assert.Equal(1400, service.AvailableDisplayWidth(PositionPreset.TopCenter, 1400, 800, 44, 340, 0, new(1240, 0, 160, 48)));
        Assert.Equal(1400, service.AvailableDisplayWidth(PositionPreset.AfterMenu, 1400, 800, 44, 340, 80, new(1240, 0, 160, 48)));
    }

    [Fact]
    public void InvalidCaptionBounds_AreIgnored()
    {
        var service = new OverlayLayoutService();
        Assert.Equal(new OverlayPlacement(850, 0), service.CalculatePlacement(PositionPreset.Custom, 1200, 800, 350, 44, 1100, 0, new(double.NaN, 0, 160, 48)));
        Assert.Equal(new OverlayPlacement(850, 0), service.CalculatePlacement(PositionPreset.Custom, 1200, 800, 350, 44, 1100, 0, new(1040, 0, 0, 48)));
    }

    [Fact]
    public void NoSafeSpace_RemainsDetectableSoTheWindowCanBeHidden()
    {
        var caption = new OverlayCaptionBounds(0, 0, 120, 40);
        var placement = new OverlayLayoutService().CalculatePlacement(PositionPreset.AfterMenu, 120, 40, 120, 40, 340, 0, caption);
        Assert.True(OverlayLayoutService.OverlapsCaption(placement, 120, 40, caption));
    }

    [Fact]
    public void ResolveDisplayMode_RequiresTheCompleteScaledAccountRowToFit()
    {
        var service = new OverlayLayoutService();
        Assert.Equal(OverlayDisplayMode.Compact, service.ResolveDisplayMode(OverlayDisplayMode.Auto, 900, OverlayDisplayMode.Expanded, 950));
        Assert.Equal(OverlayDisplayMode.Expanded, service.ResolveDisplayMode(OverlayDisplayMode.Auto, 950, OverlayDisplayMode.Expanded, 950));
        Assert.Equal(OverlayDisplayMode.Compact, service.ResolveDisplayMode(OverlayDisplayMode.Auto, 1000, OverlayDisplayMode.Compact, 950));
        Assert.Equal(OverlayDisplayMode.Expanded, service.ResolveDisplayMode(OverlayDisplayMode.Auto, 1040, OverlayDisplayMode.Compact, 950));
        Assert.Equal(OverlayDisplayMode.Expanded, service.ResolveDisplayMode(OverlayDisplayMode.Expanded, 600, OverlayDisplayMode.Expanded, 950));
    }

    [Fact]
    public void ResolveDisplayMode_UsesHysteresis()
    {
        var service = new OverlayLayoutService();

        Assert.Equal(OverlayDisplayMode.Expanded, service.ResolveDisplayMode(OverlayDisplayMode.Auto, 800, OverlayDisplayMode.Expanded));
        Assert.Equal(OverlayDisplayMode.Compact, service.ResolveDisplayMode(OverlayDisplayMode.Auto, 740, OverlayDisplayMode.Expanded));
        Assert.Equal(OverlayDisplayMode.Compact, service.ResolveDisplayMode(OverlayDisplayMode.Auto, 820, OverlayDisplayMode.Compact));
        Assert.Equal(OverlayDisplayMode.Expanded, service.ResolveDisplayMode(OverlayDisplayMode.Auto, 860, OverlayDisplayMode.Compact));
    }

    [Fact]
    public void CalculatePlacement_ClampsCustomPositionInsideClientArea()
    {
        var service = new OverlayLayoutService();

        OverlayPlacement placement = service.CalculatePlacement(PositionPreset.Custom, 300, 120, 200, 50, 999, -20);

        Assert.Equal(100, placement.OffsetX);
        Assert.Equal(0, placement.OffsetY);
    }

    [Theory]
    [InlineData(PositionPreset.TopLeft, 14)]
    [InlineData(PositionPreset.TopCenter, 340)]
    [InlineData(PositionPreset.TopRight, 666)]
    public void CalculatePlacement_BottomPresetsFollowWindowEdges(PositionPreset preset, double expectedX)
    {
        var service = new OverlayLayoutService();

        OverlayPlacement placement = service.CalculatePlacement(preset, 1200, 800, 520, 44, 999, 999);

        Assert.Equal(expectedX, placement.OffsetX);
        Assert.Equal(742, placement.OffsetY);
    }

    [Theory]
    [InlineData(PositionPreset.TopLeft)]
    [InlineData(PositionPreset.TopCenter)]
    [InlineData(PositionPreset.TopRight)]
    public void CalculatePlacement_BottomPresetsStayInsideSmallWindows(PositionPreset preset)
    {
        var service = new OverlayLayoutService();
        OverlayPlacement placement = service.CalculatePlacement(preset, 420, 120, 620, 44, 999, 999);
        Assert.Equal(0, placement.OffsetX);
        Assert.Equal(62, placement.OffsetY);

        OverlayPlacement shortWindow = service.CalculatePlacement(preset, 420, 30, 620, 44, 999, 999);
        Assert.Equal(0, shortWindow.OffsetY);
    }

    [Fact]
    public void CalculatePlacement_AfterMenuUsesClientRelativeMenuRow()
    {
        var service = new OverlayLayoutService();

        OverlayPlacement placement = service.CalculatePlacement(PositionPreset.AfterMenu, 1200, 800, 560, 50, 396, 2);

        Assert.Equal(396, placement.OffsetX);
        Assert.Equal(2, placement.OffsetY);
    }

    [Fact]
    public void CalculatePlacement_AfterMenuClampsInsideClientArea()
    {
        var service = new OverlayLayoutService();

        OverlayPlacement placement = service.CalculatePlacement(PositionPreset.AfterMenu, 420, 120, 286, 50, 396, 2);

        Assert.Equal(134, placement.OffsetX);
        Assert.Equal(2, placement.OffsetY);
    }

    [Fact]
    public void CalculatePlacement_ReplacesNonFiniteOffsetsWithSafeValues()
    {
        var service = new OverlayLayoutService();

        OverlayPlacement placement = service.CalculatePlacement(PositionPreset.Custom, 420, 120, 286, 50, double.NaN, double.PositiveInfinity);

        Assert.Equal(0, placement.OffsetX);
        Assert.Equal(0, placement.OffsetY);
    }
}
