using CodexProfileOverlay.Core.Services;

namespace CodexProfileOverlay.Tests;

public sealed class OverlayVisibilityStateTests
{
    [Fact]
    public void CaptureSessionStaysVisibleUntilItEndsWithoutATimeLimit()
    {
        var state = new OverlayVisibilityState();
        state.MarkCodexAvailable(false);
        Assert.True(state.ResolveForegroundVisibility(true));
        // More than a minute of 200-ms tracking checks while the canvas stays open.
        for (int tick = 0; tick < 400; tick++)
            Assert.True(state.ResolveForegroundVisibility(false, screenshotActive: true));
        Assert.True(state.ResolveForegroundVisibility(true, screenshotActive: false));
        Assert.False(state.ResolveForegroundVisibility(false, screenshotActive: false));
        Assert.False(state.ResolveForegroundVisibility(false, screenshotActive: true));
        Assert.True(state.ResolveForegroundVisibility(true));
    }

    [Fact]
    public void ScreenshotDoesNotRevealAnOverlayStartedBehindAnotherApp()
    {
        var state = new OverlayVisibilityState();
        state.MarkCodexAvailable(false);
        Assert.False(state.ResolveForegroundVisibility(false));
        Assert.False(state.ResolveForegroundVisibility(false, screenshotActive: true));
        Assert.False(state.ResolveForegroundVisibility(true, screenshotActive: true));
    }

    [Theory]
    [InlineData("manual")]
    [InlineData("minimized")]
    [InlineData("closed")]
    [InlineData("disabled")]
    public void ExplicitHideAndUnavailableCodexOverrideScreenshotSession(string reason)
    {
        var state = new OverlayVisibilityState();
        state.MarkCodexAvailable(false);
        Assert.True(state.ResolveForegroundVisibility(true));
        Assert.True(state.ResolveForegroundVisibility(false, screenshotActive: true));
        switch (reason)
        {
            case "manual": state.MarkManualHide(); break;
            case "minimized": state.MarkCodexAvailable(true); break;
            case "closed": state.MarkCodexUnavailable(); break;
            case "disabled": state.AutomaticDisplayEnabled = false; break;
        }
        Assert.False(state.ResolveForegroundVisibility(false, screenshotActive: true));
        state.RevealManually();
        state.MarkCodexAvailable(false);
        state.AutomaticDisplayEnabled = true;
        Assert.False(state.ResolveForegroundVisibility(false, screenshotActive: true));
        Assert.True(state.ResolveForegroundVisibility(true));
    }

    [Theory]
    [InlineData("QQScreenshot", true)]
    [InlineData("qqscreenshot", true)]
    [InlineData("QQ", false)]
    [InlineData("TIM", false)]
    [InlineData("QQScreenshotHelper", false)]
    [InlineData(null, false)]
    public void ScreenshotPolicyOnlyExemptsTheDedicatedCaptureProcess(string? name, bool expected)
    {
        Assert.Equal(expected, ScreenshotWindowPolicy.IsDedicatedCaptureProcess(name));
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
