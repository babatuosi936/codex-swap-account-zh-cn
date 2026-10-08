using CodexProfileOverlay.Core.Services;

namespace CodexProfileOverlay.Tests;

public sealed class OverlayVisibilityStateTests
{
    [Fact]
    public void ScreenshotFocusInterruptionKeepsVisibleOverlayAndRecoveryResetsGrace()
    {
        var state = new OverlayVisibilityState();
        state.MarkCodexAvailable(false);
        Assert.True(state.ResolveForegroundVisibility(true, 1000));
        Assert.True(state.ResolveForegroundVisibility(false, 1750));
        Assert.True(state.ResolveForegroundVisibility(false, 3750));
        Assert.True(state.ResolveForegroundVisibility(true, 4000));
        Assert.True(state.ResolveForegroundVisibility(false, 4750));
        Assert.True(state.ResolveForegroundVisibility(false, 6750));
        Assert.False(state.ResolveForegroundVisibility(false, 7750));
        Assert.False(state.ResolveForegroundVisibility(false, 8500));
        Assert.True(state.ResolveForegroundVisibility(true, 9000));
    }

    [Fact]
    public void BackgroundStartupDoesNotRevealOverlayDuringGrace()
    {
        var state = new OverlayVisibilityState();
        state.MarkCodexAvailable(false);
        Assert.False(state.ResolveForegroundVisibility(false, 1000));
        Assert.False(state.ResolveForegroundVisibility(false, 2000));
    }

    [Theory]
    [InlineData("manual")]
    [InlineData("minimized")]
    [InlineData("closed")]
    [InlineData("disabled")]
    public void ExplicitHideAndUnavailableCodexOverrideFocusGrace(string reason)
    {
        var state = new OverlayVisibilityState();
        state.MarkCodexAvailable(false);
        Assert.True(state.ResolveForegroundVisibility(true, 1000));
        Assert.True(state.ResolveForegroundVisibility(false, 1750));
        switch (reason)
        {
            case "manual": state.MarkManualHide(); break;
            case "minimized": state.MarkCodexAvailable(true); break;
            case "closed": state.MarkCodexUnavailable(); break;
            case "disabled": state.AutomaticDisplayEnabled = false; break;
        }
        Assert.False(state.ResolveForegroundVisibility(false, 2000));
        Assert.False(state.ResolveForegroundVisibility(true, 2100));
        state.RevealManually();
        state.MarkCodexAvailable(false);
        state.AutomaticDisplayEnabled = true;
        Assert.False(state.ResolveForegroundVisibility(false, 2500));
        Assert.True(state.ResolveForegroundVisibility(true, 3000));
    }

    [Fact]
    public void ManualHideSurvivesAutomaticWindowUpdates()
    {
        var state = new OverlayVisibilityState();

        state.MarkManualHide();
        state.MarkCodexAvailable(isMinimized: false);

        Assert.False(state.ShouldShowOverlay);
    }

    [Fact]
    public void ManualRevealAllowsAutomaticShowWhenCodexAvailable()
    {
        var state = new OverlayVisibilityState();

        state.MarkManualHide();
        state.RevealManually();
        state.MarkCodexAvailable(isMinimized: false);

        Assert.True(state.ShouldShowOverlay);
    }

    [Fact]
    public void MinimizedCodexTemporarilyHidesAndRestoreShowsWhenAppropriate()
    {
        var state = new OverlayVisibilityState();

        state.MarkCodexAvailable(isMinimized: true);
        Assert.False(state.ShouldShowOverlay);

        state.MarkCodexAvailable(isMinimized: false);
        Assert.True(state.ShouldShowOverlay);
    }

    [Fact]
    public void CodexUnavailableHidesWithoutClearingManualState()
    {
        var state = new OverlayVisibilityState();

        state.MarkManualHide();
        state.MarkCodexUnavailable();
        state.MarkCodexAvailable(isMinimized: false);

        Assert.False(state.ShouldShowOverlay);
    }
}
