using System.Text.Json;
using CodexProfileOverlay.Core.Models;
using CodexProfileOverlay.Core.Services;

namespace CodexProfileOverlay.Tests;

public sealed class CodexAppServerRateLimitsParserTests
{
    [Fact]
    public void Parse_UsesStableProtocolResponse()
    {
        UsageSnapshot? snapshot = CodexAppServerRateLimitsParser.Parse(
            """
            {"id":2,"result":{"rateLimits":{"primary":{"usedPercent":31,"windowDurationMins":300,"resetsAt":1783282686},"secondary":{"usedPercent":95,"windowDurationMins":10080,"resetsAt":1783584260},"rateLimitReachedType":null},"rateLimitsByLimitId":null}}
            """,
            new DateTimeOffset(2026, 7, 5, 10, 0, 0, TimeSpan.Zero),
            "codex-cli 0.142.5");

        Assert.NotNull(snapshot);
        Assert.Equal(2, snapshot.Windows.Count);
        Assert.Equal(69, snapshot.Windows.Single(window => window.Name == "5h").RemainingPercent);
        Assert.Equal(5, snapshot.Windows.Single(window => window.Name == "Weekly").RemainingPercent);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1783584260), snapshot.Windows.Single(window => window.Name == "Weekly").ResetAt);
        Assert.False(snapshot.IsExhausted);
    }

    [Fact]
    public void Parse_UsesNamedRateLimitMapWhenPresent()
    {
        UsageSnapshot? snapshot = CodexAppServerRateLimitsParser.Parse(
            """
            {"id":2,"result":{"rateLimits":null,"rateLimitsByLimitId":{"codex":{"primary":{"usedPercent":40,"windowDurationMins":300,"resetsAt":1783282686},"secondary":{"usedPercent":80,"windowDurationMins":10080,"resetsAt":1783584260},"rateLimitReachedType":null}}}}
            """,
            DateTimeOffset.UtcNow,
            null);

        Assert.NotNull(snapshot);
        Assert.Equal(60, snapshot.Windows.Single(window => window.Name == "5h").RemainingPercent);
        Assert.Equal(20, snapshot.Windows.Single(window => window.Name == "Weekly").RemainingPercent);
    }

    [Fact]
    public void Parse_MalformedOrRenderedOutputReturnsNull()
    {
        Assert.Null(CodexAppServerRateLimitsParser.Parse("not-json", DateTimeOffset.UtcNow, null));
        Assert.Null(CodexAppServerRateLimitsParser.Parse(
            """
            Usage remaining 5%
            5h        69%     9:58 PM
            Weekly     5%     Jul 9
            """,
            DateTimeOffset.UtcNow,
            null));
    }

    [Fact]
    public void Parse_DiscardsPrivacyFieldsAndStoresOnlySafeSourceMetadata()
    {
        UsageSnapshot? snapshot = CodexAppServerRateLimitsParser.Parse(
            """
            {"account":{"email":"user@example.invalid"},"sessionId":"00000000-0000-0000-0000-000000000000","id":2,"result":{"rateLimits":{"primary":{"usedPercent":25,"windowDurationMins":300,"resetsAt":1783282686},"secondary":null,"rateLimitReachedType":null},"rateLimitsByLimitId":null}}
            """,
            new DateTimeOffset(2026, 7, 5, 10, 0, 0, TimeSpan.Zero),
            "codex-cli 0.142.5");

        Assert.NotNull(snapshot);
        string json = JsonSerializer.Serialize(snapshot);
        Assert.Equal(CodexCliStatusUsageProvider.SourceIdentifier, snapshot.Source);
        Assert.Equal("codex-cli 0.142.5", snapshot.CodexCliVersion);
        Assert.DoesNotContain("user@example.invalid", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("00000000-0000-0000-0000-000000000000", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Provider_ReturnsNullForUnavailableSource()
    {
        var provider = new CodexCliStatusUsageProvider(new FakeRateLimitsSource(false, null));

        UsageSnapshot? snapshot = await provider.GetUsageAsync(@"C:\fake-profile", CancellationToken.None);

        Assert.Equal(UsageProviderCapability.Unavailable, provider.Capability);
        Assert.Null(snapshot);
    }

    [Fact]
    public async Task Provider_ParsesFakeAppServerOutput()
    {
        var provider = new CodexCliStatusUsageProvider(new FakeRateLimitsSource(true,
            """
            {"id":2,"result":{"rateLimits":{"primary":{"usedPercent":12,"windowDurationMins":300,"resetsAt":1783282686},"secondary":{"usedPercent":39,"windowDurationMins":10080,"resetsAt":1783584260},"rateLimitReachedType":null},"rateLimitsByLimitId":null}}
            """));

        UsageSnapshot? snapshot = await provider.GetUsageAsync(@"C:\fake-profile", CancellationToken.None);

        Assert.NotNull(snapshot);
        Assert.Equal(UsageProviderCapability.Supported, provider.Capability);
        Assert.Equal(61, UsageIntelligence.EffectiveRemainingPercent(snapshot));
    }

    [Fact]
    public async Task SupportTest_ReturnsFalseOnTimeout()
    {
        using var temp = new TempDirectory();
        var service = new ProfileStatusService(
            new ProfileStatusStore(Path.Combine(temp.Path, "status.json")),
            new SlowProvider(),
            new SafeLogger(temp.Path));

        bool supported = await service.TestProviderSupportAsync(@"C:\fake-profile", CancellationToken.None, TimeSpan.FromMilliseconds(20));

        Assert.False(supported);
    }

    private sealed class FakeRateLimitsSource(bool available, string? output) : ICodexRateLimitsSource
    {
        public bool IsAvailable => available;

        public Task<CodexRateLimitsCapture?> CaptureRateLimitsAsync(string profileDirectory, CancellationToken cancellationToken)
            => Task.FromResult(output is null
                ? null
                : new CodexRateLimitsCapture(output, new DateTimeOffset(2026, 7, 5, 10, 0, 0, TimeSpan.Zero), "codex-cli 0.142.5"));
    }

    private sealed class SlowProvider : IUsageProvider
    {
        public UsageProviderCapability Capability => UsageProviderCapability.Supported;

        public async Task<UsageSnapshot?> GetUsageAsync(string profileDirectory, CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
            return null;
        }
    }
}
