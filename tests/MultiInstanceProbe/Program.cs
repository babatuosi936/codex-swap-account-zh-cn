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
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr hwnd, uint command);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern void SwitchToThisWindow(IntPtr hwnd, bool altTab);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hwnd, int command);
    [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint first, uint second, bool attach);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(NativePoint point);
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr hwnd, uint flag);
    [DllImport("user32.dll")] private static extern bool IsWindowEnabled(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern IntPtr GetLastActivePopup(IntPtr hwnd);

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
        var initialStates = windows.Select(w => (Hwnd: (IntPtr)w.GetType().GetProperty("Hwnd")!.GetValue(w)!, Minimized: (bool)w.GetType().GetProperty("IsMinimized")!.GetValue(w)!)).ToArray();
        try
        {
            IntPtr previous = IntPtr.Zero;
            foreach (var window in args.Contains("--observe") ? Observe(windows) : args.Contains("--stress") ? Enumerable.Range(0, 4).SelectMany(_ => windows) : windows)
            {
                int pid = (int)window.GetType().GetProperty("ProcessId")!.GetValue(window)!;
                IntPtr hwnd = (IntPtr)window.GetType().GetProperty("Hwnd")!.GetValue(window)!;
                string? home = (string?)resolverType.GetMethod("ResolveHome")!.Invoke(resolver, [pid]);
                string? active = (string?)resolverType.GetMethod("FindActiveProfile")!.Invoke(resolver, [pid, profiles]);
                bool? follows = null;
                bool? foreground = null;
                bool? nativeVisible = null, minimized = null;
                bool? onTop = null;
                if (args.Contains("--verify-follow") || args.Contains("--observe"))
                {
                    if (!args.Contains("--observe"))
                    {
                    if (args.Contains("--minimize-stress") && previous != IntPtr.Zero && previous != hwnd) ShowWindow(previous, 6);
                    ShowWindow(hwnd, 9);
                    Thread.Sleep(200);
                    uint foregroundThread = GetWindowThreadProcessId(GetForegroundWindow(), out _);
                    uint probeThread = GetCurrentThreadId();
                    bool joined = foregroundThread != probeThread && AttachThreadInput(probeThread, foregroundThread, true);
                    try
                    {
                        SwitchToThisWindow(hwnd, true);
                        SetForegroundWindow(hwnd);
                        previous = hwnd;
                    }
                    finally { if (joined) AttachThreadInput(probeThread, foregroundThread, false); }
                    Thread.Sleep(1700);
                    }
                    var overlayIds = Process.GetProcessesByName("CodexProfileOverlay").Select(p => (uint)p.Id).ToHashSet();
                    bool found = false;
                    EnumWindows((candidate, _) =>
                    {
                        GetWindowThreadProcessId(candidate, out uint ownerPid);
                        if (overlayIds.Contains(ownerPid) && IsWindowVisible(candidate)
                            && GetWindow(candidate, 4) == hwnd)
                        {
                            nativeVisible = true;
                            minimized = IsIconic(candidate);
                            if (GetWindowRect(candidate, out var rect))
                                onTop = GetAncestor(WindowFromPoint(new NativePoint { X = (rect.Left + rect.Right) / 2, Y = (rect.Top + rect.Bottom) / 2 }), 2) == candidate;
                            found = !minimized.Value && onTop == true;
                        }
                        return true;
                    }, IntPtr.Zero);
                    foreground = GetForegroundWindow() == hwnd;
                    follows = found && foreground.Value;
                }
                GetWindowThreadProcessId(GetForegroundWindow(), out uint foregroundPid);
                results.Add(new { pid, enabled = IsWindowEnabled(hwnd), iconic = IsIconic(hwnd), popup = GetLastActivePopup(hwnd).ToInt64(), hwnd = hwnd.ToInt64(), foregroundPid, resolvedHome = home is not null, accountMatched = active is not null, foreground, nativeVisible, minimized, onTop, follows });
                passed &= home is not null && active is not null && follows != false;
            }
        }
        finally
        {
            if (args.Contains("--stress")) foreach (var state in initialStates) ShowWindow(state.Hwnd, state.Minimized ? 6 : 9);
            if (args.Contains("--verify-follow")) SwitchToThisWindow(original, true);
        }
        File.WriteAllText(output, JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine(JsonSerializer.Serialize(results));
        return passed && results.Count > 0 ? 0 : 1;
    }

    private static IEnumerable<object> Observe(object[] windows)
    {
        for (int sample = 0; sample < 40; sample++)
        {
            Thread.Sleep(750);
            IntPtr foreground = GetForegroundWindow();
            object? window = windows.FirstOrDefault(w => (IntPtr)w.GetType().GetProperty("Hwnd")!.GetValue(w)! == foreground);
            if (window is not null) yield return window;
        }
    }
}
