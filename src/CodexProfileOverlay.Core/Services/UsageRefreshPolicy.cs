using CodexProfileOverlay.Core.Models;

namespace CodexProfileOverlay.Core.Services;

public static class UsageRefreshPolicy
{
    public static bool AllowsAutomaticRefresh(OverlaySettings settings, UsageProviderCapability capability)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.ShowAutomaticLimitIndicators && capability == UsageProviderCapability.Supported;
    }

    public static TimeSpan Interval(OverlaySettings settings, bool active, bool lastAttemptFailed) =>
        TimeSpan.FromSeconds(Math.Max(lastAttemptFailed ? 60 : 0,
            active ? Math.Clamp(settings.ActiveProfileRefreshIntervalSeconds, 15, 3600)
                : Math.Clamp(settings.InactiveProfileRefreshIntervalSeconds, 30, 86400)));

    public static string QueryDirectory(string profileId, string? activeProfileId, string savedProfileDirectory, string sharedDirectory) =>
        string.Equals(profileId, activeProfileId, StringComparison.OrdinalIgnoreCase) ? sharedDirectory : savedProfileDirectory;
}
