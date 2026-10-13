using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CodexProfileOverlay.Core.Models;
using CodexProfileOverlay.Core.Services;
using Button = System.Windows.Controls.Button;

internal static partial class Program
{
    private static void RunAccountPlanBadgeScenario()
    {
        foreach (double scale in new[] { 0.8, 1.0, 1.4 })
        foreach (var mode in new[] { OverlayDisplayMode.Expanded, OverlayDisplayMode.Compact })
        foreach (bool quota in new[] { false, true })
        {
            var settings = new OverlaySettings
            {
                DisplayMode = mode, Scale = scale, Language = LanguagePreference.ChineseSimplified,
                ShowAutomaticLimitIndicators = quota,
            };
            var overlay = (Window)Activator.CreateInstance(OverlayType, settings,
                new SafeLogger(System.IO.Path.Combine(AppContext.BaseDirectory, "fixture-logs")))!;
            try
            {
                var localizerType = typeof(CodexProfileOverlay.App).Assembly.GetType("CodexProfileOverlay.Localizer")!;
                OverlayType.GetProperty("Localizer")!.SetValue(overlay, Activator.CreateInstance(localizerType, settings.Language));
                OverlayType.GetField("currentMode", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(overlay, mode);
                ProfileInfo[] profiles =
                [
                    new("plan-plus", "unused", "unused") { DisplayName = "主账号", Initials = "主" },
                    new("plan-pro", "unused", "unused") { DisplayName = "475账号", Initials = "4" },
                    new("plan-free", "unused", "unused") { DisplayName = "199账号", Initials = "1" },
                ];
                string[] plans = ["Plus", "Pro", "Free"];
                var document = Document(profiles, 0);
                for (int index = 0; index < profiles.Length; index++)
                    document.Snapshots[profiles[index].Name].PlanName = plans[index];
                Call(overlay, "SetProfiles", profiles, profiles[0].Name);
                Call(overlay, "SetStatusDocument", document, null);
                overlay.Show(); Pump();
                var shell = (Border)overlay.Content;
                Require(Math.Abs(overlay.ActualHeight - 44 * scale) <= 1, "Plan badges must preserve the 44-DIP header height.");
                if (mode == OverlayDisplayMode.Expanded)
                {
                    foreach (var profile in profiles)
                    {
                        var button = Descendants(shell).OfType<Button>().Single(item => Equals(item.Tag, profile.Name));
                        CheckPlanBadge(button, plans[Array.IndexOf(profiles, profile)]);
                        CheckPlanBadgeBounds(button);
                    }
                }
                else
                {
                    CheckPlanBadge(shell, "Plus");
                    CheckPlanBadgeBounds(shell);
                    Open(overlay);
                    var compact = (Popup)OverlayType.GetField("compactPopup", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(overlay)!;
                    foreach (var profile in profiles)
                    {
                        var item = Descendants(compact.Child).OfType<Button>().Single(button =>
                            Descendants(button).OfType<TextBlock>().Any(text => text.Text == profile.DisplayName));
                        CheckPlanBadge(item, plans[Array.IndexOf(profiles, profile)]);
                        CheckPlanBadgeBounds(item);
                    }
                    compact.IsOpen = false; Pump();
                }

                var overviewButton = Descendants(shell).OfType<Button>().Single(button => AutomationProperties.GetAutomationId(button) == "AccountsOverview");
                var overview = (Popup)overviewButton.ToolTip;
                overviewButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
                var cards = Descendants(overview.Child).OfType<Grid>().Single(grid => Equals(grid.Tag, "OverviewCards"));
                for (int index = 0; index < profiles.Length; index++)
                {
                    var card = (FrameworkElement)cards.Children[index];
                    Require(Descendants(card).OfType<TextBlock>().Any(text => text.Text == profiles[index].DisplayName), "Overview lost an account name.");
                    CheckPlanBadge(card, plans[index]);
                    CheckPlanBadgeBounds(card);
                }
                if (scale == 1 && mode == OverlayDisplayMode.Expanded && quota)
                    SavePlanBadgePreview((FrameworkElement)overview.Child);
                overview.IsOpen = false; Pump();

                // Only the plan changes: neither percentages nor CapturedAt can
                // serve as the redraw trigger for this update.
                DateTimeOffset capturedAt = document.Snapshots[profiles[0].Name].CapturedAt;
                int? percent = document.Snapshots[profiles[0].Name].ShortWindowRemainingPercent;
                document.Snapshots[profiles[0].Name].PlanName = "pro";
                Call(overlay, "SetStatusDocument", document, null); Pump();
                var updatedHost = mode == OverlayDisplayMode.Expanded
                    ? (DependencyObject)Descendants((Border)overlay.Content).OfType<Button>().Single(button => Equals(button.Tag, profiles[0].Name))
                    : (Border)overlay.Content;
                CheckPlanBadge(updatedHost, "Pro");
                Require(document.Snapshots[profiles[0].Name].CapturedAt == capturedAt
                    && document.Snapshots[profiles[0].Name].ShortWindowRemainingPercent == percent,
                    "Plan-only redraw must leave the quota and timestamp unchanged.");

                profiles[0] = profiles[0] with { DisplayName = "这是一个很长的测试账号名称应该省略但套餐仍然显示" };
                document.Snapshots[profiles[0].Name].PlanName = "not-a-recognized-plan";
                Call(overlay, "SetStatusDocument", document, null);
                Call(overlay, "SetProfiles", profiles, profiles[0].Name); Pump();
                var unknownHost = mode == OverlayDisplayMode.Expanded
                    ? (DependencyObject)Descendants((Border)overlay.Content).OfType<Button>().Single(button => Equals(button.Tag, profiles[0].Name))
                    : (Border)overlay.Content;
                CheckPlanBadge(unknownHost, null);
                CheckPlanBadgeBounds((FrameworkElement)unknownHost);
                Evidence.Add(new { scenario = "account-plan-badges", scale, mode = mode.ToString(), quota, mixedPlans = true, planOnlyRefresh = true, unknownPlan = true });
            }
            finally { overlay.Close(); Pump(); }
        }
    }

    private static void CheckPlanBadge(DependencyObject host, string? expected)
    {
        var badge = Descendants(host).OfType<Border>().Single(border => AutomationProperties.GetAutomationId(border) == "AccountPlanBadge");
        Require(Equals(badge.Tag, expected), $"Wrong account plan: expected '{expected ?? "unknown"}', found '{badge.Tag}'.");
        var label = (TextBlock)badge.Child;
        Require(label.Text == (expected ?? "未知套餐"), "Plan label must use actual subscription type or an explicit unknown label.");
        Require(expected is not null || label.Text != "Free", "A missing or unfamiliar plan must never be treated as Free.");
    }

    private static void CheckPlanBadgeBounds(FrameworkElement host)
    {
        var badge = Descendants(host).OfType<Border>().Single(border => AutomationProperties.GetAutomationId(border) == "AccountPlanBadge");
        Rect bounds = badge.TransformToAncestor(host).TransformBounds(new Rect(badge.RenderSize));
        Require(badge.ActualWidth > 0 && badge.ActualHeight > 0 && bounds.Left >= -0.5 && bounds.Top >= -0.5
            && bounds.Right <= host.ActualWidth + 0.5 && bounds.Bottom <= host.ActualHeight + 0.5,
            $"Plan badge is clipped: bounds={bounds}, host={host.RenderSize}.");
        var label = (TextBlock)badge.Child;
        var natural = new TextBlock { Text = label.Text, FontSize = label.FontSize, FontWeight = label.FontWeight, FontFamily = label.FontFamily };
        natural.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
        Require(label.ActualWidth >= natural.DesiredSize.Width - 0.5 && label.ActualHeight >= natural.DesiredSize.Height - 0.5,
            "Plan badge label must remain fully readable when the account name is long.");
    }

    private static void SavePlanBadgePreview(FrameworkElement surface)
    {
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(surface.ActualWidth * 2), (int)Math.Ceiling(surface.ActualHeight * 2), 192, 192, PixelFormats.Pbgra32);
        bitmap.Render(surface);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = System.IO.File.Create(System.IO.Path.Combine(AppContext.BaseDirectory, "account-plan-badges-preview.png"));
        encoder.Save(file);
    }
}
