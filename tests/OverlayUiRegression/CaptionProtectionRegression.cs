using System.Reflection;
using System.Windows;
using System.Windows.Interop;
using CodexProfileOverlay.Core.Models;
using CodexProfileOverlay.Core.Services;

internal static partial class Program
{
    private static void RunCaptionProtectionScenario()
    {
        foreach (double scale in new[] { 0.8, 1.0, 1.4 })
        foreach (var mode in new[] { OverlayDisplayMode.Expanded, OverlayDisplayMode.Auto })
        {
            // Electron-style client area includes the title row. No real Codex
            // accounts or windows are involved in this native placement fixture.
            var host = new Window { WindowStyle = WindowStyle.None, Width = 2200, Height = 700, Left = 40, Top = 40, ShowInTaskbar = false };
            var settings = new OverlaySettings { DisplayMode = mode, Scale = scale, PositionPreset = PositionPreset.AfterMenu, OffsetX = 340, OffsetY = 0 };
            var overlay = (Window)Activator.CreateInstance(OverlayType, settings, new SafeLogger(System.IO.Path.Combine(AppContext.BaseDirectory, "fixture-logs")))!;
            try
            {
                host.Show(); Pump();
                ProfileInfo[] profiles = [new("caption-main", "unused", "unused") { DisplayName = "181账号", Initials = "1" }, new("caption-two", "unused", "unused") { DisplayName = "475账号", Initials = "4" }, new("caption-three", "unused", "unused") { DisplayName = "199账号", Initials = "1" }];
                Call(overlay, "SetProfiles", profiles, profiles[1].Name);
                Call(overlay, "SetStatusDocument", Document(profiles, 0), null);
                var owner = new WindowInteropHelper(host).Handle;
                Call(overlay, "AttachTo", owner); Call(overlay, "UpdatePlacement", owner); Pump();
                AssertExpandedProfilesFit(overlay);
                AssertCaptionClear(overlay, owner);
                double expandedWidth = overlay.ActualWidth;

                host.Width = 340 + expandedWidth + 120;
                Pump(); Call(overlay, "UpdatePlacement", owner); Pump();
                AssertCaptionClear(overlay, owner);
                var client = CaptionFixtureClient(overlay, owner);
                if (mode == OverlayDisplayMode.Expanded)
                {
                    Require(overlay.Top - client.Top >= 48, "An expanded row that reaches caption buttons must move below them.");
                    AssertExpandedProfilesFit(overlay);
                }
                else
                {
                    Require((OverlayDisplayMode)OverlayType.GetField("currentMode", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(overlay)! == OverlayDisplayMode.Compact,
                        "Auto mode must account for the menu and caption buttons, not just the full client width.");
                    Require(Math.Abs(overlay.Top - client.Top) < 1, "A compact bar that fits must retain the menu-row position.");
                }
                Require(settings.PositionPreset == PositionPreset.AfterMenu && settings.OffsetX == 340 && settings.OffsetY == 0,
                    "Temporary caption avoidance must not change saved placement.");

                // A data refresh may enlarge the bar without an owner-resize event.
                Call(overlay, "SetStatusDocument", Document(profiles, 1), null); Pump();
                AssertCaptionClear(overlay, owner);
                Call(overlay, "MinimizeToBall"); Pump();
                OverlayType.GetField("ballOffset", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(overlay, new System.Windows.Point(host.Width - 32, 0));
                Call(overlay, "UpdatePlacement", owner); Pump();
                AssertCaptionClear(overlay, owner);
                Require(overlay.IsVisible && overlay.ActualWidth < 50, "The minimized ball must stay visible outside the caption area.");
                Call(overlay, "RestoreFromBall"); Pump();
                AssertCaptionClear(overlay, owner);

                host.Width = 2200;
                Pump(); Call(overlay, "UpdatePlacement", owner); Pump();
                AssertExpandedProfilesFit(overlay);
                AssertCaptionClear(overlay, owner);
                client = CaptionFixtureClient(overlay, owner);
                Require(Math.Abs(overlay.Left - client.Left - 340) < 1 && Math.Abs(overlay.Top - client.Top) < 1,
                    "Growing the owner must restore the saved menu anchor automatically.");

                settings.PositionPreset = PositionPreset.Custom;
                settings.OffsetX = host.Width - 100; settings.OffsetY = 0;
                Call(overlay, "ApplySettings"); Pump();
                AssertCaptionClear(overlay, owner);
                Require(settings.OffsetX == host.Width - 100 && settings.OffsetY == 0, "Custom safety clamping must preserve the requested coordinates.");
                Evidence.Add(new { scenario = "caption-buttons-protected", scale, mode = mode.ToString(), expandedWidth });
            }
            finally { overlay.Close(); host.Close(); Pump(); }
        }
    }

    private static Rect CaptionFixtureClient(Window overlay, IntPtr owner)
    {
        object?[] arguments = [owner, Rect.Empty];
        var result = (bool)OverlayType.GetMethod("TryGetClientBounds", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(overlay, arguments)!;
        Require(result, "Fixture client bounds must be readable.");
        return (Rect)arguments[1]!;
    }

    private static void AssertCaptionClear(Window overlay, IntPtr owner)
    {
        var client = CaptionFixtureClient(overlay, owner);
        var caption = (OverlayCaptionBounds?)OverlayType.GetMethod("GetCaptionButtonsBounds", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(overlay, [owner, client]);
        Require(caption is { IsValid: true }, "Borderless title-row fixture must have a protected caption area.");
        Require(overlay.IsVisible && !OverlayLayoutService.OverlapsCaption(new(overlay.Left - client.Left, overlay.Top - client.Top), overlay.ActualWidth, overlay.ActualHeight, caption),
            $"Overlay overlaps caption controls: {overlay.Left - client.Left},{overlay.Top - client.Top},{overlay.ActualWidth},{overlay.ActualHeight}; caption={caption}.");
    }
}
