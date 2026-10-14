namespace CodexProfileOverlay.Core.Services;

public sealed record ClosedAccountDesktop(DesktopProcessInfo Root, IReadOnlyList<DesktopProcessInfo> Processes);

// Observe only verified desktops that have had an open account window. Never
// sweep arbitrary background processes present when the helper starts.
public sealed class DesktopWindowLifetimeTracker(int overlayProcessId)
{
    private readonly Dictionary<(int Id, DateTime? Started), Entry> entries = [];
    private static readonly TimeSpan ClosedWindowDelay = TimeSpan.FromSeconds(1);

    public IReadOnlyList<ClosedAccountDesktop> Observe(IReadOnlyList<DesktopProcessInfo> snapshot,
        IReadOnlySet<int> openWindowProcesses, IReadOnlySet<int> verifiedAccountProcesses, DateTimeOffset now)
    {
        foreach (var root in snapshot.Where(process => process.IsDesktopExecutable && process.StartTimeUtc is not null
            && openWindowProcesses.Contains(process.Id) && verifiedAccountProcesses.Contains(process.Id)))
        {
            var key = (root.Id, root.StartTimeUtc);
            if (!entries.ContainsKey(key)) entries[key] = new Entry(root);
        }

        var closed = new List<ClosedAccountDesktop>();
        var live = snapshot.Select(process => (process.Id, process.StartTimeUtc)).ToHashSet();
        foreach (var pair in entries.ToArray())
        {
            var entry = pair.Value;
            bool sameRoot = snapshot.Any(process => process.Id == entry.Root.Id && process.StartTimeUtc == entry.Root.StartTimeUtc);
            foreach (var key in entry.Processes.Keys.Where(key => !live.Contains(key)).ToArray()) entry.Processes.Remove(key);
            foreach (var process in CodexDesktopProcessPolicy.SelectTargets(snapshot, overlayProcessId, entry.Processes.Values, entry.Root))
            {
                // Unknown start times cannot be used as a safe remembered identity.
                if (process.StartTimeUtc is not null) entry.Processes[(process.Id, process.StartTimeUtc)] = process;
            }
            if (sameRoot && openWindowProcesses.Contains(entry.Root.Id))
            {
                entry.ClosedSince = null;
                continue;
            }
            entry.ClosedSince ??= now;
            if (now - entry.ClosedSince < ClosedWindowDelay) continue;
            closed.Add(new(entry.Root, entry.Processes.Values.ToArray()));
            entries.Remove(pair.Key);
        }
        return closed;
    }

    private sealed class Entry(DesktopProcessInfo root)
    {
        public DesktopProcessInfo Root { get; } = root;
        public Dictionary<(int Id, DateTime? Started), DesktopProcessInfo> Processes { get; } = [];
        public DateTimeOffset? ClosedSince { get; set; }
    }
}
