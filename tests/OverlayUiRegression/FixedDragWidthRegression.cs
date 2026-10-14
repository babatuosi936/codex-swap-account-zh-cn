using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using CodexProfileOverlay.Core.Models;
using CodexProfileOverlay.Core.Services;

internal static partial class Program
{
    private static void RunFixedDragWidthScenario()
    {
        foreach (double scale in new[] { 0.8, 1.0, 1.4 })
        foreach (var mode in new[] { OverlayDisplayMode.Expanded, OverlayDisplayMode.Auto })
        {
            var host = new Window { WindowStyle = WindowStyle.None, Width = 1200, Height = 700, Left = 30, Top = 30, ShowInTaskbar = false };
            var settings = new OverlaySettings { DisplayMode = mode, PositionPreset = PositionPreset.Custom, OffsetX = 550, OffsetY = 0, Scale = scale, ShowAutomaticLimitIndicators = true };
            var overlay = (Window)Activator.CreateInstance(OverlayType, settings, new SafeLogger(System.IO.Path.Combine(AppContext.BaseDirectory, "fixture-logs")))!;
            try
            {
                host.Show(); Pump();
                var profiles = new[] { "181账号", "475账号", "bab", "199账号" }.Select((name, i) => new ProfileInfo("fixed-drag-" + i, "unused", "unused") { DisplayName = name }).ToArray();
                Call(overlay, "SetProfiles", profiles, profiles[1].Name);
                var document = Document(profiles, 0);
                Call(overlay, "SetStatusDocument", document, null);
                var owner = new WindowInteropHelper(host).Handle;
                Call(overlay, "AttachTo", owner); Call(overlay, "UpdatePlacement", owner); Pump();
                double width = overlay.ActualWidth;
                double height = overlay.ActualHeight;
                var client = CaptionFixtureClient(overlay, owner);
                var caption = (OverlayCaptionBounds?)OverlayType.GetMethod("GetCaptionButtonsBounds", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(overlay, [owner, client]);
                var currentMode = OverlayType.GetField("currentMode", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(overlay);
                // Drive the same gesture methods used after the native mouse
                // threshold, without moving the user's real pointer.
                Call(overlay, "BeginHeaderDrag");
                foreach (var offset in new[] { new System.Windows.Point(100, 0), new System.Windows.Point(900, 0), new System.Windows.Point(500, 0), new System.Windows.Point(100, 0) })
                {
                    Call(overlay, "MoveHeaderDrag", client, offset, caption); Pump();
                    Require(Math.Abs(overlay.ActualWidth - width) < 1 && Math.Abs(overlay.ActualHeight - height) < 1,
                        "Dragging within the title row must never resize the whole bar.");
                    AssertCaptionClear(overlay, owner);
                    if (offset.Y == 0) Require(overlay.Left - client.Left >= 339.5, "Fixed-width dragging must retain the X=340 boundary.");
                }
                OverlayType.GetProperty("OnSettingsChanged")!.SetValue(overlay, (Action<OverlaySettings>)(_ =>
                {
                    Call(overlay, "ApplySettings"); Call(overlay, "UpdatePlacement", owner);
                }));
                Call(overlay, "FinishDrag"); Pump();
                for (int tick = 0; tick < 3; tick++) { Call(overlay, "UpdatePlacement", owner); Pump(); }
                Require(Math.Abs(overlay.ActualWidth - width) < 1 && Equals(currentMode, OverlayType.GetField("currentMode", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(overlay)),
                    "Release, settings save and tracking ticks must retain the dragged width and display mode.");
                var refresh = Document(profiles, 0);
                foreach (var snapshot in refresh.Snapshots.Values) snapshot.CapturedAt = DateTimeOffset.UtcNow.AddSeconds(2);
                Call(overlay, "SetStatusDocument", refresh, null); Pump();
                Require(Math.Abs(overlay.ActualWidth - width) < 1, "A timestamp-only quota refresh must not unlock the drag width.");
                host.Width = 2800; Pump(); Call(overlay, "UpdatePlacement", owner); Pump();
                AssertExpandedProfilesFit(overlay);
                Require(overlay.ActualWidth > width + 10, "An owner resize must still let the bar adapt to available space.");
                Evidence.Add(new { scenario = "fixed-header-drag-width", scale, mode, width });
            }
            finally { overlay.Close(); host.Close(); Pump(); }
        }
    }
}
