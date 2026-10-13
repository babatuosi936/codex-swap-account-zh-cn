using CodexProfileOverlay.Core.Services;

namespace CodexProfileOverlay.Tests;

public sealed class AccountPlanTests
{
    [Theory]
    [InlineData("plus", "Plus")]
    [InlineData("pro", "Pro")]
    [InlineData("free", "Free")]
    [InlineData(" PLUS ", "Plus")]
    [InlineData("business", "Business")]
    [InlineData("pro_lite", "Pro Lite")]
    [InlineData("prolite", "Pro Lite")]
    [InlineData("promax", "Pro Max")]
    [InlineData("self_serve_business_prolite", "Business Pro Lite")]
    [InlineData("enterprise_cbp_usage_based", "Enterprise")]
    [InlineData("edu_pro", "Edu Pro")]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("unknown", null)]
    public void FormatterUsesOnlyKnownReportedPlans(string? input, string? expected)
        => Assert.Equal(expected, AccountPlanFormatter.Normalize(input));

    [Theory]
    [InlineData("plus", "Plus")]
    [InlineData("pro", "Pro")]
    [InlineData("free", "Free")]
    public void ParserReadsPlanFromSelectedCodexRateLimit(string plan, string expected)
    {
        string json = """{"result":{"rateLimits":{"planType":"free","primary":{"usedPercent":1}},"rateLimitsByLimitId":{"codex":{"planType":"PLAN","primary":{"usedPercent":20}},"other":{"planType":"pro","primary":{"usedPercent":99}}}}}""".Replace("PLAN", plan);
        var snapshot = CodexAppServerRateLimitsParser.Parse(json, DateTimeOffset.UtcNow, null);
        Assert.NotNull(snapshot);
        Assert.Equal(expected, snapshot.PlanName);
        Assert.Equal(80, Assert.Single(snapshot.Windows).RemainingPercent);
    }

    [Fact]
    public void MissingPlanDoesNotMeanFree()
    {
        var snapshot = CodexAppServerRateLimitsParser.Parse(
            """{"result":{"rateLimits":{"primary":{"usedPercent":20}}}}""", DateTimeOffset.UtcNow, null);
        Assert.NotNull(snapshot);
        Assert.Null(snapshot.PlanName);
    }

    [Fact]
    public void MissingNamedPlanFallsBackToSingleBucketMetadataOnly()
    {
        var snapshot = CodexAppServerRateLimitsParser.Parse(
            """{"result":{"rateLimits":{"planType":"plus","primary":{"usedPercent":99}},"rateLimitsByLimitId":{"codex":{"planType":null,"primary":{"usedPercent":20}}}}}""",
            DateTimeOffset.UtcNow, null);
        Assert.NotNull(snapshot);
        Assert.Equal("Plus", snapshot.PlanName);
        Assert.Equal(80, Assert.Single(snapshot.Windows).RemainingPercent);
    }

    [Fact]
    public void PlanWithoutLimitsDoesNotInventQuotaOrExhaustion()
    {
        var snapshot = CodexAppServerRateLimitsParser.Parse(
            """{"result":{"rateLimits":{"planType":"free","primary":null,"secondary":null}}}""", DateTimeOffset.UtcNow, null);
        Assert.NotNull(snapshot);
        Assert.Equal("Free", snapshot.PlanName);
        Assert.Empty(snapshot.Windows);
        Assert.Null(snapshot.IsExhausted);
        Assert.Null(snapshot.ShortWindowRemainingPercent);
        Assert.Null(snapshot.LongWindowRemainingPercent);
    }

    [Fact]
    public void PlanWithoutLimitsPreservesExplicitServerExhaustion()
    {
        var snapshot = CodexAppServerRateLimitsParser.Parse(
            """{"result":{"rateLimits":{"planType":"free","primary":null,"secondary":null,"rateLimitReachedType":"rate_limit_reached"}}}""",
            DateTimeOffset.UtcNow, null);
        Assert.NotNull(snapshot);
        Assert.Equal("Free", snapshot.PlanName);
        Assert.True(snapshot.IsExhausted);
        Assert.Empty(snapshot.Windows);
    }
}
