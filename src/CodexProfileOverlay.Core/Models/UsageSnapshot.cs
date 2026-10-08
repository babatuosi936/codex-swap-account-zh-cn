namespace CodexProfileOverlay.Core.Models;

public sealed class UsageSnapshot
{
    public List<UsageLimitWindow> Windows { get; set; } = [];

    // Kept for backward-compatible deserialization of the first status document format.
    public int? ShortWindowRemainingPercent { get; set; }

    public DateTimeOffset? ShortWindowResetAt { get; set; }

    public int? LongWindowRemainingPercent { get; set; }

    public DateTimeOffset? LongWindowResetAt { get; set; }

    public int? CreditsRemaining { get; set; }

    public string? PlanName { get; set; }

    public bool? IsExhausted { get; set; }

    public DateTimeOffset CapturedAt { get; set; } = DateTimeOffset.UtcNow;

    public string? Source { get; set; }

    public string? CodexCliVersion { get; set; }

    public int Confidence { get; set; } = 100;

    public bool IsStale { get; set; }
}
