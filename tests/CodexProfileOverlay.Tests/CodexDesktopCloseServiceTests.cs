using CodexProfileOverlay.Core.Services;
using static CodexProfileOverlay.Tests.CodexDesktopProcessPolicyTests;

namespace CodexProfileOverlay.Tests;

public sealed class CodexDesktopCloseServiceTests
{
    [Fact]
    public async Task CloseRememberedAsync_StopsOnlyClosedAccountEvenAfterItsRootExited()
    {
        var root = Process(1, 0, "ChatGPT", true);
        var worker = Process(2, 1, "codex") with { Depth = 1 };
        var runtime = new FakeRuntime(worker, Process(10, 0, "ChatGPT", true), Process(11, 10, "codex"), Process(20, 0, "Code"), Process(21, 20, "codex"));
        await new CodexDesktopCloseService(runtime, 99).CloseRememberedAsync(root, [root, worker], 1, CancellationToken.None, () => true);
        Assert.Equal(new[] { 2 }, runtime.Terminated);
        Assert.Equal(new[] { 10, 11, 20, 21 }, runtime.Running.Select(process => process.Id));
    }

    [Fact]
    public async Task CloseRememberedAsync_AbortsIfAccountReopensDuringGracePeriod()
    {
        var root = Process(1, 0, "ChatGPT", true);
        var runtime = new FakeRuntime(root);
        bool reopened = false;
        runtime.OnRequest = _ => reopened = true;
        await new CodexDesktopCloseService(runtime, 99).CloseRememberedAsync(root, [root], 1, CancellationToken.None, () => !reopened);
        Assert.Empty(runtime.Terminated);
        Assert.Single(runtime.Running);
    }

    [Fact]
    public async Task CloseRememberedAsync_RejectsReusedPidAndNeverFallsBackToOtherDesktops()
    {
        var oldRoot = Process(1, 0, "ChatGPT", true);
        var replacement = oldRoot with { StartTimeUtc = oldRoot.StartTimeUtc!.Value.AddHours(1) };
        var runtime = new FakeRuntime(replacement, Process(10, 0, "ChatGPT", true));
        await new CodexDesktopCloseService(runtime, 99).CloseRememberedAsync(oldRoot, [oldRoot], 1, CancellationToken.None, () => true);
        Assert.Empty(runtime.Requested);
        Assert.Empty(runtime.Terminated);
        Assert.Equal(2, runtime.Running.Count);
    }

