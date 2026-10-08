using CodexProfileOverlay.Core.Services;

namespace CodexProfileOverlay.Tests;

public sealed class DialogPlacementCalculatorTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void CentersInOwnerUsingPhysicalPixelsAtDifferentScales(int scale)
    {
        var work = new DialogRectangle(0, 0, 1920 * scale, 1040 * scale);
        var owner = new DialogRectangle(100 * scale, 80 * scale, 1200 * scale, 800 * scale);
        var actual = DialogPlacementCalculator.CenterAndClamp(work, owner, 500 * scale, 246 * scale);
        Assert.Equal(new DialogRectangle(450 * scale, 357 * scale, 500 * scale, 246 * scale), actual);
    }

    [Fact]
    public void PartiallyOffscreenOwnerCannotPutButtonsBehindTaskbarOrRightEdge()
    {
        var work = new DialogRectangle(0, 0, 1920, 1040);
        var actual = DialogPlacementCalculator.CenterAndClamp(work, new(1700, 900, 800, 600), 1000, 492);
        Assert.Equal(new DialogRectangle(920, 548, 1000, 492), actual);
    }

    [Fact]
    public void SupportsMonitorToLeftAndAbovePrimaryScreen()
    {
        var actual = DialogPlacementCalculator.CenterAndClamp(new(-1920, -1080, 1920, 1040), new(-1800, -1000, 1200, 800), 500, 246);
        Assert.Equal(new DialogRectangle(-1450, -723, 500, 246), actual);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingOrInvalidOwnerFallsBackToWorkAreaCenter(bool invalid)
    {
        var actual = DialogPlacementCalculator.CenterAndClamp(new(1920, 40, 1920, 1040), invalid ? new DialogRectangle(0, 0, 0, 0) : null, 500, 246);
        Assert.Equal(new DialogRectangle(2630, 437, 500, 246), actual);
    }

    [Fact]
    public void OversizeDialogIsLimitedToVisibleWorkArea()
    {
        var actual = DialogPlacementCalculator.CenterAndClamp(new(-800, 0, 800, 560), null, 1000, 600);
        Assert.Equal(new DialogRectangle(-800, 0, 800, 560), actual);
    }
}
