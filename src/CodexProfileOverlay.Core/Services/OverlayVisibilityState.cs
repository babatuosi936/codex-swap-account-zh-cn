namespace CodexProfileOverlay.Core.Services;

public sealed class OverlayVisibilityState
{
    private const long FocusLossGraceMilliseconds = 3000;
    private long? focusLostAt;
    private bool foregroundWasEligible;

    // A capture overlay can temporarily become foreground and cover Codex.
    // Preserve an already-visible overlay briefly, but never reveal a hidden
    // overlay just because Codex exists behind another application.
    public bool ResolveForegroundVisibility(bool foregroundEligible, long monotonicMilliseconds)
    {
        if (!ShouldShowOverlay)
        {
            ResetForegroundVisibility();
            return false;
        }
        if (foregroundEligible)
        {
            foregroundWasEligible = true;
            focusLostAt = null;
            return true;
        }
        if (!foregroundWasEligible)
        {
            return false;
        }
        focusLostAt ??= monotonicMilliseconds;
        long elapsed = monotonicMilliseconds - focusLostAt.Value;
        return elapsed >= 0 && elapsed < FocusLossGraceMilliseconds;
    }

    private void ResetForegroundVisibility()
    {
        foregroundWasEligible = false;
        focusLostAt = null;
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
