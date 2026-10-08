namespace CodexProfileOverlay.Core.Services;

public static class ScreenshotWindowPolicy
{
    // QQ NT ships this dedicated capture executable. Do not exempt all QQ/TIM
    // windows: switching to a chat must still hide the Codex overlay.
    public static bool IsDedicatedCaptureProcess(string? processName) =>
        string.Equals(processName, "QQScreenshot", StringComparison.OrdinalIgnoreCase);
}
