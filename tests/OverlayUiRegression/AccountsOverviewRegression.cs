using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CodexProfileOverlay.Core.Models;
using CodexProfileOverlay.Core.Services;
using Button = System.Windows.Controls.Button;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

internal static partial class Program
{
    private static void RunAccountsOverviewScenario()
    {
        foreach (var theme in new[] { AppTheme.Light, AppTheme.Dark })
        foreach (var mode in new[] { OverlayDisplayMode.Expanded, OverlayDisplayMode.Compact })
        foreach (int count in new[] { 0, 1, 2, 3, 12 })
        {
            CodexProfileOverlay.App.ApplyTheme(theme);
            var settings = new OverlaySettings { DisplayMode = mode, Theme = theme, Language = LanguagePreference.ChineseSimplified, ShowAutomaticLimitIndicators = true };
            var overlay = (Window)Activator.CreateInstance(OverlayType, settings, new SafeLogger(System.IO.Path.Combine(AppContext.BaseDirectory, "fixture-logs")))!;
            try
            {
                // Configure actual compact mode through placement rather than bypassing it.
                OverlayType.GetField("currentMode", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(overlay, mode);
                var localizerType = typeof(CodexProfileOverlay.App).Assembly.GetType("CodexProfileOverlay.Localizer")!;
                OverlayType.GetProperty("Localizer")!.SetValue(overlay, Activator.CreateInstance(localizerType, settings.Language));
                var profiles = Enumerable.Range(0, count).Select(i => new ProfileInfo("overview-" + i, "unused", "unused") { DisplayName = i == 0 ? "主账号" : "账号 " + i }).ToArray();
                Call(overlay, "SetProfiles", profiles, profiles.FirstOrDefault()?.Name);
                var document = Document(profiles, 0);
                if (count > 0) document.Snapshots[profiles[0].Name] = new UsageSnapshot { LongWindowRemainingPercent = 83, LongWindowResetAt = DateTimeOffset.UtcNow.AddDays(3), CapturedAt = DateTimeOffset.UtcNow };
                Call(overlay, "SetStatusDocument", document, null);
                overlay.Show(); Pump();
                var overview = Descendants((DependencyObject)overlay.Content).OfType<Button>().SingleOrDefault(b => AutomationProperties.GetAutomationId(b) == "AccountsOverview");
                Require((overview is not null) == (count >= 2), "Overview threshold must be two accounts in both layouts.");
                if (overview is not null)
                {
                    var first = Descendants((DependencyObject)overlay.Content).OfType<Button>().First();
                    Require(ReferenceEquals(first, overview), "Overview must precede account buttons.");
                    var popup = (Popup)overview.ToolTip;
                    overview.RaiseEvent(new MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = System.Windows.Input.Mouse.MouseEnterEvent });
                    WaitFor(() => popup.IsOpen);
                    Require(popup.IsOpen && ReferenceEquals(popup.PlacementTarget, overview), "Overview must open beneath its own entry.");
                    var text = string.Join(" ", Descendants(popup.Child).OfType<TextBlock>().Select(t => t.Text));
                    Require(profiles.All(p => text.Contains(p.DisplayName)) && text.Contains("83%") && text.Contains("最近更新"), "Overview must include every account and quota timestamps.");
                    var scroll = Descendants(popup.Child).OfType<ScrollViewer>().Single();
                    Require(scroll.MaxHeight <= 480 && scroll.VerticalScrollBarVisibility == ScrollBarVisibility.Auto, "Large overviews must have bounded scrolling.");
                    if (count == 3 && mode == OverlayDisplayMode.Expanded && theme == AppTheme.Light)
                    {
                        overview.RaiseEvent(new MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = System.Windows.Input.Mouse.MouseLeaveEvent });
                        popup.Child.RaiseEvent(new MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = System.Windows.Input.Mouse.MouseEnterEvent });
                        for (int i = 0; i < 6; i++) Pump();
                        Require(popup.IsOpen, "Pointer must be able to enter and inspect the overview.");
                        var surface = (FrameworkElement)popup.Child;
                        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(surface.ActualWidth * 2), (int)Math.Ceiling(surface.ActualHeight * 2), 192, 192, PixelFormats.Pbgra32);
                        bitmap.Render(surface);
                        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                        using var file = System.IO.File.Create(System.IO.Path.Combine(AppContext.BaseDirectory, "accounts-overview.png")); encoder.Save(file);
                        overlay.Hide();
                        Require(!popup.IsOpen, "Hiding the floating window must close its overview.");
                    }
                    popup.IsOpen = false;
                }
                Evidence.Add(new { scenario = "accounts-overview", theme, mode, count });
            }
            finally { overlay.Close(); Pump(); }
        }
        CodexProfileOverlay.App.ApplyTheme(AppTheme.Light);
    }
}
