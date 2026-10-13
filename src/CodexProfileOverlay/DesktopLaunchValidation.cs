using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CodexProfileOverlay.Core.Services;

namespace CodexProfileOverlay;

/// <summary>Opt-in package launch check using only newly created, unsigned-in test homes.</summary>
internal static class DesktopLaunchValidation
{
    internal const string Argument = "--verify-isolated-desktop-launch";

    public static bool TryHandle(string[] args, out int exitCode)
    {
        exitCode = 0;
        if (!args.Contains(Argument, StringComparer.Ordinal)) return false;
        exitCode = 2;
        if (args.Length != 2 || args[0] != Argument || !Path.IsPathFullyQualified(args[1])
            || Directory.Exists(args[1]) || File.Exists(args[1])) return true;
        string fixture = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(fixture);
        try
        {
            exitCode = RunAsync(fixture).GetAwaiter().GetResult() ? 0 : 1;
        }
        catch (Exception exception)
        {
            File.WriteAllText(Path.Combine(fixture, "result.json"), JsonSerializer.Serialize(new
            {
                passed = false, failure = exception.GetType().Name, message = exception.Message,
                fixture, realAccountDataRead = false, modelTurnStarted = false,
            }, new JsonSerializerOptions { WriteIndented = true }));
            exitCode = 1;
        }
        return true;
    }

    private static async Task<bool> RunAsync(string fixture)
    {
        var results = new List<LaunchCheck>();
        string shared = Path.Combine(fixture, "shared-history");
        Directory.CreateDirectory(shared);
        foreach (string folder in new[] { "sessions", "archived_sessions", "attachments", "thread-writer-locks" })
            Directory.CreateDirectory(Path.Combine(shared, folder));
        var logger = new SafeLogger(Path.Combine(fixture, "logs"));
        var linker = new WindowsSharedDirectoryLinker();
        var launcher = new IsolatedDesktopLauncher(logger);
        var finder = new CodexWindowFinder(logger);
        var resolver = new CodexInstanceResolver(shared);
        int[] existingProcesses = finder.FindAllWindows().Select(window => window.ProcessId).Distinct().ToArray();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        bool passed = true;
        bool simultaneouslyOpen = false;
        bool existingWindowsPreserved = false;
        try
        {
        for (int index = 0; index < 2; index++)
        {
            string home = Path.Combine(fixture, "account-" + index);
            string desktop = Path.Combine(fixture, "desktop-" + index);
            Directory.CreateDirectory(home);
            Directory.CreateDirectory(desktop);
            File.WriteAllText(Path.Combine(home, "config.toml"), "cli_auth_credentials_store = \"file\"\n[analytics]\nenabled = false\n");
            foreach (string folder in new[] { "sessions", "archived_sessions", "attachments", "thread-writer-locks" })
                linker.CreateDirectoryLink(Path.Combine(home, folder), Path.Combine(shared, folder));
            DateTimeOffset started = DateTimeOffset.UtcNow;
            IsolatedDesktopLaunchResult launch = await launcher.LaunchAsync(home, shared, desktop, timeout.Token).ConfigureAwait(false);
            DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(25);
            CodexWindowInfo? window = null;
            while (DateTimeOffset.UtcNow < deadline && !timeout.IsCancellationRequested)
            {
                window = finder.FindAllWindows().FirstOrDefault(candidate =>
                    (launch.ProcessId is null || candidate.ProcessId == launch.ProcessId)
                    && string.Equals(resolver.ResolveHome(candidate.ProcessId), home, StringComparison.OrdinalIgnoreCase)
                    && resolver.MatchesSharedEnvironment(candidate.ProcessId, shared, desktop));
                if (window is not null) break;
                await Task.Delay(250, timeout.Token).ConfigureAwait(false);
            }
            bool valid = window is not null && launch.PackageIdentityVerified
                && !File.Exists(Path.Combine(home, "auth.json"));
            passed &= valid;
            results.Add(new LaunchCheck(index, valid, window?.ProcessId ?? launch.ProcessId,
                launch.PackageIdentityVerified, launch.ActivationUncertain, window is not null,
                home, shared, desktop, (DateTimeOffset.UtcNow - started).TotalSeconds));
            if (!valid) break;
        }
        var open = finder.FindAllWindows();
        simultaneouslyOpen = results.Count == 2 && results.Select(result => result.ProcessId).Distinct().Count() == 2
            && results.All(result => open.Any(window => window.ProcessId == result.ProcessId
                && string.Equals(resolver.ResolveHome(window.ProcessId), result.Home, StringComparison.OrdinalIgnoreCase)
                && resolver.MatchesSharedEnvironment(window.ProcessId, result.Shared, result.Desktop)));
        existingWindowsPreserved = existingProcesses.All(processId => open.Any(window => window.ProcessId == processId));
        }
        finally
        {
            foreach (LaunchCheck result in results)
            {
                // Close only a PID proven to use this fresh synthetic account and cache.
                if (result.ProcessId is not int processId
                    || !string.Equals(resolver.ResolveHome(processId), result.Home, StringComparison.OrdinalIgnoreCase)
                    || !resolver.MatchesSharedEnvironment(processId, result.Shared, result.Desktop)) continue;
                try
                {
                    using Process process = Process.GetProcessById(processId);
                    CodexWindowInfo? ownWindow = finder.FindAllWindows().FirstOrDefault(window => window.ProcessId == processId);
                    if (ownWindow is not null) _ = PostMessageW(ownWindow.Hwnd, 0x0010, IntPtr.Zero, IntPtr.Zero);
                    try
                    {
                        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(12)).ConfigureAwait(false);
                    }
                    catch (TimeoutException)
                    {
                        // Test-only cleanup: never apply to a signed-in or real account.
                        if (File.Exists(Path.Combine(result.Home, "auth.json"))
                            || !string.Equals(resolver.ResolveHome(processId), result.Home, StringComparison.OrdinalIgnoreCase)
                            || !resolver.MatchesSharedEnvironment(processId, result.Shared, result.Desktop)) continue;
                        process.Kill(entireProcessTree: false);
                        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                        result.ForcedSyntheticCleanup = true;
                    }
                    result.SyntheticWindowClosed = true;
                }
                catch (Exception exception) when (exception is TimeoutException or ArgumentException or InvalidOperationException) { }
            }
            passed &= simultaneouslyOpen && existingWindowsPreserved && results.All(result => result.SyntheticWindowClosed);
            File.WriteAllText(Path.Combine(fixture, "result.json"), JsonSerializer.Serialize(new
            {
                passed, simultaneouslyOpen, existingWindowsPreserved, results,
                realAccountDataRead = false, modelTurnStarted = false,
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        return passed;
    }

    private sealed record LaunchCheck(int Index, bool Valid, int? ProcessId, bool PackageIdentityVerified,
        bool ActivationUncertain, bool WindowEnvironmentVerified, string Home, string Shared, string Desktop, double ElapsedSeconds)
    {
        public bool SyntheticWindowClosed { get; set; }
        public bool ForcedSyntheticCleanup { get; set; }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessageW(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
}
