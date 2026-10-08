using CodexProfileOverlay.Core.Models;
using CodexProfileOverlay.Core.Services;

namespace CodexProfileOverlay.Tests;

public sealed class OverlayLayoutServiceTests
{
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
