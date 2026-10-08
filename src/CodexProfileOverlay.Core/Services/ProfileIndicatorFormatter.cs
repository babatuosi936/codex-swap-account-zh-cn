using CodexProfileOverlay.Core.Models;

namespace CodexProfileOverlay.Core.Services;

public static class ProfileIndicatorFormatter
{
    public static string FormatAutomatic(
        UsageSnapshot? snapshot,
        bool enabled,
        int greenThreshold,
        int yellowThreshold,
        DateTimeOffset now,
        TimeSpan staleThreshold,
        bool recommended)
    {
        if (!enabled || snapshot is null || UsageIntelligence.IsStale(snapshot, now, staleThreshold))
        {
            return string.Empty;
        }

        int? effective = UsageIntelligence.EffectiveRemainingPercent(snapshot);
        if (effective is null && snapshot.IsExhausted != true)
        {
            return string.Empty;
        }

        if (recommended && snapshot.IsExhausted != true)
        {
            return "⭐";
        }

        if (snapshot.IsExhausted == true)
        {
            return "🔴";
        }

        if (effective >= greenThreshold)
        {
            return "🟢";
        }

        return effective >= yellowThreshold ? "🟡" : "🔴";
    }
}
