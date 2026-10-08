using CodexProfileOverlay.Core.Models;

namespace CodexProfileOverlay.Core.Services;

public static class UsageQuotaFormatter
{
    public static string WindowName(UsageLimitWindow window, LanguagePreference language)
    {
        string key = window.Duration == TimeSpan.FromHours(5) || window.Name is "5h" or "short"
            ? "FiveHourWindow"
            : window.Duration == TimeSpan.FromDays(7) || window.Name is "Weekly" or "weekly" or "7d" or "long"
                ? "WeeklyWindow"
                : "UsageWindow";
        return key == "UsageWindow" && !string.IsNullOrWhiteSpace(window.Name)
            ? window.Name : LocalizationCatalog.Text(language, key);
    }

    public static string Summary(UsageSnapshot? snapshot, LanguagePreference language, DateTimeOffset now, TimeSpan staleThreshold)
    {
        if (snapshot is null || UsageIntelligence.GetKnownWindows(snapshot).Count == 0)
        {
            return LocalizationCatalog.Text(language, "QuotaUnknown");
        }
        string summary = string.Join(" · ", UsageIntelligence.GetKnownWindows(snapshot).Select(window =>
            LocalizationCatalog.Text(language, "QuotaRemaining", WindowName(window, language), window.RemainingPercent!)));
        return UsageIntelligence.IsStale(snapshot, now, staleThreshold)
            ? summary + " · " + LocalizationCatalog.Text(language, "QuotaCached")
            : summary;
    }
}
