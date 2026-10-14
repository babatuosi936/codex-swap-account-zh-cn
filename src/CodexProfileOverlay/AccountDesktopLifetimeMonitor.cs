using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodexProfileOverlay.Core.Services;

namespace CodexProfileOverlay;

internal sealed class AccountDesktopLifetimeMonitor : IDisposable
{
    private readonly WindowsDesktopProcessRuntime runtime;
    private readonly DesktopWindowLifetimeTracker tracker = new(Environment.ProcessId);
    private readonly CancellationTokenSource stopping = new();
    private readonly HashSet<(int Id, DateTime? Started)> closing = [];
    private readonly SafeLogger logger;
    private readonly Action<Exception> reportFailure;
    private DateTimeOffset lastObservation = DateTimeOffset.MinValue;
    private bool disposed;

    public AccountDesktopLifetimeMonitor(SafeLogger logger, Action<Exception> reportFailure)
    {
        this.logger = logger;
        this.reportFailure = reportFailure;
        runtime = new WindowsDesktopProcessRuntime(logger);
    }

    public void Observe(IReadOnlyList<CodexWindowInfo> windows, Func<int, bool> verifiedAccount, int gracefulTimeoutSeconds)
    {
        if (disposed) return;
        var now = DateTimeOffset.UtcNow;
        if (now - lastObservation < TimeSpan.FromMilliseconds(700)) return;
        lastObservation = now;
        var snapshot = runtime.Snapshot();
        var open = windows.Select(window => window.ProcessId).ToHashSet();
        var verified = open.Where(verifiedAccount).ToHashSet();
        foreach (var request in tracker.Observe(snapshot, open, verified, now))
        {
            if (closing.Add((request.Root.Id, request.Root.StartTimeUtc)))
                _ = CloseAsync(request, gracefulTimeoutSeconds);
        }
    }

    private async Task CloseAsync(ClosedAccountDesktop request, int gracefulTimeoutSeconds)
    {
        var token = stopping.Token;
        try
        {
            logger.Info($"Last account window closed; stopping desktop {request.Root.Id} and its remembered workers.");
            // The published helper is launched separately. Check before touching
            // a desktop nevertheless, because package/job lifetime may change.
            if (CodexDesktopProcessPolicy.HasLifetimeDependency(runtime.Snapshot(), Environment.ProcessId,
                WindowsDesktopProcessRuntime.CurrentJobMembers())) throw new DesktopLifetimeDependencyException();
            await new CodexDesktopCloseService(runtime, Environment.ProcessId).CloseRememberedAsync(
                request.Root, request.Processes, gracefulTimeoutSeconds, token,
                () => !CodexWindowFinder.HasOpenWindow(request.Root.Id)).ConfigureAwait(true);
            logger.Info($"Closed-account desktop cleanup completed for process {request.Root.Id}.");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception exception)
        {
            logger.Error("Could not stop the closed account desktop background.", exception);
            if (!disposed) reportFailure(exception);
        }
        finally { closing.Remove((request.Root.Id, request.Root.StartTimeUtc)); }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        stopping.Cancel();
        stopping.Dispose();
    }
}
