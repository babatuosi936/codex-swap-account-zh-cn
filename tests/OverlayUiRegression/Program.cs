using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Linq;
using CodexProfileOverlay.Core.Models;
using CodexProfileOverlay.Core.Services;
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;
using Forms = System.Windows.Forms;
using Point = System.Windows.Point;

// Isolated WPF regression fixture. No controller, network calls, real account
// directories, Codex windows, or account switching are used here.
internal static class Program
{
    private static readonly Type OverlayType = typeof(CodexProfileOverlay.App).Assembly.GetType("CodexProfileOverlay.OverlayWindow")!;
    private static readonly List<object> Evidence = [];

    [STAThread]
    private static int Main()
    {
        var app = new FixtureApp { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        using var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("OverlayUiRegression.FixtureResources.xaml")!;
        var source = XDocument.Load(resource);
        XNamespace wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var dictionary = new XElement(wpf + "ResourceDictionary",
            new XAttribute(XNamespace.Xmlns + "x", "http://schemas.microsoft.com/winfx/2006/xaml"),
            source.Root!.Element(wpf + "Application.Resources")!.Elements());
        app.Resources = (ResourceDictionary)System.Windows.Markup.XamlReader.Parse(dictionary.ToString());
        CodexProfileOverlay.App.ApplyTheme(AppTheme.Light);
        try
        {
            foreach (var scenario in new[] { (Scale: 1.0, Edge: false), (Scale: 1.4, Edge: false), (Scale: 1.0, Edge: true), (Scale: 1.4, Edge: true) })
                foreach (bool staysOpen in new[] { false, true })
                    RunScenario(scenario.Scale, scenario.Edge, staysOpen);
            RunExpandedDragScenario(1.0);
            RunExpandedDragScenario(1.4);
            System.IO.File.WriteAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "popup-regression.json"), JsonSerializer.Serialize(Evidence, new JsonSerializerOptions { WriteIndented = true }));
            System.Console.WriteLine($"PASS: {Evidence.Count} UI checks across popup placement and expanded account dragging.");
            app.Shutdown();
            return 0;
        }
        catch (Exception error)
        {
            System.Console.Error.WriteLine(error);
            app.Shutdown();
            return 1;
        }
    }

    private static void RunScenario(double scale, bool edge, bool staysOpen)
    {
        var screen = Forms.Screen.PrimaryScreen!.WorkingArea;
        var host = new Window { Title = "Quota Popup Regression Fixture", Width = 1000, Height = 700, Left = 60, Top = 60, ShowInTaskbar = false };
        host.Show();
        Pump();
        var settings = new OverlaySettings { DisplayMode = OverlayDisplayMode.Compact, Scale = scale, ShowAutomaticLimitIndicators = true, Language = LanguagePreference.ChineseSimplified };
        var overlay = (Window)Activator.CreateInstance(OverlayType, settings, new SafeLogger(System.IO.Path.Combine(AppContext.BaseDirectory, "fixture-logs")))!;
        try
        {
            var localizerType = typeof(CodexProfileOverlay.App).Assembly.GetType("CodexProfileOverlay.Localizer")!;
            OverlayType.GetProperty("Localizer")!.SetValue(overlay, Activator.CreateInstance(localizerType, LanguagePreference.ChineseSimplified));
            typeof(OverlaySettings).GetProperty("DisplayMode")!.SetValue(settings, OverlayDisplayMode.Compact);
            OverlayType.GetField("currentMode", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(overlay, OverlayDisplayMode.Compact);
            ProfileInfo[] profiles = [new("fixture-main", "unused", "unused") { DisplayName = "主账号" }, new("fixture-two", "unused", "unused") { DisplayName = "测试账号二" }, new("fixture-three", "unused", "unused") { DisplayName = "测试账号三" }];
            Call(overlay, "SetProfiles", profiles, profiles[0].Name);
            Call(overlay, "SetStatusDocument", Document(profiles, 0), null);
            overlay.Show();
            overlay.Left = 340;
            overlay.Top = 80;
            Pump();
            if (edge)
            {
                double dpi = PresentationSource.FromVisual(overlay)!.CompositionTarget!.TransformToDevice.M11;
                overlay.Left = (screen.Right - 15) / dpi - overlay.ActualWidth;
                overlay.Top = (screen.Bottom - 20) / dpi - overlay.ActualHeight;
                Pump();
            }

            var popup = (Popup)OverlayType.GetField("compactPopup", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(overlay)!;
            popup.StaysOpen = staysOpen; // Cover production mouse capture and focus-independent geometry checks.
            Open(overlay);
            Verify(overlay, popup, scale, edge, "initial");
            for (int i = 1; i <= 5; i++)
            {
                Call(overlay, "SetStatusDocument", Document(profiles, i), null);
                Pump();
                Verify(overlay, popup, scale, edge, $"background-refresh-{i}");
            }

            int refreshCalls = 0;
            OverlayType.GetProperty("OnRefreshProfiles")!.SetValue(overlay, (Action)(() =>
            {
                Require(popup.IsOpen, "Manual refresh must keep the menu open when calling the refresh action.");
                refreshCalls++;
                Call(overlay, "SetProfiles", profiles, profiles[0].Name);
                Call(overlay, "SetStatusDocument", Document(profiles, 5 + refreshCalls), null);
            }));
            bool managerOpened = false;
            OverlayType.GetProperty("OnManageProfiles")!.SetValue(overlay, (Action)(() =>
            {
                Require(!popup.IsOpen, "Opening the profile manager must still close the menu.");
                managerOpened = true;
            }));
            // Install the command delegates while preserving the open menu.
            Call(overlay, "ApplySettings");
            Pump();
            for (int click = 1; click <= 3; click++)
            {
                var refreshButton = Descendants(popup.Child).OfType<Button>().Single(button => Text(button).Contains("刷新全部额度"));
                refreshButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Pump();
                Require(refreshCalls == click && popup.IsOpen, "Manual refresh unexpectedly closed the menu.");
                Verify(overlay, popup, scale, edge, $"manual-refresh-{click}");
                // Simulate another account's asynchronous query result arriving later.
                Call(overlay, "SetStatusDocument", Document(profiles, 10 + click), null);
                Pump();
                Verify(overlay, popup, scale, edge, $"manual-refresh-result-{click}");
                int remainingShort = 62 - (10 + click);
                int remainingLong = 36 - (10 + click);
                Require(Descendants(popup.Child).OfType<TextBlock>().Any(text => text.Text.Contains($"{remainingShort}%") && text.Text.Contains($"{remainingLong}%")), "Open menu did not show the latest quota result.");
            }
            var managerButton = Descendants(popup.Child).OfType<Button>().Single(button => Text(button).Contains("管理账号"));
            managerButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Pump();
            Require(managerOpened && !popup.IsOpen, "Profile manager command did not close the menu.");
        }
        finally { overlay.Close(); host.Close(); Pump(); }
    }

    private static void RunExpandedDragScenario(double scale)
    {
        var host = new Window { Title = "Expanded Drag Regression Fixture", Width = 1100, Height = 700, Left = 60, Top = 60, ShowInTaskbar = false };
        host.Show();
        Pump();
        var settings = new OverlaySettings { DisplayMode = OverlayDisplayMode.Expanded, Scale = scale, ShowAutomaticLimitIndicators = true, Language = LanguagePreference.ChineseSimplified };
        var overlay = (Window)Activator.CreateInstance(OverlayType, settings, new SafeLogger(System.IO.Path.Combine(AppContext.BaseDirectory, "fixture-logs")))!;
        try
        {
            var localizerType = typeof(CodexProfileOverlay.App).Assembly.GetType("CodexProfileOverlay.Localizer")!;
            OverlayType.GetProperty("Localizer")!.SetValue(overlay, Activator.CreateInstance(localizerType, LanguagePreference.ChineseSimplified));
            ProfileInfo[] profiles = [new("drag-main", "unused", "unused") { DisplayName = "主账号" }, new("drag-two", "unused", "unused") { DisplayName = "475账号" }, new("drag-three", "unused", "unused") { DisplayName = "199账号" }];
            Call(overlay, "SetProfiles", profiles, profiles[0].Name);
            Call(overlay, "SetStatusDocument", Document(profiles, 0), null);
            overlay.Left = 200;
            overlay.Top = 100;
            overlay.Show();
            Pump();
            var scroller = Descendants((DependencyObject)overlay.Content).OfType<ScrollViewer>().Single();
            var buttons = Descendants(scroller).OfType<Button>().Where(button => button.Tag is string).ToArray();
            scroller.ScrollToHorizontalOffset(0);
            Pump();
            double before = scroller.HorizontalOffset;
            Require(buttons.Length == 3 && scroller.ScrollableWidth > 0, "Fixture must have three accounts and an overflowing horizontal viewport.");
            var pending = OverlayType.GetField("dragPending", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var dragging = OverlayType.GetField("isDragging", BindingFlags.Instance | BindingFlags.NonPublic)!;
            foreach (bool dragStarted in new[] { false, true })
            {
                pending.SetValue(overlay, !dragStarted);
                dragging.SetValue(overlay, dragStarted);
                // Button focus requests BringIntoView on press, before the drag threshold.
                buttons[2].Focus();
                buttons[2].BringIntoView();
                Pump();
                double after = scroller.HorizontalOffset;
                Rect activeBounds = buttons[0].TransformToAncestor(scroller).TransformBounds(new Rect(buttons[0].RenderSize));
                bool activeVisible = activeBounds.Left >= -0.5 && activeBounds.Right <= scroller.ActualWidth + 0.5;
                Evidence.Add(new { kind = "expanded-drag", scale, dragStarted, before, after, activeVisible });
                Require(Math.Abs(after - before) < 0.01 && activeVisible, $"Pressing account three scrolled away the main account: scale={scale}, dragging={dragStarted}, offset={before}->{after}.");
            }
            pending.SetValue(overlay, false);
            dragging.SetValue(overlay, false);
            // Normal requests outside a pointer gesture should retain their accessibility behavior.
            buttons[2].BringIntoView();
            Pump();
            Require(scroller.HorizontalOffset > before, "Normal keyboard/programmatic navigation must still be able to scroll.");
        }
        finally { overlay.Close(); host.Close(); Pump(); }
    }

    private static ProfileStatusDocument Document(ProfileInfo[] profiles, int sequence)
    {
        var document = new ProfileStatusDocument();
        foreach (var profile in profiles)
            document.Snapshots[profile.Name] = new UsageSnapshot { ShortWindowRemainingPercent = 62 - sequence, LongWindowRemainingPercent = 36 - sequence, CapturedAt = DateTimeOffset.UtcNow.AddSeconds(sequence) };
        return document;
    }

    private static void Open(Window overlay)
    {
        var shell = (Border)overlay.Content;
        ((Button)shell.Child).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Pump();
    }

    private static void Verify(Window overlay, Popup popup, double scale, bool edge, string stage)
    {
        Require(popup.IsOpen, $"Menu unexpectedly closed at {stage}.");
        Require(PresentationSource.FromVisual(popup.PlacementTarget) is not null, $"Detached popup anchor at {stage}.");
        var child = (FrameworkElement)popup.Child;
        var corner = child.PointToScreen(new Point(0, 0));
        var opposite = child.PointToScreen(new Point(child.ActualWidth, child.ActualHeight));
        var anchor = ((Border)overlay.Content).PointToScreen(new Point(0, 0));
        var anchorEnd = ((Border)overlay.Content).PointToScreen(new Point(((Border)overlay.Content).ActualWidth, ((Border)overlay.Content).ActualHeight));
        bool overlaps = corner.X < anchorEnd.X + 3 && opposite.X > anchor.X - 3;
        // Allow the original button's inner padding as well as the shell anchor.
        bool attached = Math.Min(Math.Abs(corner.Y - anchorEnd.Y), Math.Abs(opposite.Y - anchor.Y)) < 24;
        var screen = Forms.Screen.FromHandle(new WindowInteropHelper(overlay).Handle).WorkingArea;
        bool visible = corner.X >= screen.Left - 2 && corner.Y >= screen.Top - 2 && opposite.X <= screen.Right + 2 && opposite.Y <= screen.Bottom + 2;
        Evidence.Add(new { scale, edge, popup.StaysOpen, stage, popupX = corner.X, popupY = corner.Y, popupRight = opposite.X, popupBottom = opposite.Y, anchorX = anchor.X, anchorY = anchor.Y, overlaps, attached, visible });
        Require(overlaps && attached && visible, $"Popup detached or clipped at {stage}, scale={scale}, edge={edge}: ({corner})..({opposite}), anchor=({anchor})..({anchorEnd})");
    }

    private static string Text(Button button) => string.Join(" ", Descendants(button).OfType<TextBlock>().Select(text => text.Text));
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        yield return parent;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            foreach (var child in Descendants(VisualTreeHelper.GetChild(parent, i))) yield return child;
    }
    private static void Call(Window overlay, string method, params object?[] arguments) => OverlayType.GetMethod(method)!.Invoke(overlay, arguments);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Pump()
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(100) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }
    private sealed class FixtureApp : Application { protected override void OnStartup(StartupEventArgs e) { } }
}