    [Fact]
    public async Task CloseRememberedAsync_CancellationPreservesAllProcesses()
    {
        var root = Process(1, 0, "ChatGPT", true);
        var runtime = new FakeRuntime(root);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new CodexDesktopCloseService(runtime, 99)
            .CloseRememberedAsync(root, [root], 1, cancellation.Token, () => true));
        Assert.Empty(runtime.Requested);
        Assert.Empty(runtime.Terminated);
    }

    [Fact]
    public async Task CloseAsync_GracefulExitDoesNotForceTerminateOrTouchUnrelatedCli()
    {
        var runtime = new FakeRuntime(Process(1, 0, "ChatGPT", desktop: true), Process(2, 0, "codex")) { ExitOnRequest = true };

        await new CodexDesktopCloseService(runtime, 99).CloseAsync(1, true, CancellationToken.None);

        Assert.Equal(new[] { 1 }, runtime.Requested);
        Assert.Empty(runtime.Terminated);
        Assert.Equal(new[] { 2 }, runtime.Running.Select(process => process.Id));
    }

    [Fact]
    public async Task CloseAsync_ForceFallbackClosesOnlySelectedProcessesChildrenFirst()
    {
        var runtime = new FakeRuntime(Process(1, 0, "ChatGPT", desktop: true), Process(2, 1, "codex"), Process(3, 0, "Code"), Process(4, 3, "codex"));

        await new CodexDesktopCloseService(runtime, 99).CloseAsync(1, true, CancellationToken.None);

        Assert.Equal(new[] { 2, 1 }, runtime.Terminated);
        Assert.Equal(new[] { 3, 4 }, runtime.Running.Select(process => process.Id));
    }

    [Fact]
    public async Task CloseAsync_StillWaitsForOrphanAfterDesktopExits()
    {
        var runtime = new FakeRuntime(Process(1, 0, "ChatGPT", desktop: true), Process(2, 1, "codex"));
        runtime.OnRequest = process => { if (process.Id == 1) runtime.Running.RemoveAll(item => item.Id == 1); };

        await new CodexDesktopCloseService(runtime, 99).CloseAsync(1, true, CancellationToken.None);

        Assert.Equal(new[] { 2 }, runtime.Terminated);
        Assert.Empty(runtime.Running);
    }

    [Fact]
    public async Task CloseAsync_WithoutForceFallbackFailsBeforeAnyTermination()
    {
        var runtime = new FakeRuntime(Process(1, 0, "ChatGPT", desktop: true));

        await Assert.ThrowsAsync<InvalidOperationException>(() => new CodexDesktopCloseService(runtime, 99).CloseAsync(1, false, CancellationToken.None));

        Assert.Empty(runtime.Terminated);
        Assert.Single(runtime.Running);
    }

    [Fact]
    public async Task CloseAsync_CancellationDoesNotCloseAnything()
    {
        var runtime = new FakeRuntime(Process(1, 0, "ChatGPT", desktop: true));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new CodexDesktopCloseService(runtime, 99).CloseAsync(1, true, cancellation.Token));

        Assert.Empty(runtime.Requested);
        Assert.Empty(runtime.Terminated);
    }

    [Fact]
    public async Task CloseAsync_TracksNewDesktopChildrenSpawnedDuringShutdown()
    {
        var runtime = new FakeRuntime(Process(1, 0, "ChatGPT", desktop: true));
        runtime.OnRequest = process => { if (process.Id == 1) runtime.Running.Add(Process(2, 1, "codex")); };

        await new CodexDesktopCloseService(runtime, 99).CloseAsync(1, true, CancellationToken.None);

        Assert.Equal(new[] { 2, 1 }, runtime.Terminated);
    }

    [Fact]
    public async Task CloseAsync_InaccessibleIdentityAbortsWithoutTerminatingAnything()
    {
        var runtime = new FakeRuntime(Process(1, 0, "ChatGPT", desktop: true)) { IdentityFailure = true };

        await Assert.ThrowsAsync<InvalidOperationException>(() => new CodexDesktopCloseService(runtime, 99).CloseAsync(1, true, CancellationToken.None));

        Assert.Empty(runtime.Requested);
        Assert.Empty(runtime.Terminated);
    }

    private sealed class FakeRuntime(params DesktopProcessInfo[] processes) : IDesktopProcessRuntime
    {
        public List<DesktopProcessInfo> Running { get; } = [.. processes];
        public List<int> Requested { get; } = [];
        public List<int> Terminated { get; } = [];
        public bool ExitOnRequest { get; init; }
        public bool IdentityFailure { get; init; }
        public Action<DesktopProcessInfo>? OnRequest { get; set; }
        public IReadOnlyList<DesktopProcessInfo> Snapshot() => Running.ToArray();
        public bool IsRunning(DesktopProcessInfo process)
        {
            if (IdentityFailure) throw new InvalidOperationException("Cannot verify process identity.");
            return Running.Any(item => item.Id == process.Id && item.StartTimeUtc == process.StartTimeUtc);
        }
        public void RequestClose(DesktopProcessInfo process)
        {
            Requested.Add(process.Id);
            if (ExitOnRequest) Running.RemoveAll(item => item.Id == process.Id);
            OnRequest?.Invoke(process);
        }
        public void Terminate(DesktopProcessInfo process)
        {
            Terminated.Add(process.Id);
            Running.RemoveAll(item => item.Id == process.Id);
        }
    }
}
