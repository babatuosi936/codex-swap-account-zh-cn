using CodexProfileOverlay.Core.Models;
using CodexProfileOverlay.Core.Services;

namespace CodexProfileOverlay.Tests;

public sealed class UsageQuotaTests
{
    [Fact]
    public void Summary_ShowsBothRemainingWindowsAndZeroWithoutInventingUnknown()
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = new UsageSnapshot { CapturedAt = now, Windows = [
            new() { Name = "primary", Duration = TimeSpan.FromHours(5), RemainingPercent = 72 },
            new() { Name = "secondary", Duration = TimeSpan.FromDays(7), RemainingPercent = 0 },
            new() { Name = "unknown", RemainingPercent = null }] };
        Assert.Equal("5小时剩余 72% · 每周剩余 0%", UsageQuotaFormatter.Summary(snapshot, LanguagePreference.ChineseSimplified, now, TimeSpan.FromMinutes(90)));
        Assert.Equal("额度暂不可用", UsageQuotaFormatter.Summary(null, LanguagePreference.ChineseSimplified, now, TimeSpan.FromMinutes(90)));
        Assert.Equal("额度暂不可用", UsageQuotaFormatter.Summary(new UsageSnapshot(), LanguagePreference.ChineseSimplified, now, TimeSpan.FromMinutes(90)));
    }

    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 91)]
    public void Summary_LabelsFailedOrExpiredDataAsCached(bool failed, int minutesAgo)
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = new UsageSnapshot { CapturedAt = now.AddMinutes(-minutesAgo), IsStale = failed,
            Windows = [new() { Name = "5h", RemainingPercent = 44 }] };
        Assert.Equal("5小时剩余 44% · 旧数据", UsageQuotaFormatter.Summary(snapshot, LanguagePreference.ChineseSimplified, now, TimeSpan.FromMinutes(90)));
    }

    [Fact]
    public void Refresh_UsesSecondsAndBacksOffOnFailure()
    {
        var settings = new OverlaySettings();
        Assert.Equal(TimeSpan.FromSeconds(30), UsageRefreshPolicy.Interval(settings, true, false));
        Assert.Equal(TimeSpan.FromSeconds(60), UsageRefreshPolicy.Interval(settings, false, false));
        Assert.Equal(TimeSpan.FromSeconds(60), UsageRefreshPolicy.Interval(settings, true, true));
        settings.ActiveProfileRefreshIntervalSeconds = -1;
        settings.InactiveProfileRefreshIntervalSeconds = int.MaxValue;
        Assert.Equal(TimeSpan.FromSeconds(15), UsageRefreshPolicy.Interval(settings, true, false));
        Assert.Equal(TimeSpan.FromDays(1), UsageRefreshPolicy.Interval(settings, false, false));
    }

    [Fact]
    public void Refresh_UsesLiveAuthorizationOnlyForActiveAccount()
    {
        Assert.Equal("shared", UsageRefreshPolicy.QueryDirectory("main", "MAIN", "saved-main", "shared", "account-a", "account-a"));
        Assert.Equal("saved-main", UsageRefreshPolicy.QueryDirectory("main", "MAIN", "saved-main", "shared", "account-a", "account-b"));
        Assert.Equal("saved-main", UsageRefreshPolicy.QueryDirectory("main", "MAIN", "saved-main", "shared"));
        Assert.Equal("saved-other", UsageRefreshPolicy.QueryDirectory("other", "main", "saved-other", "shared"));
        Assert.Equal("saved-main", UsageRefreshPolicy.QueryDirectory("main", null, "saved-main", "shared"));
    }

    [Fact]
    public void Settings_OldMinuteConfigurationGetsNewDefaultsAndSecondsRoundTrip()
    {
        using var temp = new TempDirectory();
        string path = Path.Combine(temp.Path, "settings.json");
        File.WriteAllText(path, """{"activeProfileRefreshIntervalMinutes":15,"inactiveProfileRefreshIntervalMinutes":60}""");
        var service = new SettingsService(path);
        var settings = service.Load();
        Assert.Equal(30, settings.ActiveProfileRefreshIntervalSeconds);
        Assert.Equal(60, settings.InactiveProfileRefreshIntervalSeconds);
        settings.ActiveProfileRefreshIntervalSeconds = 45;
        settings.InactiveProfileRefreshIntervalSeconds = 90;
        service.Save(settings);
        Assert.Equal(45, service.Load().ActiveProfileRefreshIntervalSeconds);
        Assert.Equal(90, service.Load().InactiveProfileRefreshIntervalSeconds);
    }
}
