namespace CodexProfileOverlay.Core.Models;

public sealed class ProfileStatusMetadata
{
    public string ProfileId { get; set; } = string.Empty;

    public string? ManualEmoji { get; set; }

    public string? ManualLabel { get; set; }

    public string? ManualNote { get; set; }

    public DateTimeOffset? ManualResetAt { get; set; }

    public string? ManualColor { get; set; }

    public bool AutomaticRefreshEnabled { get; set; } = true;

    public DateTimeOffset? LastAutomaticSnapshot { get; set; }

    public DateTimeOffset? LastRefreshAttemptAt { get; set; }

    public string? LastRefreshError { get; set; }
}
