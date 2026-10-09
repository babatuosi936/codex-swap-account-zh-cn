using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CodexProfileOverlay.Core.Models;
using CodexProfileOverlay.Core.Services;
using Popup = System.Windows.Controls.Primitives.Popup;

internal static partial class Program
{
    private static void RunUsageTooltipScenario()
    {
        foreach (var theme in new[] { AppTheme.Light, AppTheme.Dark })
        foreach (var language in new[] { LanguagePreference.ChineseSimplified, LanguagePreference.English, LanguagePreference.Russian })
        foreach (var kind in new[] { "complete", "weekly-only", "unknown" })
        {
            CodexProfileOverlay.App.ApplyTheme(theme);
            var settings = new OverlaySettings { ShowAutomaticLimitIndicators = true, Theme = theme, Language = language };
            var overlay = (Window)Activator.CreateInstance(OverlayType, settings, new SafeLogger(System.IO.Path.Combine(AppContext.BaseDirectory, "fixture-logs")))!;
            try
            {
                var localizerType = typeof(CodexProfileOverlay.App).Assembly.GetType("CodexProfileOverlay.Localizer")!;
                OverlayType.GetProperty("Localizer")!.SetValue(overlay, Activator.CreateInstance(localizerType, language));
                ProfileInfo[] profiles = [new("tooltip-demo", "unused", "unused") { DisplayName = "演示账号", Initials = "演" }];
                Call(overlay, "SetProfiles", profiles, profiles[0].Name);
                var document = new ProfileStatusDocument();
                if (kind != "unknown")
                {
                    document.Snapshots[profiles[0].Name] = new UsageSnapshot
                    {
                        ShortWindowRemainingPercent = kind == "weekly-only" ? null : 80,
                        ShortWindowResetAt = new DateTimeOffset(2026, 10, 9, 15, 11, 0, TimeSpan.FromHours(8)),
                        LongWindowRemainingPercent = 2,
                        LongWindowResetAt = new DateTimeOffset(2026, 10, 14, 11, 52, 0, TimeSpan.FromHours(8)),
                        CapturedAt = new DateTimeOffset(2026, 10, 9, 10, 32, 0, TimeSpan.FromHours(8)),
                        Source = "SOURCE-MUST-NOT-APPEAR",
                        CodexCliVersion = "CLI-MUST-NOT-APPEAR",
                        IsStale = true,
                    };
                }
                Call(overlay, "SetStatusDocument", document, null);
                var button = Descendants((DependencyObject)overlay.Content).OfType<System.Windows.Controls.Button>().Single(item => item.Tag is string);
                Require(button.ToolTip is Popup, "The whole account button must expose the quota card.");
                var tooltip = (Popup)button.ToolTip;
                Require(ReferenceEquals(tooltip.PlacementTarget, button), "Hover card must be anchored to the account button.");
                Require(tooltip.Placement == System.Windows.Controls.Primitives.PlacementMode.Custom && tooltip.CustomPopupPlacementCallback is not null, "Hover card must use account-centered placement.");
                var placements = tooltip.CustomPopupPlacementCallback!(new System.Windows.Size(350, 110), new System.Windows.Size(200, 40), new System.Windows.Point(0, 4));
                Require(placements[0].Point == new System.Windows.Point(-75, 44), "Hover card must be centered immediately below the account.");
                var surface = (Border)tooltip.Child;
                var card = (Border)surface.Child;
                int expectedRows = kind == "complete" ? 3 : 2;
                Require(card.CornerRadius.TopLeft >= 10 && ((StackPanel)card.Child).Children.Count == expectedRows, "Usage hover must omit unavailable limit rows and shrink to fit.");
                var text = string.Join(" ", Descendants(card).OfType<TextBlock>().Select(item => item.Text));
                Require(!text.Contains("MUST-NOT-APPEAR") && !text.Contains(LocalizationCatalog.Text(language, "UsageDataStale")), "Tooltip contains extra diagnostics instead of three rows.");
                Require(text.Contains(LocalizationCatalog.Text(language, "LastUpdated")), "Last updated must remain visible.");
                Require(text.Contains(LocalizationCatalog.Text(language, "FiveHourWindow")) == (kind == "complete"), "Absent five-hour limits must not appear in the card.");
                Require(text.Contains(LocalizationCatalog.Text(language, "WeeklyWindow")) == (kind != "unknown"), "Only available weekly limits should appear.");
                if (kind == "complete") Require(text.Contains("80%") && text.Contains("2%"), "Tooltip percentages are incorrect.");
                if (kind == "weekly-only") Require(text.Contains("2%") && !text.Contains('—'), "Weekly-only cards must not include empty limit placeholders.");
                if (kind == "unknown") Require(text.Contains(LocalizationCatalog.Text(language, "QuotaUnknown")) && !text.Contains("0%"), "Unknown quota must not be displayed as zero.");
                surface.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
                surface.Arrange(new Rect(surface.DesiredSize));
                surface.UpdateLayout();
                foreach (var label in Descendants(card).OfType<TextBlock>())
                {
                    var natural = new TextBlock { Text = label.Text, FontSize = label.FontSize, FontFamily = label.FontFamily, FontWeight = label.FontWeight };
                    natural.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
                    Require(label.ActualWidth >= natural.DesiredSize.Width - 1, "Hover card clips a quota, label or timestamp.");
                }
                Evidence.Add(new { kind = "usage-tooltip", theme = theme.ToString(), language = language.ToString(), data = kind, rows = expectedRows, width = surface.ActualWidth });
                if (language == LanguagePreference.ChineseSimplified && kind == "complete")
                {
                    var bitmap = new RenderTargetBitmap((int)Math.Ceiling(surface.ActualWidth * 2), (int)Math.Ceiling(surface.ActualHeight * 2), 192, 192, PixelFormats.Pbgra32);
                    bitmap.Render(surface);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var file = System.IO.File.Create(System.IO.Path.Combine(AppContext.BaseDirectory, "usage-tooltip-" + theme.ToString().ToLowerInvariant() + ".png"));
                    encoder.Save(file);
                }
                if (theme == AppTheme.Light && language == LanguagePreference.ChineseSimplified && kind == "complete")
                {
                    overlay.Left = 200;
                    overlay.Top = 300;
                    overlay.Show();
                    Pump();
                    void Mouse(FrameworkElement element, RoutedEvent routedEvent) => element.RaiseEvent(new System.Windows.Input.MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = routedEvent });
                    Mouse(button, System.Windows.Input.Mouse.MouseEnterEvent);
                    WaitFor(() => tooltip.IsOpen);
                    Require(tooltip.IsOpen && tooltip.IsHitTestVisible, "The hover card must open and receive mouse input.");
                    Mouse(button, System.Windows.Input.Mouse.MouseLeaveEvent);
                    Pump();
                    Require(tooltip.IsOpen, "The card must remain open while crossing the gap.");
                    Mouse(surface, System.Windows.Input.Mouse.MouseEnterEvent);
                    for (int tick = 0; tick < 8; tick++) Pump();
                    Require(tooltip.IsOpen, "The card must remain visible while the mouse is inside it.");
                    Mouse(surface, System.Windows.Input.Mouse.MouseLeaveEvent);
                    WaitFor(() => !tooltip.IsOpen);
                    Require(!tooltip.IsOpen, "The card must close after leaving both hover surfaces.");
                    Mouse(button, System.Windows.Input.Mouse.MouseEnterEvent);
                    Mouse(button, System.Windows.Input.Mouse.MouseLeaveEvent);
                    for (int tick = 0; tick < 6; tick++) Pump();
                    Require(!tooltip.IsOpen, "A brief pass over the account must not leave a stray card.");
                    Mouse(button, System.Windows.Input.Mouse.MouseEnterEvent);
                    WaitFor(() => tooltip.IsOpen);
                    Require(tooltip.IsOpen, "Hover must reopen the card after dismissal.");
                    overlay.Hide();
                    Require(!tooltip.IsOpen, "Hiding the overlay must also dismiss its detail card.");
                    Evidence.Add(new { kind = "usage-hover-lifetime", gap = "kept", cardHover = "kept", leave = "closed", briefPass = "cancelled" });
                }
            }
            finally { overlay.Close(); }
        }
        CodexProfileOverlay.App.ApplyTheme(AppTheme.Light);
    }
}
