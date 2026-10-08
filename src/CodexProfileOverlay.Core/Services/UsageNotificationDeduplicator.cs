namespace CodexProfileOverlay.Core.Services;

public sealed class UsageNotificationDeduplicator
{
    private readonly Dictionary<string, string> states = new(StringComparer.OrdinalIgnoreCase);

    public bool ShouldNotify(string profileId, string eventName, string state)
    {
        string key = profileId + "\n" + eventName;
        if (states.TryGetValue(key, out string? previous) && string.Equals(previous, state, StringComparison.Ordinal))
        {
            return false;
        }

        states[key] = state;
        return true;
    }
}
