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
using Point = System.Windows.Point;
using Size = System.Windows.Size;

internal static partial class Program
{
    private static void RunAccountsOverviewScenario()
    {
        RunQuotaPopupRefreshScenario();
        var builder = typeof(CodexProfileOverlay.App).Assembly.GetType("CodexProfileOverlay.AccountsOverviewBuilder")!;
        var resolve = builder.GetMethod("ResolveColumns", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        foreach (var (width, expected) in new[] { (420d, 1), (800d, 2), (1200d, 3) })
        {
            Require((int)resolve.Invoke(null, new object[] { 12, width })! == expected, "Overview must wrap according to available monitor width.");
            Evidence.Add(new { scenario = "overview-columns", width, expected });
        }
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
                    overview.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Require(popup.IsOpen && !popup.StaysOpen, "Click must open overview immediately with outside-click dismissal.");
                    overview.RaiseEvent(new MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = System.Windows.Input.Mouse.MouseLeaveEvent });
                    popup.Child.RaiseEvent(new MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = System.Windows.Input.Mouse.MouseLeaveEvent });
                    for (int i = 0; i < 6; i++) Pump();
                    Require(popup.IsOpen, "Clicked overview must remain open after the pointer leaves.");
                    overview.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    for (int i = 0; i < 6; i++) Pump();
                    Require(!popup.IsOpen, "A second Overview click must close it without reopening.");
                    overview.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Require(popup.IsOpen, "A subsequent Overview click must open it again.");
                    popup.Child.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,
                        PresentationSource.FromVisual(popup.Child), Environment.TickCount, System.Windows.Input.Key.Escape)
                        { RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent });
                    Pump();
                    Require(!popup.IsOpen && popup.StaysOpen, "Escape must dismiss the clicked overview and restore hover behavior.");
                    overview.RaiseEvent(new MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = System.Windows.Input.Mouse.MouseEnterEvent });
                    WaitFor(() => popup.IsOpen);
                    overview.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    for (int i = 0; i < 6; i++) Pump();
                    Require(popup.IsOpen && !popup.StaysOpen, "Clicking an existing hover preview must pin it without closing it.");
                    popup.IsOpen = false;
                    Pump();
                    overview.RaiseEvent(new MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = System.Windows.Input.Mouse.MouseEnterEvent });
                    WaitFor(() => popup.IsOpen);
                    Require(popup.IsOpen && ReferenceEquals(popup.PlacementTarget, overlay.Content), "Overview must anchor to the whole floating bar.");
                    var placements = popup.CustomPopupPlacementCallback!(new Size(1050, 300), new Size(720, 40), new Point(0, 4));
                    Require(Math.Abs(placements[0].Point.X + 525 - 360) < 0.01 && placements[0].Point.Y == 44,
                        "Overview and floating bar must share the horizontal center axis.");
                    var text = string.Join(" ", Descendants(popup.Child).OfType<TextBlock>().Select(t => t.Text));
                    Require(profiles.All(p => text.Contains(p.DisplayName)) && text.Contains("83%") && text.Contains("最近更新"), "Overview must include every account and quota timestamps.");
                    var scroll = Descendants(popup.Child).OfType<ScrollViewer>().Single();
                    Require(scroll.MaxHeight <= 480 && scroll.VerticalScrollBarVisibility == ScrollBarVisibility.Auto, "Large overviews must have bounded scrolling.");
                    var cards = Descendants(popup.Child).OfType<Grid>().Single(g => Equals(g.Tag, "OverviewCards"));
                    Require(cards.Children.Count == count && cards.ColumnDefinitions.Count <= 3,
                        "Overview must retain all cards and use at most three horizontal columns.");
                    Require(cards.RowDefinitions.Count == (count + cards.ColumnDefinitions.Count - 1) / cards.ColumnDefinitions.Count,
                        "Additional accounts must wrap into vertical rows.");
                    if (cards.ColumnDefinitions.Count > 1)
                        Require(Grid.GetRow(cards.Children[0]) == Grid.GetRow(cards.Children[1]) && Grid.GetColumn(cards.Children[1]) == 1,
                            "Cards must occupy neighboring horizontal columns before starting another row.");
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

    private static void RunQuotaPopupRefreshScenario()
    {
        var settings = new OverlaySettings { DisplayMode = OverlayDisplayMode.Expanded, Language = LanguagePreference.ChineseSimplified, ShowAutomaticLimitIndicators = true };
        var overlay = (Window)Activator.CreateInstance(OverlayType, settings, new SafeLogger(System.IO.Path.Combine(AppContext.BaseDirectory, "fixture-logs")))!;
        try
        {
            var profiles = Enumerable.Range(0, 3).Select(i => new ProfileInfo("refresh-" + i, "unused", "unused") { DisplayName = "测试账号 " + i }).ToArray();
            var document = Document(profiles, 0);
            var pending = new TaskCompletionSource<bool>();
            int allCalls = 0;
            string? singleId = null;
            OverlayType.GetProperty("OnRefreshAllQuotas")!.SetValue(overlay, (Func<Task>)(() => { allCalls++; return pending.Task; }));
            OverlayType.GetProperty("OnRefreshQuota")!.SetValue(overlay, (Func<string, Task>)(id =>
            {
                singleId = id;
                document.Snapshots[id] = new UsageSnapshot { LongWindowRemainingPercent = 91, CapturedAt = DateTimeOffset.UtcNow };
                Call(overlay, "SetStatusDocument", document, null);
                return Task.CompletedTask;
            }));
            Call(overlay, "SetProfiles", profiles, profiles[0].Name);
            Call(overlay, "SetStatusDocument", document, null);
            overlay.Show(); Pump();
            var overview = Descendants((DependencyObject)overlay.Content).OfType<Button>().Single(b => AutomationProperties.GetAutomationId(b) == "AccountsOverview");
            overview.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var popup = (Popup)overview.ToolTip;
            Button RefreshButton(Popup panel, string id) => Descendants(panel.Child).OfType<Button>().Single(b => AutomationProperties.GetAutomationId(b) == id);
            var refresh = RefreshButton(popup, "RefreshAllQuota");
            refresh.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(allCalls == 1 && !refresh.IsEnabled, "Overview refresh must invoke all-account callback and disable repeats.");
            document.Snapshots[profiles[0].Name] = new UsageSnapshot { LongWindowRemainingPercent = 89, CapturedAt = DateTimeOffset.UtcNow };
            Call(overlay, "SetStatusDocument", document, null); Pump();
            Require(popup.IsOpen && !RefreshButton(popup, "RefreshAllQuota").IsEnabled, "Intermediate quota updates must preserve the open panel and busy state.");
            RefreshButton(popup, "RefreshAllQuota").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(allCalls == 1, "Rebuilt refresh controls must not submit another request while busy.");
            pending.SetResult(true); Pump();
            Require(popup.IsOpen && RefreshButton(popup, "RefreshAllQuota").IsEnabled, "Completion must update the existing overview and restore refresh action.");
            popup.IsOpen = false; Pump();
            var account = Descendants((DependencyObject)overlay.Content).OfType<Button>().Single(b => Equals(b.Tag, profiles[1].Name));
            account.RaiseEvent(new MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = System.Windows.Input.Mouse.MouseEnterEvent });
            var single = (Popup)account.ToolTip;
            WaitFor(() => single.IsOpen);
            RefreshButton(single, "RefreshQuota").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
            Require(singleId == profiles[1].Name && single.IsOpen, "Individual refresh must target its own account and keep details visible.");
            Require(Descendants(single.Child).OfType<TextBlock>().Any(t => t.Text == "91%"), "Individual card must show the newly returned quota.");
            Evidence.Add(new { scenario = "quota-popup-refresh", allCalls, singleId });
        }
        finally { overlay.Close(); Pump(); }
    }
}
