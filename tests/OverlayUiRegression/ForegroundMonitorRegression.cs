using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using CodexProfileOverlay.Core.Services;

internal static partial class Program
{
    [DllImport("user32.dll")] private static extern void NotifyWinEvent(uint eventType, IntPtr hwnd, int objectId, int childId);

    private static void RunForegroundMonitorScenario()
    {
        var host = new Window { Width = 500, Height = 300, ShowInTaskbar = false };
        var monitorType = typeof(CodexProfileOverlay.App).Assembly.GetType("CodexProfileOverlay.ForegroundWindowMonitor")!;
        int refreshes = 0;
        double? elapsed = null;
        var stopwatch = new Stopwatch();
        using var monitor = (IDisposable)Activator.CreateInstance(monitorType, Dispatcher.CurrentDispatcher,
            new Action(() => { refreshes++; if (stopwatch.IsRunning) elapsed = stopwatch.Elapsed.TotalMilliseconds; }),
            new SafeLogger(System.IO.Path.Combine(AppContext.BaseDirectory, "fixture-logs")))!;
        try
        {
            host.Show();
            Pump();
            monitorType.GetMethod("Start")!.Invoke(monitor, null);
            Pump();
            foreach (uint windowEvent in new uint[] { 0x0003, 0x0017 })
            {
                int previous = refreshes;
                elapsed = null;
                stopwatch.Restart();
                NotifyWinEvent(windowEvent, new WindowInteropHelper(host).Handle, 0, 0);
                WaitFor(() => refreshes > previous);
                stopwatch.Stop();
                Require(refreshes > previous && elapsed < 500, "Window event must refresh promptly without waiting for the 750ms tracking timer.");
                Evidence.Add(new { scenario = "window-event-response", windowEvent, milliseconds = elapsed });
            }
            monitor.Dispose();
            int before = refreshes;
            NotifyWinEvent(0x0017, new WindowInteropHelper(host).Handle, 0, 0);
            Pump();
            Require(refreshes == before, "Disposed window hooks must not refresh the controller.");
            Evidence.Add(new { scenario = "window-event-disposal" });
        }
        finally { host.Close(); Pump(); }
    }
}
