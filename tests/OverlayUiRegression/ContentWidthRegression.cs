using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using CodexProfileOverlay.Core.Models;
using CodexProfileOverlay.Core.Services;

internal static partial class Program
{
    private static void RunContentWidthScenario()
    {
        foreach (double scale in new[] { 0.8, 1.0, 1.4 })
        foreach (bool quota in new[] { false, true })
        {
            var host = new Window { Width = 2200, Height = 700, ShowInTaskbar = false };
            var settings = new OverlaySettings { DisplayMode = OverlayDisplayMode.Expanded, Scale = scale, ShowAutomaticLimitIndicators = quota, Language = LanguagePreference.ChineseSimplified, PositionPreset = PositionPreset.TopCenter };
            var overlay = (Window)Activator.CreateInstance(OverlayType, settings, new SafeLogger(System.IO.Path.Combine(AppContext.BaseDirectory, "fixture-logs")))!;
            try
            {
                var localizerType = typeof(CodexProfileOverlay.App).Assembly.GetType("CodexProfileOverlay.Localizer")!;
                OverlayType.GetProperty("Localizer")!.SetValue(overlay, Activator.CreateInstance(localizerType, settings.Language));
                host.Show(); Pump();
                var owner = new WindowInteropHelper(host).Handle;
                Call(overlay, "AttachTo", owner);
                var profiles = new[] { "181账号", "199账号", "475账号" }.Select((name, index) => new ProfileInfo("content-" + index, "unused", "unused") { DisplayName = name, Initials = name[..1] }).ToArray();
                double previousWidth = double.PositiveInfinity;
                foreach (int count in new[] { 3, 2, 1, 0 })
                {
                    var visible = profiles.Take(count).ToArray();
                    Call(overlay, "SetProfiles", visible, visible.FirstOrDefault()?.Name);
                    var document = Document(visible, 0);
                    foreach (var profile in visible)
                        document.Snapshots[profile.Name] = new UsageSnapshot { LongWindowRemainingPercent = 78, PlanName = "plus", CapturedAt = DateTimeOffset.UtcNow };
                    Call(overlay, "SetStatusDocument", document, null);
                    Call(overlay, "UpdatePlacement", owner); Pump();
                    AssertNoExpandedTrailingSpace(overlay);
                    Require(overlay.ActualWidth < previousWidth - 10 * scale, "Removing an account must immediately shrink the expanded overlay.");
                    previousWidth = overlay.ActualWidth;
                    if (count == 2 && scale == 1 && quota)
                    {
                        var shell = (Border)overlay.Content;
                        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(overlay.ActualWidth * 2), (int)Math.Ceiling(overlay.ActualHeight * 2), 192, 192, System.Windows.Media.PixelFormats.Pbgra32);
                        bitmap.Render(shell);
                        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                        using var file = System.IO.File.Create(System.IO.Path.Combine(AppContext.BaseDirectory, "two-account-width-preview.png"));
                        encoder.Save(file);
                    }
                }
                // Adding accounts and receiving longer quota summaries must grow
                // the same existing window without an owner resize or restart.
                var two = profiles.Take(2).ToArray();
                Call(overlay, "SetProfiles", two, two[0].Name);
                var shortDocument = new ProfileStatusDocument();
                foreach (var profile in two) shortDocument.Snapshots[profile.Name] = new UsageSnapshot { LongWindowRemainingPercent = 78, PlanName = "plus" };
                Call(overlay, "SetStatusDocument", shortDocument, null); Pump();
                double shortWidth = overlay.ActualWidth;
                AssertNoExpandedTrailingSpace(overlay);
                Call(overlay, "SetStatusDocument", Document(two, 0), null); Pump();
                AssertNoExpandedTrailingSpace(overlay);
                if (quota) Require(overlay.ActualWidth > shortWidth + 20 * scale, "Longer quota summaries must increase content width immediately.");
                Call(overlay, "SetStatusDocument", shortDocument, null); Pump();
                Require(Math.Abs(overlay.ActualWidth - shortWidth) <= 1, "Shorter refreshed summaries must shrink the overlay again.");
                AssertNoExpandedTrailingSpace(overlay);
                Evidence.Add(new { scenario = "account-content-width", scale, quota, twoAccountWidth = shortWidth });
            }
            finally { overlay.Close(); host.Close(); Pump(); }
        }
    }

    private static void AssertNoExpandedTrailingSpace(Window overlay)
    {
        AssertExpandedProfilesFit(overlay);
        var shell = (Border)overlay.Content;
        var grid = (Grid)shell.Child;
        var scroller = grid.Children.OfType<ScrollViewer>().Single();
        var panel = (StackPanel)scroller.Content;
        var lastItem = (FrameworkElement)panel.Children[panel.Children.Count - 1];
        var menu = grid.Children.OfType<System.Windows.Controls.Button>().Single(button => Grid.GetColumn(button) == 2);
        var itemBounds = lastItem.TransformToAncestor(grid).TransformBounds(new Rect(lastItem.RenderSize));
        var menuBounds = menu.TransformToAncestor(grid).TransformBounds(new Rect(menu.RenderSize));
        double gap = menuBounds.Left - itemBounds.Right;
        Require(gap >= -0.5 && gap <= lastItem.Margin.Right + menu.Margin.Left + 4,
            $"Expanded account row has unnecessary trailing space: {gap} DIP.");
    }
}
