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

            RunCustomPositionRetentionScenario();
        }
        finally { overlay.Close(); foreach (var host in hosts) host.Close(); Pump(); }
    }

    private static void RunCustomPositionRetentionScenario()
    {
        var settings = new OverlaySettings { PositionPreset = PositionPreset.Custom, OffsetX = 324.5, OffsetY = 180, DisplayMode = OverlayDisplayMode.Expanded };
        var host = new Window { Width = 1800, Height = 700, Left = 20, Top = 20 };
        // Retention below the title row is independent of the top menu boundary.
        var smallHost = new Window { Width = 400, Height = 200, Left = 50, Top = 50 };
        var overlay = (Window)Activator.CreateInstance(OverlayType, settings, new SafeLogger(System.IO.Path.Combine(AppContext.BaseDirectory, "fixture-logs")))!;
        try
        {
            host.Show(); Pump();
            var owner = new WindowInteropHelper(host).Handle;
            Call(overlay, "AttachTo", owner);
            Call(overlay, "UpdatePlacement", owner); Pump();
            Call(overlay, "UpdatePlacement", owner); Pump();
            double originalLeft = overlay.Left, originalTop = overlay.Top;

            for (int i = 0; i < 3; i++)
            {
                host.Width = 400; host.Height = 200; Pump();
                Call(overlay, "UpdatePlacement", owner); Pump();
                Require(settings.OffsetX == 324.5 && settings.OffsetY == 180, "Clamping to a smaller owner must preserve requested offsets.");
                Require(overlay.Left < originalLeft && overlay.Top < originalTop, "Fixture must temporarily clamp both axes.");
                Call(overlay, "ApplySettings");
                Call(overlay, "UpdatePlacement", owner); Pump();
                Require(settings.OffsetX == 324.5 && settings.OffsetY == 180, "Refreshing a clamped layout must not overwrite saved position.");

                host.Width = 1800; host.Height = 700; Pump();
                Call(overlay, "UpdatePlacement", owner); Pump();
                Require(Math.Abs(overlay.Left - originalLeft) < 1 && Math.Abs(overlay.Top - originalTop) < 1, "Restoring owner size must restore the requested position on both axes.");
                Evidence.Add(new { scenario = "custom-position-resize-retention", cycle = i });
            }

            smallHost.Show(); Pump();
            var smallOwner = new WindowInteropHelper(smallHost).Handle;
            Call(overlay, "AttachTo", smallOwner);
            Call(overlay, "UpdatePlacement", smallOwner); Pump();
            Require(settings.OffsetX == 324.5 && settings.OffsetY == 180, "A smaller independent instance must not overwrite shared placement preferences.");
            Call(overlay, "AttachTo", owner);
            Call(overlay, "UpdatePlacement", owner); Pump();
            Require(Math.Abs(overlay.Left - originalLeft) < 1 && Math.Abs(overlay.Top - originalTop) < 1, "Returning from a smaller instance must restore the original position.");
            Evidence.Add(new { scenario = "custom-position-instance-retention" });
        }
        finally { overlay.Close(); smallHost.Close(); host.Close(); Pump(); }
    }
}
