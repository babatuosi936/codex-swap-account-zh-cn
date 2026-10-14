using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using CodexProfileOverlay.Core.Models;
using CodexProfileOverlay.Core.Services;

internal static partial class Program
{
    private static void RunBoundedAccountRowScenario()
    {
        foreach (double scale in new[] { 0.8, 1.0, 1.4 })
        foreach (PositionPreset preset in new[] { PositionPreset.AfterMenu, PositionPreset.Custom })
        {
            var host = new Window { WindowStyle = WindowStyle.None, Width = 1000, Height = 700, Left = 30, Top = 30, ShowInTaskbar = false };
            var settings = new OverlaySettings { DisplayMode = OverlayDisplayMode.Expanded, PositionPreset = preset, OffsetX = preset == PositionPreset.Custom ? 20 : 340, OffsetY = 0, Scale = scale, Language = LanguagePreference.ChineseSimplified, ShowAutomaticLimitIndicators = true };
            var overlay = (Window)Activator.CreateInstance(OverlayType, settings, new SafeLogger(System.IO.Path.Combine(AppContext.BaseDirectory, "fixture-logs")))!;
            try
            {
                var localizerType = typeof(CodexProfileOverlay.App).Assembly.GetType("CodexProfileOverlay.Localizer")!;
                OverlayType.GetProperty("Localizer")!.SetValue(overlay, Activator.CreateInstance(localizerType, settings.Language));
                host.Show(); Pump();
                var profiles = new[] { "181账号", "475账号", "bab", "199账号" }.Select((name, index) => new ProfileInfo("bounded-" + index, "unused", "unused") { DisplayName = name, Initials = name[..1] }).ToArray();
                Call(overlay, "SetProfiles", profiles, profiles[1].Name);
                Call(overlay, "SetStatusDocument", Document(profiles, 0), null);
                var owner = new WindowInteropHelper(host).Handle;
                Call(overlay, "AttachTo", owner); Call(overlay, "UpdatePlacement", owner); Pump();
                var client = CaptionFixtureClient(overlay, owner);
                Require(overlay.IsVisible && Math.Abs(overlay.Left - client.Left - 340) < 1 && Math.Abs(overlay.Top - client.Top) < 1,
                    "Top bar must stop at X=340 and remain in the title row for both menu and dragged/custom placement.");
                AssertCaptionClear(overlay, owner);
                var shell = (Border)overlay.Content;
                var grid = (Grid)shell.Child;
                var overview = grid.Children.OfType<System.Windows.Controls.Button>().Single(button => Grid.GetColumn(button) == 0);
                var menu = grid.Children.OfType<System.Windows.Controls.Button>().Single(button => Grid.GetColumn(button) == 2);
                var scroll = grid.Children.OfType<ScrollViewer>().Single();
                Require(scroll.ScrollableWidth > 20 && scroll.ClipToBounds, $"Middle account viewport must scroll and clip overflowing accounts: scale={scale}, width={overlay.ActualWidth}, viewport={scroll.ActualWidth}, overflow={scroll.ScrollableWidth}.");
                Require(scroll.HorizontalScrollBarVisibility == ScrollBarVisibility.Hidden && scroll.VerticalScrollBarVisibility == ScrollBarVisibility.Disabled,
                    "No scrollbars may be displayed below or beside the account row.");
                Require(!Descendants(scroll).OfType<System.Windows.Controls.Primitives.ScrollBar>().Any(bar => bar.IsVisible), "Hidden scrolling must reserve no visible progress bar.");
                Rect Bounds(FrameworkElement element) => element.TransformToAncestor(shell).TransformBounds(new Rect(element.RenderSize));
                var overviewBefore = Bounds(overview); var menuBefore = Bounds(menu);
                var wheel = new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, -120) { RoutedEvent = Mouse.PreviewMouseWheelEvent };
                scroll.RaiseEvent(wheel); Pump();
                Require(wheel.Handled && scroll.HorizontalOffset > 0, "Normal mouse wheel must scroll accounts horizontally without Shift.");
                Require(Bounds(overview) == overviewBefore && Bounds(menu) == menuBefore, "Overview and menu endpoints must stay fixed when accounts scroll.");
                double offsetBeforeRefresh = scroll.HorizontalOffset;
                Call(overlay, "SetStatusDocument", Document(profiles, 1), null); Pump();
                scroll = ((Grid)((Border)overlay.Content).Child).Children.OfType<ScrollViewer>().Single();
                Require(Math.Abs(scroll.HorizontalOffset - offsetBeforeRefresh) < 1, "Quota refresh must preserve the visible horizontal account position.");
                for (int step = 0; step < 20; step++)
                {
                    scroll.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, -120) { RoutedEvent = Mouse.PreviewMouseWheelEvent });
                    Pump();
                }
                Pump();
                var last = Descendants(scroll).OfType<System.Windows.Controls.Button>().Single(button => Equals(button.Tag, profiles[^1].Name));
                var lastBounds = last.TransformToAncestor(scroll).TransformBounds(new Rect(last.RenderSize));
                Require(lastBounds.Right <= scroll.ActualWidth + 1 && Math.Abs(scroll.HorizontalOffset - scroll.ScrollableWidth) < 1,
                    "Wheel scrolling must reach the final account's right edge.");
                scroll.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, 120) { RoutedEvent = Mouse.PreviewMouseWheelEvent }); Pump();
                Require(scroll.HorizontalOffset < scroll.ScrollableWidth, "Reverse wheel motion must scroll back towards earlier accounts.");

                host.Width = 2800; Pump(); Call(overlay, "UpdatePlacement", owner); Pump();
                AssertExpandedProfilesFit(overlay);
                Require(Math.Abs(overlay.Left - CaptionFixtureClient(overlay, owner).Left - 340) < 1,
                    "Growing a window must restore full content width without shifting the minimum anchor.");
                Require(settings.OffsetX == (preset == PositionPreset.Custom ? 20 : 340) && settings.OffsetY == 0,
                    "Temporary bounds must not rewrite saved placement.");
                Evidence.Add(new { scenario = "bounded-account-row", scale, preset, wheelOffset = offsetBeforeRefresh });
            }
            finally { overlay.Close(); host.Close(); Pump(); }
        }
    }
}
