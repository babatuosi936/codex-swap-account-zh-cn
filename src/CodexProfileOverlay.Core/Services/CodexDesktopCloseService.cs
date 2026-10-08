namespace CodexProfileOverlay.Core.Services;

public interface IDesktopProcessRuntime
{
    IReadOnlyList<DesktopProcessInfo> Snapshot();
    bool IsRunning(DesktopProcessInfo process);
    void RequestClose(DesktopProcessInfo process);
    void Terminate(DesktopProcessInfo process);
}

public sealed class CodexDesktopCloseService
{
    private readonly IDesktopProcessRuntime runtime;
    private readonly int overlayProcessId;

    public CodexDesktopCloseService(IDesktopProcessRuntime runtime, int overlayProcessId)
    {
        this.runtime = runtime;
        this.overlayProcessId = overlayProcessId;
    }

    public async Task CloseAsync(int gracefulTimeoutSeconds, bool allowForceClose, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var tracked = new Dictionary<(int Id, DateTime? Started), DesktopProcessInfo>();
        var requested = new HashSet<(int Id, DateTime? Started)>();

        IReadOnlyList<DesktopProcessInfo> Refresh()
        {
            foreach (DesktopProcessInfo process in CodexDesktopProcessPolicy.SelectTargets(runtime.Snapshot(), overlayProcessId, tracked.Values))
            {
                var key = (process.Id, process.StartTimeUtc);
                if (!tracked.TryGetValue(key, out DesktopProcessInfo? previous) || previous.Depth < process.Depth)
                {
                    tracked[key] = process;
                }
            }
            return tracked.Values.Where(runtime.IsRunning).OrderByDescending(process => process.Depth).ToArray();
        }

        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(Math.Clamp(gracefulTimeoutSeconds, 1, 60));
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<DesktopProcessInfo> running = Refresh();
            if (running.Count == 0)
            {
                return;
            }
            foreach (DesktopProcessInfo process in running)
            {
                if (requested.Add((process.Id, process.StartTimeUtc)))
                {
                    runtime.RequestClose(process);
                }
            }
            if (DateTimeOffset.UtcNow >= deadline)
            {
                break;
            }
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }

        if (!allowForceClose)
        {
            throw new InvalidOperationException("Codex Desktop is still running, so the profile cannot be switched safely.");
        }

        deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<DesktopProcessInfo> running = Refresh();
            if (running.Count == 0)
            {
                return;
            }
            if (DateTimeOffset.UtcNow >= deadline)
            {
                throw new InvalidOperationException("Codex Desktop processes did not exit after forced termination.");
            }
            // Kill only selected, identity-checked processes, deepest children first.
            // Killing an entire tree would bypass the protected editor/helper boundaries.
            foreach (DesktopProcessInfo process in running)
            {
                runtime.Terminate(process);
            }
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }
    }
}
