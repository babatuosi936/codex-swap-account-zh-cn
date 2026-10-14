using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using CodexProfileOverlay.Core.Models;
using CodexProfileOverlay.Core.Services;

internal static partial class Program
{
    private static void RunReturnToTopScenario()
    {
        foreach (double scale in new[] { 0.8, 1.0, 1.4 })
        {
            var host = new Window { WindowStyle = WindowStyle.None, Width = 1200, Height = 700, Left = 30, Top = 30, ShowInTaskbar = false };
            var settings = new OverlaySettings { DisplayMode = OverlayDisplayMode.Expanded, PositionPreset = PositionPreset.Custom, OffsetX = 120, OffsetY = 100, Scale = scale, ShowAutomaticLimitIndicators = true };
            var overlay = (Window)Activator.CreateInstance(OverlayType, settings, new SafeLogger(System.IO.Path.Combine(AppContext.BaseDirectory, "fixture-logs")))!;
            try
            {
                host.Show(); Pump();
                var profiles = new[] { "181账号", "475账号", "bab", "199账号" }.Select((name, i) => new ProfileInfo("return-top-" + i, "unused", "unused") { DisplayName = name }).ToArray();
                Call(overlay, "SetProfiles", profiles, profiles[1].Name);
                Call(overlay, "SetStatusDocument", Document(profiles, 0), null);
                var owner = new WindowInteropHelper(host).Handle;
                Call(overlay, "AttachTo", owner); Call(overlay, "UpdatePlacement", owner); Pump();
                var client = CaptionFixtureClient(overlay, owner);
                var caption = (OverlayCaptionBounds?)OverlayType.GetMethod("GetCaptionButtonsBounds", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(overlay, [owner, client]);
                double wideWidth = overlay.ActualWidth;
                Require(wideWidth > caption!.Left - 8 - 340, "Fixture must be wider than the protected title-row slot.");
                OverlayType.GetProperty("OnSettingsChanged")!.SetValue(overlay, (Action<OverlaySettings>)(_ =>
                {
                    Call(overlay, "ApplySettings"); Call(overlay, "UpdatePlacement", owner);
                }));
                Call(overlay, "BeginHeaderDrag");
                Call(overlay, "MoveHeaderDrag", client, new System.Windows.Point(100, 0), caption); Pump();
                Require(Math.Abs(overlay.Top - client.Top) < 1 && Math.Abs(overlay.Left - client.Left - 340) < 1,
                    "A wide lower bar must return to the protected title row instead of being blocked.");
                AssertCaptionClear(overlay, owner);
                double dockedWidth = overlay.ActualWidth;
                Require(dockedWidth < wideWidth - 10, "Returning to the title row must fit the available slot once.");
                foreach (var offset in new[] { new System.Windows.Point(900, 0), new System.Windows.Point(100, 0), new System.Windows.Point(100, 120), new System.Windows.Point(100, 0) })
                {
                    if (offset.Y >= 48)
                    {
                        var before = ((Grid)((Border)overlay.Content).Child).Children.OfType<ScrollViewer>().Single();
                        before.ScrollToHorizontalOffset(90); Pump();
                        // Traverse the caption's safety gap a few pixels at a
                        // time, as a real pointer does, rather than jumping out.
                        Call(overlay, "MoveHeaderDrag", client,
                            new System.Windows.Point(100, caption.Top + caption.Height + OverlayLayoutService.CaptionSafetyGap - 2), caption);
                        Pump();
                    }
                    Call(overlay, "MoveHeaderDrag", client, offset, caption); Pump();
                    if (offset.Y >= 48)
                    {
                        Require(Math.Abs(overlay.ActualWidth - wideWidth) < 1, "Dragging below the title row must restore full account width.");
                        var scroll = ((Grid)((Border)overlay.Content).Child).Children.OfType<ScrollViewer>().Single();
                        Require(scroll.HorizontalOffset < 1, "Undocking must return account content to the beginning.");
                        if (wideWidth < client.Width - 1) AssertExpandedProfilesFit(overlay);
                        Call(overlay, "MoveHeaderDrag", client, new System.Windows.Point(250, 150), caption); Pump();
                        Require(Math.Abs(overlay.ActualWidth - wideWidth) < 1, "Further movement below must keep the full width fixed.");
                    }
                    else Require(Math.Abs(overlay.ActualWidth - dockedWidth) < 1, "Movement in the title row must retain the docked width.");
                }
                Call(overlay, "FinishDrag"); Pump(); Call(overlay, "UpdatePlacement", owner); Pump();
                Require(Math.Abs(overlay.Top - client.Top) < 1 && Math.Abs(overlay.ActualWidth - dockedWidth) < 1,
                    "Dropping and saving at the top must retain both the position and fitted width.");
                // Reproduce a new gesture after being undocked again.
                Call(overlay, "BeginHeaderDrag");
                Call(overlay, "MoveHeaderDrag", client, new System.Windows.Point(150, 120), caption);
                Call(overlay, "FinishDrag"); Pump();
                Require(Math.Abs(overlay.ActualWidth - wideWidth) < 1, "Dropping below must retain the restored account width.");
                Call(overlay, "BeginHeaderDrag");
                Call(overlay, "MoveHeaderDrag", client, new System.Windows.Point(100, 0), caption);
                Call(overlay, "FinishDrag"); Pump();
                Require(Math.Abs(overlay.Top - client.Top) < 1 && Math.Abs(overlay.ActualWidth - dockedWidth) < 1,
                    "Repeated drag-down and drag-back gestures must apply the correct width in each region.");
                Evidence.Add(new { scenario = "wide-bar-return-to-title-row", scale, wideWidth, dockedWidth });
            }
            finally { overlay.Close(); host.Close(); Pump(); }
        }
        RunUndockAutoScenario();
    }

    private static void RunUndockAutoScenario()
    {
        var host = new Window { WindowStyle = WindowStyle.None, Width = 2200, Height = 700, ShowInTaskbar = false };
        var settings = new OverlaySettings { DisplayMode = OverlayDisplayMode.Auto, PositionPreset = PositionPreset.Custom, OffsetX = 1800, OffsetY = 0, ShowAutomaticLimitIndicators = true };
        var overlay = (Window)Activator.CreateInstance(OverlayType, settings, new SafeLogger(System.IO.Path.Combine(AppContext.BaseDirectory, "fixture-logs")))!;
        try
        {
            host.Show(); Pump();
            var profiles = Enumerable.Range(0, 4).Select(i => new ProfileInfo("undock-auto-" + i, "unused", "unused") { DisplayName = "Account " + i }).ToArray();
            Call(overlay, "SetProfiles", profiles, profiles[0].Name);
            Call(overlay, "SetStatusDocument", Document(profiles, 0), null);
            var owner = new WindowInteropHelper(host).Handle;
            Call(overlay, "AttachTo", owner); Call(overlay, "UpdatePlacement", owner); Pump();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            Require((OverlayDisplayMode)OverlayType.GetField("currentMode", flags)!.GetValue(overlay)! == OverlayDisplayMode.Compact,
                "Fixture must start compact in the narrow top slot.");
            var client = CaptionFixtureClient(overlay, owner);
            var caption = (OverlayCaptionBounds?)OverlayType.GetMethod("GetCaptionButtonsBounds", flags)!.Invoke(overlay, [owner, client]);
            Call(overlay, "BeginHeaderDrag");
            Call(overlay, "MoveHeaderDrag", client, new System.Windows.Point(200, 120), caption); Pump();
            Call(overlay, "FinishDrag"); Call(overlay, "UpdatePlacement", owner); Pump();
            AssertExpandedProfilesFit(overlay);
            Require(Descendants(overlay.Content as DependencyObject ?? overlay).OfType<System.Windows.Controls.Button>().Count(button => button.Tag is string) == 4,
                "Undocking Auto mode must show every account rather than retaining a one-account compact button.");
            Evidence.Add(new { scenario = "undock-auto-shows-all-accounts" });
        }
        finally { overlay.Close(); host.Close(); Pump(); }
    }
}
