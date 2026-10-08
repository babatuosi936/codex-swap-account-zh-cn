namespace CodexProfileOverlay.Core.Models;

public sealed class UsageLimitWindow
{
    public string Name { get; set; } = string.Empty;

    public TimeSpan? Duration { get; set; }

    public int? RemainingPercent { get; set; }

    public DateTimeOffset? ResetAt { get; set; }
}
