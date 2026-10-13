using CodexProfileOverlay.Core.Services;

namespace CodexProfileOverlay.Tests;

public sealed class DialogAnchorPlacementTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void OpensBelowAndCentersOnAccountBarAtDifferentScales(int scale)
    {
        var workArea = new DialogRectangle(0, 0, 1920 * scale, 1040 * scale);
        var accountBar = new DialogRectangle(100 * scale, 0, 720 * scale, 44 * scale);

        var actual = DialogPlacementCalculator.BelowAndClamp(workArea, accountBar, 540 * scale, 258 * scale, 4 * scale);

        Assert.Equal(new DialogRectangle(190 * scale, 48 * scale, 540 * scale, 258 * scale), actual);
        Assert.True(actual.Top >= accountBar.Top + accountBar.Height + 4 * scale);
    }

    [Fact]
    public void BelowPlacementSupportsNegativeMonitorOrigins()
    {
        var workArea = new DialogRectangle(-1920, -1080, 1920, 1040);
        var accountBar = new DialogRectangle(-1800, -1080, 800, 44);

        var actual = DialogPlacementCalculator.BelowAndClamp(workArea, accountBar, 540, 258, 4);

        Assert.Equal(new DialogRectangle(-1670, -1032, 540, 258), actual);
    }

    [Theory]
    [InlineData(-80, 0)]
    [InlineData(1840, 1380)]
    public void HorizontalClampingDoesNotMoveDialogOverAccountBar(int accountBarLeft, int expectedLeft)
    {
        var workArea = new DialogRectangle(0, 0, 1920, 1040);
        var accountBar = new DialogRectangle(accountBarLeft, 100, 200, 44);

        var actual = DialogPlacementCalculator.BelowAndClamp(workArea, accountBar, 540, 258, 4);

        Assert.Equal(new DialogRectangle(expectedLeft, 148, 540, 258), actual);
    }

    [Fact]
    public void NearBottomUsesAbovePlacementWithoutOverlappingAccountBar()
    {
        var workArea = new DialogRectangle(0, 0, 1920, 1040);
        var accountBar = new DialogRectangle(600, 950, 720, 44);

        var actual = DialogPlacementCalculator.BelowAndClamp(workArea, accountBar, 540, 258, 4);

        Assert.Equal(new DialogRectangle(690, 688, 540, 258), actual);
        Assert.Equal(accountBar.Top - 4, actual.Top + actual.Height);
    }

    [Fact]
    public void OversizeDialogUsesLargerSpaceBelowWithoutOverlappingAccountBar()
    {
        var workArea = new DialogRectangle(0, 0, 1000, 500);
        var accountBar = new DialogRectangle(200, 140, 600, 44);

        var actual = DialogPlacementCalculator.BelowAndClamp(workArea, accountBar, 540, 600, 4);

        Assert.Equal(new DialogRectangle(230, 188, 540, 312), actual);
        Assert.Equal(workArea.Top + workArea.Height, actual.Top + actual.Height);
    }

    [Fact]
    public void OversizeDialogUsesLargerSpaceAboveWithoutOverlappingAccountBar()
    {
        var workArea = new DialogRectangle(0, 0, 1000, 500);
        var accountBar = new DialogRectangle(200, 340, 600, 44);

        var actual = DialogPlacementCalculator.BelowAndClamp(workArea, accountBar, 540, 600, 4);

        Assert.Equal(new DialogRectangle(230, 0, 540, 336), actual);
        Assert.Equal(accountBar.Top - 4, actual.Top + actual.Height);
    }

    [Fact]
    public void OversizeWidthIsContainedWhileKeepingBelowPosition()
    {
        var actual = DialogPlacementCalculator.BelowAndClamp(
            new DialogRectangle(0, 0, 1000, 600),
            new DialogRectangle(300, 10, 400, 44),
            1400, 258, 4);

        Assert.Equal(new DialogRectangle(0, 58, 1000, 258), actual);
    }
}
