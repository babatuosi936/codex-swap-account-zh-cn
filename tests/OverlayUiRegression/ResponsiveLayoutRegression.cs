using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using CodexProfileOverlay.Core.Models;
using CodexProfileOverlay.Core.Services;

internal static partial class Program
{
    private static void RunResponsiveLayoutScenario(double scale, LanguagePreference language)
    {
        var host = new Window { Title = "Responsive Overlay Fixture", Width = 1450, Height = 700, Left = 20, Top = 20, ShowInTaskbar = false };
        var settings = new OverlaySettings { DisplayMode = OverlayDisplayMode.Auto, Scale = scale, ShowAutomaticLimitIndicators = true, Language = language, PositionPreset = PositionPreset.TopCenter };
        var overlay = (Window)Activator.CreateInstance(OverlayType, settings, new SafeLogger(System.IO.Path.Combine(AppContext.BaseDirectory, "fixture-logs")))!;
        try
        {
            host.Show();
            Pump();
            var localizerType = typeof(CodexProfileOverlay.App).Assembly.GetType("CodexProfileOverlay.Localizer")!;
            OverlayType.GetProperty("Localizer")!.SetValue(overlay, Activator.CreateInstance(localizerType, language));
            ProfileInfo[] profiles = [new("responsive-main", "unused", "unused") { DisplayName = "主账号", Initials = "主" }, new("responsive-two", "unused", "unused") { DisplayName = "475账号", Initials = "4" }, new("responsive-three", "unused", "unused") { DisplayName = "199账号", Initials = "1" }];
            Call(overlay, "SetProfiles", profiles, profiles[1].Name);
            Call(overlay, "SetStatusDocument", Document(profiles, 0), null);
            var handle = new WindowInteropHelper(host).Handle;
            Call(overlay, "AttachTo", handle);
            Call(overlay, "UpdatePlacement", handle);
            Pump();
            AssertExpandedProfilesFit(overlay);
            double requiredWidth = overlay.ActualWidth;

            // The former fixed 620-DIP panel cannot fit two quota windows per account.
            host.Width = requiredWidth - 35;
            Pump();
            Call(overlay, "UpdatePlacement", handle);
            Pump();
            Require((OverlayDisplayMode)OverlayType.GetField("currentMode", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(overlay)! == OverlayDisplayMode.Compact,
                "Auto mode must become compact when the actual account row no longer fits.");
            Require(overlay.ActualWidth <= host.ActualWidth, "Compact overlay exceeds its owner window.");

            host.Width = 1450;
            Pump();
            Call(overlay, "UpdatePlacement", handle);
            Pump();
            AssertExpandedProfilesFit(overlay);
            Call(overlay, "SetStatusDocument", Document(profiles, 1), null);
            Call(overlay, "UpdatePlacement", handle);
            Pump();
            AssertExpandedProfilesFit(overlay);
            // Match a stale weekly-only account beside two full quota lines.
            var screenshotDocument = Document(profiles, 0);
            screenshotDocument.Snapshots[profiles[0].Name] = new UsageSnapshot { LongWindowRemainingPercent = 0, CapturedAt = DateTimeOffset.UtcNow.AddHours(-2) };
            Call(overlay, "SetStatusDocument", screenshotDocument, null);
            foreach (var active in profiles)
            {
                Call(overlay, "SetProfiles", profiles, active.Name);
                Call(overlay, "UpdatePlacement", handle);
                Pump();
                AssertExpandedProfilesFit(overlay);
            }
            if (scale == 1 && language == LanguagePreference.ChineseSimplified)
            {
                var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(overlay.ActualWidth * 2), (int)Math.Ceiling(overlay.ActualHeight * 2), 192, 192, System.Windows.Media.PixelFormats.Pbgra32);
                bitmap.Render((Border)overlay.Content);
                var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                using var file = System.IO.File.Create(System.IO.Path.Combine(AppContext.BaseDirectory, "responsive-layout-preview.png"));
                encoder.Save(file);
            }
            Evidence.Add(new { kind = "responsive-layout", scale, language = language.ToString(), requiredWidth, refreshedWidth = overlay.ActualWidth });

            var manyProfiles = Enumerable.Range(0, 12).Select(index => new ProfileInfo("responsive-" + index, "unused", "unused") { DisplayName = "账号 " + index }).ToArray();
            Call(overlay, "SetProfiles", manyProfiles, manyProfiles[0].Name);
            Call(overlay, "SetStatusDocument", Document(manyProfiles, 0), null);
            Pump();
            Require((OverlayDisplayMode)OverlayType.GetField("currentMode", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(overlay)! == OverlayDisplayMode.Compact,
                "Adding more accounts must immediately switch to compact without waiting for an owner resize.");
            host.Width = 260;
            Pump();
            Call(overlay, "UpdatePlacement", handle);
            Pump();
            Require(overlay.ActualWidth <= host.ActualWidth, "Overlay exceeds a very narrow owner window.");
        }
        finally { overlay.Close(); host.Close(); Pump(); }
    }

    private static void AssertExpandedProfilesFit(Window overlay)
    {
        Require((OverlayDisplayMode)OverlayType.GetField("currentMode", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(overlay)! == OverlayDisplayMode.Expanded, "Wide windows should show the expanded account row.");
        var scroller = Descendants((DependencyObject)overlay.Content).OfType<ScrollViewer>().Single();
        Require(scroller.ScrollableWidth <= 1, $"Account row overflows by {scroller.ScrollableWidth} DIP.");
        foreach (var text in Descendants(scroller).OfType<TextBlock>().Where(text => text.Text.Contains('%')))
        {
            var bounds = text.TransformToAncestor(scroller).TransformBounds(new Rect(text.RenderSize));
            Require(bounds.Left >= -0.5 && bounds.Right <= scroller.ActualWidth + 0.5, $"Quota text is outside the account viewport: {text.Text}.");
            var natural = new TextBlock { Text = text.Text, FontFamily = text.FontFamily, FontSize = text.FontSize, FontWeight = text.FontWeight };
            natural.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
            Require(text.ActualWidth >= natural.DesiredSize.Width - 1, $"Quota text is truncated inside its account button: {text.Text}.");
        }
    }
}
