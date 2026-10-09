using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using CodexProfileOverlay.Core.Models;
using CodexProfileOverlay.Core.Services;

internal static partial class Program
{
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hwnd, int command);

    private static void RunOwnerVisibilityScenario()
    {
        var hosts = new[] { new Window { Width = 1200, Height = 600, Left = 20, Top = 20 }, new Window { Width = 1200, Height = 600, Left = 20, Top = 20 } };
        var overlay = (Window)Activator.CreateInstance(OverlayType, new OverlaySettings { ShowAutomaticallyWhenCodexOpens = true }, new SafeLogger(System.IO.Path.Combine(AppContext.BaseDirectory, "fixture-logs")))!;
        try
        {
            foreach (var host in hosts) host.Show();
            Pump();
            for (int i = 0; i < 8; i++)
            {
                var owner = new WindowInteropHelper(hosts[i % 2]).Handle;
                Call(overlay, "AttachTo", owner);
                Call(overlay, "UpdatePlacement", owner);
                Pump();
                var handle = new WindowInteropHelper(overlay).Handle;
                // Hide only the native HWND to reproduce managed/native disagreement.
                ShowWindow(handle, 0);
                Require(overlay.IsVisible && !IsWindowVisible(handle), "Fixture must preserve WPF visibility while hiding the native HWND.");
                Call(overlay, "UpdatePlacement", owner);
                Pump();
                Require(overlay.IsVisible && IsWindowVisible(handle) && !IsIconic(handle), "Unchanged placement must recover a natively hidden overlay.");
                Evidence.Add(new { scenario = "owner-native-visibility-recovery", cycle = i });
            }
            OverlayType.GetProperty("AllowAutoShow")!.SetValue(overlay, false);
            var hiddenHandle = new WindowInteropHelper(overlay).Handle;
            ShowWindow(hiddenHandle, 0);
            Call(overlay, "UpdatePlacement", new WindowInteropHelper(hosts[1]).Handle);
            Require(!IsWindowVisible(hiddenHandle), "Recovery must respect the controller's hide decision.");
            Evidence.Add(new { scenario = "owner-hidden-gate" });
        }
        finally { overlay.Close(); foreach (var host in hosts) host.Close(); Pump(); }
    }
}
