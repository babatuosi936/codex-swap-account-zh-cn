namespace CodexProfileOverlay.Core.Services;

public sealed class OverlayVisibilityState
{
    private bool foregroundWasEligible;

    // Keep a visible overlay throughout a recognized capture session, without
    // changing visibility for ordinary application switches or hidden overlays.
    public bool ResolveForegroundVisibility(bool foregroundEligible, bool screenshotActive = false)
    {
        if (!ShouldShowOverlay)
        {
            ResetForegroundVisibility();
            return false;
        }
        if (screenshotActive)
        {
            return foregroundWasEligible;
        }
        if (foregroundEligible)
        {
            foregroundWasEligible = true;
            return true;
        }
        ResetForegroundVisibility();
        return false;
    }

    private void ResetForegroundVisibility()
    {
        foregroundWasEligible = false;
    }

    public bool CodexAvailable { get; private set; }

    public bool AutomaticDisplayEnabled { get; set; } = true;

    public bool UserManuallyHidOverlay { get; private set; }

    public bool TemporarilyHiddenBecauseCodexMinimized { get; private set; }

    public bool ShouldShowOverlay => CodexAvailable
        && AutomaticDisplayEnabled
        && !UserManuallyHidOverlay
        && !TemporarilyHiddenBecauseCodexMinimized;

    public void MarkCodexUnavailable()
    {
        CodexAvailable = false;
        TemporarilyHiddenBecauseCodexMinimized = false;
        ResetForegroundVisibility();
    }

    public void MarkCodexAvailable(bool isMinimized)
    {
        CodexAvailable = true;
        TemporarilyHiddenBecauseCodexMinimized = isMinimized;
        if (isMinimized)
        {
            ResetForegroundVisibility();
        }
    }

    public void MarkManualHide()
    {
        UserManuallyHidOverlay = true;
        ResetForegroundVisibility();
    }

    public void RevealManually()
    {
        UserManuallyHidOverlay = false;
    }
}
