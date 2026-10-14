using CodexProfileOverlay.Core.Services;
using static CodexProfileOverlay.Tests.CodexDesktopProcessPolicyTests;

namespace CodexProfileOverlay.Tests;

public sealed class DesktopWindowLifetimeTrackerTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public void ClosingOneAccount_RetainsOtherAccountAndEditorWorkers()
    {
        var first = Process(1, 0, "ChatGPT", true);
        var second = Process(10, 0, "ChatGPT", true);
        DesktopProcessInfo[] snapshot = [first, Process(2, 1, "codex"), second, Process(11, 10, "codex"), Process(20, 0, "Code"), Process(21, 20, "codex")];
        var tracker = new DesktopWindowLifetimeTracker(99);
        Assert.Empty(tracker.Observe(snapshot, new HashSet<int> { 1, 10 }, new HashSet<int> { 1, 10 }, Now));
        Assert.Empty(tracker.Observe(snapshot, new HashSet<int> { 10 }, new HashSet<int> { 10 }, Now.AddMilliseconds(750)));
        var closed = Assert.Single(tracker.Observe(snapshot, new HashSet<int> { 10 }, new HashSet<int> { 10 }, Now.AddSeconds(2)));
        Assert.Equal(first, closed.Root);
        Assert.Equal(new[] { 1, 2 }, closed.Processes.Select(process => process.Id).Order());
        Assert.Empty(tracker.Observe(snapshot, new HashSet<int> { 10 }, new HashSet<int> { 10 }, Now.AddSeconds(3)));
    }

    [Fact]
    public void DesktopExitsBeforeCleanup_RemembersOrphanedServer()
    {
        var first = Process(1, 0, "ChatGPT", true);
        var worker = Process(2, 1, "codex");
        var tracker = new DesktopWindowLifetimeTracker(99);
        tracker.Observe([first, worker], new HashSet<int> { 1 }, new HashSet<int> { 1 }, Now);
        tracker.Observe([worker], new HashSet<int>(), new HashSet<int>(), Now.AddSeconds(1));
        var closed = Assert.Single(tracker.Observe([worker], new HashSet<int>(), new HashSet<int>(), Now.AddSeconds(3)));
        Assert.Equal(first, closed.Root);
        Assert.Equal(worker.Id, Assert.Single(closed.Processes).Id);
    }

    [Fact]
    public void OpenWindow_PreventsCleanup()
    {
        var tracker = new DesktopWindowLifetimeTracker(99);
        var root = Process(1, 0, "ChatGPT", true);
        Assert.Empty(tracker.Observe([root], new HashSet<int> { 1 }, new HashSet<int> { 1 }, Now));
        Assert.Empty(tracker.Observe([root], new HashSet<int> { 1 }, new HashSet<int> { 1 }, Now.AddHours(2)));
    }

    [Fact]
    public void TemporaryHideOrReopenedWindow_ResetsTheCloseDelay()
    {
        var tracker = new DesktopWindowLifetimeTracker(99);
        var root = Process(1, 0, "ChatGPT", true);
        tracker.Observe([root], new HashSet<int> { 1 }, new HashSet<int> { 1 }, Now);
        Assert.Empty(tracker.Observe([root], new HashSet<int>(), new HashSet<int>(), Now.AddSeconds(1)));
        Assert.Empty(tracker.Observe([root], new HashSet<int> { 1 }, new HashSet<int> { 1 }, Now.AddMilliseconds(1300)));
        Assert.Empty(tracker.Observe([root], new HashSet<int>(), new HashSet<int>(), Now.AddSeconds(2)));
        Assert.Empty(tracker.Observe([root], new HashSet<int>(), new HashSet<int>(), Now.AddMilliseconds(2500)));
        Assert.Single(tracker.Observe([root], new HashSet<int>(), new HashSet<int>(), Now.AddSeconds(4)));
    }

    [Fact]
    public void UnseenOrUnverifiedBackground_IsNotSweptOnStartup()
    {
        var tracker = new DesktopWindowLifetimeTracker(99);
        var root = Process(1, 0, "ChatGPT", true);
        Assert.Empty(tracker.Observe([root], new HashSet<int>(), new HashSet<int>(), Now));
        Assert.Empty(tracker.Observe([root], new HashSet<int> { 1 }, new HashSet<int>(), Now.AddSeconds(1)));
        Assert.Empty(tracker.Observe([root], new HashSet<int>(), new HashSet<int>(), Now.AddSeconds(4)));
    }

    [Fact]
    public void ReusedProcessIds_DoNotInheritOldAccountWorkers()
    {
        var tracker = new DesktopWindowLifetimeTracker(99);
        var oldRoot = Process(1, 0, "ChatGPT", true);
        var worker = Process(2, 1, "codex");
        tracker.Observe([oldRoot, worker], new HashSet<int> { 1 }, new HashSet<int> { 1 }, Now);
        var replacement = oldRoot with { StartTimeUtc = oldRoot.StartTimeUtc!.Value.AddHours(1) };
        var replacementWorker = worker with { StartTimeUtc = worker.StartTimeUtc!.Value.AddHours(1) };
        tracker.Observe([replacement, replacementWorker], new HashSet<int> { 1 }, new HashSet<int> { 1 }, Now.AddSeconds(1));
        var closed = Assert.Single(tracker.Observe([replacement, replacementWorker], new HashSet<int> { 1 }, new HashSet<int> { 1 }, Now.AddSeconds(3)));
        Assert.Equal(oldRoot, closed.Root);
        Assert.Empty(closed.Processes);
    }
}
