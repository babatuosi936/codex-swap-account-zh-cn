// Opt-in local integration probe: no auth writes, network or process closes.
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using CodexProfileOverlay.Core.Services;

internal static class Program
{
    private delegate bool WindowCallback(IntPtr hwnd, IntPtr state);
    [DllImport("user32.dll")] private static extern bool EnumWindows(WindowCallback callback, IntPtr state);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr hwnd, uint command);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hwnd, int command);
    [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint first, uint second, bool attach);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();

    [STAThread]
    private static int Main(string[] args)
    {
        var assembly = typeof(CodexProfileOverlay.App).Assembly;
        var paths = AppPaths.FromEnvironment();
        var profiles = new ProfileDiscoveryService(paths.ProfilesDirectory).DiscoverProfiles();
        string output = args.FirstOrDefault(value => value.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            ?? Path.Combine(AppContext.BaseDirectory, "multi-instance-probe.json");
        var finderType = assembly.GetType("CodexProfileOverlay.CodexWindowFinder")!;
        var resolverType = assembly.GetType("CodexProfileOverlay.CodexInstanceResolver")!;
        var finder = Activator.CreateInstance(finderType, new SafeLogger(Path.Combine(Path.GetDirectoryName(output)!, "probe-logs")))!;
        var resolver = Activator.CreateInstance(resolverType, paths.SharedCodexDirectory)!;
        var windows = ((System.Collections.IEnumerable)finderType.GetMethod("FindAllWindows")!.Invoke(finder, null)!).Cast<object>().ToArray();
        var results = new List<object>();
        bool passed = windows.Length > 0;
        IntPtr original = GetForegroundWindow();
        try
        {
            foreach (var window in windows)
            {
                int pid = (int)window.GetType().GetProperty("ProcessId")!.GetValue(window)!;
                IntPtr hwnd = (IntPtr)window.GetType().GetProperty("Hwnd")!.GetValue(window)!;
                string? home = (string?)resolverType.GetMethod("ResolveHome")!.Invoke(resolver, [pid]);
                string? active = (string?)resolverType.GetMethod("FindActiveProfile")!.Invoke(resolver, [pid, profiles]);
                bool? follows = null;
                bool? foreground = null;
                if (args.Contains("--verify-follow"))
                {
                    uint foregroundThread = GetWindowThreadProcessId(GetForegroundWindow(), out _);
                    uint probeThread = GetCurrentThreadId();
                    bool joined = foregroundThread != probeThread && AttachThreadInput(probeThread, foregroundThread, true);
                    try { ShowWindow(hwnd, 9); SetForegroundWindow(hwnd); }
                    finally { if (joined) AttachThreadInput(probeThread, foregroundThread, false); }
                    Thread.Sleep(1700);
                    var overlayIds = Process.GetProcessesByName("CodexProfileOverlay").Select(p => (uint)p.Id).ToHashSet();
                    bool found = false;
                    EnumWindows((candidate, _) =>
                    {
                        GetWindowThreadProcessId(candidate, out uint ownerPid);
                        if (overlayIds.Contains(ownerPid) && IsWindowVisible(candidate)
                            && GetWindow(candidate, 4) == hwnd) found = true;
                        return true;
                    }, IntPtr.Zero);
                    foreground = GetForegroundWindow() == hwnd;
                    follows = found && foreground.Value;
                }
                results.Add(new { pid, resolvedHome = home is not null, accountMatched = active is not null, foreground, follows });
                passed &= home is not null && active is not null && follows != false;
            }
        }
        finally { if (args.Contains("--verify-follow")) SetForegroundWindow(original); }
        File.WriteAllText(output, JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine(JsonSerializer.Serialize(results));
        return passed ? 0 : 1;
    }
}
