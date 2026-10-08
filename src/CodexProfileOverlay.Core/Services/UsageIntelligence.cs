using CodexProfileOverlay.Core.Models;

namespace CodexProfileOverlay.Core.Services;

public static class UsageIntelligence
{
    public static IReadOnlyList<UsageLimitWindow> GetKnownWindows(UsageSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var windows = snapshot.Windows
            .Where(window => window.RemainingPercent is >= 0 and <= 100)
            .ToList();

        if (windows.Count > 0)
        {
            return windows;
        }

        if (snapshot.ShortWindowRemainingPercent is >= 0 and <= 100)
        {
            windows.Add(new UsageLimitWindow
            {
                Name = "short",
                RemainingPercent = snapshot.ShortWindowRemainingPercent,
                ResetAt = snapshot.ShortWindowResetAt,
            });
        }

        if (snapshot.LongWindowRemainingPercent is >= 0 and <= 100)
        {
            windows.Add(new UsageLimitWindow
            {
                Name = "long",
                RemainingPercent = snapshot.LongWindowRemainingPercent,
                ResetAt = snapshot.LongWindowResetAt,
            });
        }

        return windows;
    }

    public static int? EffectiveRemainingPercent(UsageSnapshot snapshot)
    {
        int[] percentages = GetKnownWindows(snapshot)
            .Where(window => window.RemainingPercent.HasValue)
            .Select(window => window.RemainingPercent!.Value)
            .ToArray();
        return percentages.Length == 0 ? null : percentages.Min();
    }

    public static double? AverageRemainingPercent(UsageSnapshot snapshot)
    {
        int[] percentages = GetKnownWindows(snapshot)
            .Where(window => window.RemainingPercent.HasValue)
            .Select(window => window.RemainingPercent!.Value)
            .ToArray();
        return percentages.Length == 0 ? null : percentages.Average();
    }

    public static DateTimeOffset? NearestResetAt(UsageSnapshot snapshot)
    {
        DateTimeOffset[] resets = GetKnownWindows(snapshot)
            .Where(window => window.ResetAt.HasValue)
            .Select(window => window.ResetAt!.Value)
            .ToArray();
        return resets.Length == 0 ? null : resets.Min();
    }

    public static bool IsStale(UsageSnapshot snapshot, DateTimeOffset now, TimeSpan staleThreshold)
    {
        return snapshot.IsStale || now.ToUniversalTime() - snapshot.CapturedAt.ToUniversalTime() > staleThreshold;
    }
}
