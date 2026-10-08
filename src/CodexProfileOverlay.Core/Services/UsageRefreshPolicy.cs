using CodexProfileOverlay.Core.Models;

namespace CodexProfileOverlay.Core.Services;

public static class UsageRefreshPolicy
{
    public static bool AllowsAutomaticRefresh(OverlaySettings settings, UsageProviderCapability capability)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.ShowAutomaticLimitIndicators && capability == UsageProviderCapability.Supported;
    }
}
