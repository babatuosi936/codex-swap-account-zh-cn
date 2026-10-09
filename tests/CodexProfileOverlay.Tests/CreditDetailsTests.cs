using CodexProfileOverlay.Core.Models;
using CodexProfileOverlay.Core.Services;
using Xunit;

namespace CodexProfileOverlay.Tests;

public sealed class CreditDetailsTests
{
    [Theory]
    [InlineData("{\"balance\":\"12.5\",\"unlimited\":false}", "12.5", false)]
    [InlineData("{\"balance\":0,\"unlimited\":false}", "0", false)]
    [InlineData("{\"hasCredits\":false,\"unlimited\":false}", "0", false)]
    [InlineData("{\"unlimited\":true}", null, true)]
    [InlineData("{}", null, null)]
    public void ParsePreservesCreditBalanceAndUnknown(string credits, string? expected, bool? unlimited)
    {
        var snapshot = CodexAppServerRateLimitsParser.Parse("{\"result\":{\"rateLimits\":{\"primary\":{\"usedPercent\":50},\"credits\":" + credits + "}}}", DateTimeOffset.UtcNow, null)!;
        Assert.Equal(expected is null ? null : decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), snapshot.CreditsBalance);
        Assert.Equal(unlimited, snapshot.CreditsUnlimited);
    }

    [Fact]
    public void ResetCreditsExcludeUsedAndExpiredAndSelectNearestExpiry()
    {
        var snapshot = new UsageSnapshot();
        var now = DateTimeOffset.FromUnixTimeSeconds(2000000000);
        ResetCreditsSource.Apply("""
            {"credits":[{"status":"used","expires_at":2000000300},{"status":"expired"},{"expires_at":1999999999},{"status":"available","expires_at":2000000600},{"status":"available","expires_at":2000000900000}]}
            """, snapshot, now);
        Assert.Equal(2, snapshot.ResetCreditsRemaining);
        Assert.Equal(now.AddMinutes(10), snapshot.ResetCreditsExpiresAt);
    }

    [Theory]
    [InlineData("{}", null)]
    [InlineData("{\"error\":\"unavailable\"}", null)]
    [InlineData("{\"credits\":[]}", 0)]
    [InlineData("{\"available_count\":1,\"next_expires_at\":2000000600}", 1)]
    [InlineData("{\"data\":{\"availableCount\":2,\"nextExpiresAt\":\"2033-05-18T03:43:20Z\"}}", 2)]
    public void ResetCreditZeroAndUnknownStayDistinct(string json, int? count)
    {
        var snapshot = new UsageSnapshot();
        ResetCreditsSource.Apply(json, snapshot, DateTimeOffset.FromUnixTimeSeconds(2000000000));
        Assert.Equal(count, snapshot.ResetCreditsRemaining);
        if (count is null or 0) Assert.Null(snapshot.ResetCreditsExpiresAt);
    }
}
