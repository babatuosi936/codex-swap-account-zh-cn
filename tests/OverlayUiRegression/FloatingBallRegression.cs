using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using CodexProfileOverlay.Core.Models;
using CodexProfileOverlay.Core.Services;

internal static partial class Program
{
    private static void RunFloatingBallScenario()
    {
        foreach (double scale in new[] { 0.8, 1.0, 1.4 })
        foreach (PositionPreset preset in new[] { PositionPreset.Custom, PositionPreset.TopCenter })
        {
            var settings = new OverlaySettings { Scale = scale, PositionPreset = preset, OffsetX = 100, OffsetY = 50, DisplayMode = OverlayDisplayMode.Expanded };
            var host = new Window { Width = 1400, Height = 800, Left = 20, Top = 20 };
            var overlay = (Window)Activator.CreateInstance(OverlayType, settings, new SafeLogger(System.IO.Path.Combine(AppContext.BaseDirectory, "fixture-logs")))!;
            try
            {
                host.Show(); Pump();
                var owner = new WindowInteropHelper(host).Handle;
                Call(overlay, "AttachTo", owner); Call(overlay, "UpdatePlacement", owner); Pump(); Call(overlay, "UpdatePlacement", owner); Pump();
                double left = overlay.Left, top = overlay.Top, width = overlay.ActualWidth;
                double x = settings.OffsetX, y = settings.OffsetY;
                Call(overlay, "MinimizeToBall"); Pump();
                Require(overlay.IsVisible && Math.Abs(overlay.ActualWidth - 32 * scale) < 1 && Math.Abs(overlay.ActualHeight - 32 * scale) < 1, "Minimize must retain a visible small round surface.");
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                OverlayType.GetField("ballOffset", flags)!.SetValue(overlay, new System.Windows.Point(300, 200));
                Call(overlay, "UpdatePlacement", owner); Pump();
                Require(Math.Abs(overlay.Left - left) > 10 || Math.Abs(overlay.Top - top) > 10, "The ball must have independent movement.");
                Call(overlay, "ApplySettings"); Call(overlay, "UpdatePlacement", owner); Pump();
                Require(overlay.ActualWidth < 50 && settings.PositionPreset == preset && settings.OffsetX == x && settings.OffsetY == y, "Refreshing the minimized UI must preserve normal placement and ball mode.");
                Call(overlay, "RestoreFromBall"); Pump();
                Require(Math.Abs(overlay.Left - left) < 1 && Math.Abs(overlay.Top - top) < 1 && Math.Abs(overlay.ActualWidth - width) < 1, $"Restore mismatch scale={scale} preset={preset}: {left},{top},{width} -> {overlay.Left},{overlay.Top},{overlay.ActualWidth}");
                Evidence.Add(new { scenario = "floating-ball-independent-placement", scale, preset = preset.ToString() });
            }
            finally { overlay.Close(); host.Close(); Pump(); }
        }
    }
}



