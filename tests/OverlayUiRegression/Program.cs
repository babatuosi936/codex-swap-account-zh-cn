using System.Reflection;
using System.Runtime.InteropServices;
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
internal static partial class Program
{
    private static readonly Type OverlayType = typeof(CodexProfileOverlay.App).Assembly.GetType("CodexProfileOverlay.OverlayWindow")!;
    private static readonly List<object> Evidence = [];

    [STAThread]
    private static int Main(string[] args)
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
            if (args.Length == 2 && args[0] == "--docs")
            {
                RenderDocumentation(args[1]);
                app.Shutdown();
                return 0;
            }
            if (args.Contains("--hotkeys-only"))
            {
                RunHotkeysScenario();
            }
            if (args.Contains("--tooltip-only") || args.Length == 0)
            {
                RunUsageTooltipScenario();
            }
            if (!args.Contains("--auxiliary-only") && !args.Contains("--hotkeys-only") && !args.Contains("--tooltip-only"))
            {
                foreach (double scale in new[] { 0.8, 1.0, 1.4 })
                    foreach (var mode in new[] { OverlayDisplayMode.Compact, OverlayDisplayMode.Expanded })
                        foreach (bool quota in new[] { false, true })
                            RunHeaderLayoutScenario(scale, mode, quota);
                foreach (double scale in new[] { 0.8, 1.0, 1.4 })
                    foreach (var language in new[] { LanguagePreference.ChineseSimplified, LanguagePreference.English, LanguagePreference.Russian })
                        RunResponsiveLayoutScenario(scale, language);
                if (!args.Contains("--header-only"))
                {
                    foreach (var scenario in new[] { (Scale: 1.0, Edge: false), (Scale: 1.4, Edge: false), (Scale: 1.0, Edge: true), (Scale: 1.4, Edge: true) })
                        foreach (bool staysOpen in new[] { false, true })
                            RunScenario(scenario.Scale, scenario.Edge, staysOpen);
                    RunExpandedDragScenario(1.0);
                    RunExpandedDragScenario(1.4);
                }
            }
            if (!args.Contains("--header-only") && !args.Contains("--hotkeys-only") && !args.Contains("--tooltip-only"))
            {
                RunAuxiliaryWindowScenario("SettingsWindow");
                RunAuxiliaryWindowScenario("ProfileManagerWindow");
            }
            if (args.Length == 0)
            {
                RunHotkeysScenario();
            }
            System.IO.File.WriteAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "popup-regression.json"), JsonSerializer.Serialize(Evidence, new JsonSerializerOptions { WriteIndented = true }));
            System.Console.WriteLine($"PASS: {Evidence.Count} UI checks.");
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

    private static void RunHeaderLayoutScenario(double scale, OverlayDisplayMode mode, bool quota)
    {
        var settings = new OverlaySettings { DisplayMode = mode, Scale = scale, ShowAutomaticLimitIndicators = quota, Language = LanguagePreference.ChineseSimplified };
        var overlay = (Window)Activator.CreateInstance(OverlayType, settings, new SafeLogger(System.IO.Path.Combine(AppContext.BaseDirectory, "fixture-logs")))!;
        try
        {
            var localizerType = typeof(CodexProfileOverlay.App).Assembly.GetType("CodexProfileOverlay.Localizer")!;
            OverlayType.GetProperty("Localizer")!.SetValue(overlay, Activator.CreateInstance(localizerType, LanguagePreference.ChineseSimplified));
            OverlayType.GetField("currentMode", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(overlay, mode);
            ProfileInfo[] profiles = [new("header-main", "unused", "unused") { DisplayName = "主账号", Initials = "主" }, new("header-two", "unused", "unused") { DisplayName = "475账号", Initials = "4" }, new("header-three", "unused", "unused") { DisplayName = "199账号", Initials = "1" }];
            Call(overlay, "SetProfiles", profiles, profiles[0].Name);
            Call(overlay, "SetStatusDocument", Document(profiles, 0), null);
            overlay.Show();
            Pump();
            Require(Math.Abs(overlay.ActualHeight - 44 * scale) <= 1, $"Header height does not match Codex's full title bar: mode={mode}, quota={quota}, scale={scale}, actual={overlay.ActualHeight}.");
            var shell = (Border)overlay.Content;
            var textBlocks = Descendants(shell).OfType<TextBlock>().Where(text => text.Text.Contains("账号") || text.Text.Contains('%')).ToArray();
            Require(textBlocks.Length == (mode == OverlayDisplayMode.Compact ? 1 : 3) * (quota ? 2 : 1), "Header lost an account name or quota line.");
            foreach (var text in textBlocks)
            {
                Rect bounds = text.TransformToAncestor(shell).TransformBounds(new Rect(text.RenderSize));
                // DesiredSize includes the margin; ActualHeight measures the text itself.
                double desiredTextHeight = text.DesiredSize.Height - text.Margin.Top - text.Margin.Bottom;
                Require(bounds.Top >= 0 && bounds.Bottom <= shell.ActualHeight + 0.5 && text.ActualHeight >= desiredTextHeight - 0.5,
                    $"Header text is vertically clipped: '{text.Text}', bounds={bounds}, actual={text.ActualHeight}, desired={text.DesiredSize.Height}.");
            }
            Evidence.Add(new { kind = "header-layout", scale, mode = mode.ToString(), quota, height = overlay.ActualHeight, textLines = textBlocks.Length });
            if (scale == 1 && mode == OverlayDisplayMode.Expanded && quota)
            {
                var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(overlay.ActualWidth * 2), (int)Math.Ceiling(overlay.ActualHeight * 2), 192, 192, PixelFormats.Pbgra32);
                bitmap.Render(shell);
                var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                using var file = System.IO.File.Create(System.IO.Path.Combine(AppContext.BaseDirectory, "header-layout-preview.png"));
                encoder.Save(file);
            }
        }
        finally { overlay.Close(); Pump(); }
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
            // Keep the intended overflow scenario even when header typography is compact.
            overlay.Width = 430 * scale;
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

    private static void RunAuxiliaryWindowScenario(string typeName)
    {
        string root = System.IO.Path.Combine(AppContext.BaseDirectory, "local-test-data", typeName);
        System.IO.Directory.CreateDirectory(root);
        var logger = new SafeLogger(System.IO.Path.Combine(root, "logs"));
        using var service = new ProfileStatusService(new ProfileStatusStore(System.IO.Path.Combine(root, "profile-status.json")), new FixtureUsageProvider(), logger);
        var paths = new AppPaths(root, root);
        var settings = new OverlaySettings { Language = LanguagePreference.ChineseSimplified };
        ProfileInfo[] profiles = [new("auxiliary-test", root, "unused") { DisplayName = "测试账号" }];
        var localizerType = typeof(CodexProfileOverlay.App).Assembly.GetType("CodexProfileOverlay.Localizer")!;
        object localizer = Activator.CreateInstance(localizerType, LanguagePreference.ChineseSimplified)!;
        Type windowType = typeof(CodexProfileOverlay.App).Assembly.GetType("CodexProfileOverlay." + typeName)!;
        var constructor = windowType.GetConstructors().Single();
        var arguments = constructor.GetParameters().Select(parameter => parameter.ParameterType == typeof(OverlaySettings) ? (object)settings
            : parameter.ParameterType == typeof(IReadOnlyList<ProfileInfo>) ? profiles
            : parameter.ParameterType == localizerType ? localizer
            : parameter.ParameterType == typeof(ProfileStatusService) ? service
            : parameter.ParameterType == typeof(BackupMaintenanceService) ? new BackupMaintenanceService(paths)
            : parameter.ParameterType == typeof(Action<OverlaySettings>) ? (Action<OverlaySettings>)(_ => { })
            : parameter.ParameterType == typeof(Func<string, Task>) ? (Func<string, Task>)(_ => Task.CompletedTask)
            : parameter.ParameterType == typeof(Action<ProfileInfo>) ? (Action<ProfileInfo>)(_ => { })
            : parameter.ParameterType == typeof(Action<IReadOnlyList<string>>) ? (Action<IReadOnlyList<string>>)(_ => { })
            : parameter.ParameterType == typeof(string) ? profiles[0].Name
            : (Action)(() => { })).ToArray();
        var host = new Window { Title = "Auxiliary Minimize Regression Fixture", Width = 700, Height = 500, Left = 40, Top = 40, ShowInTaskbar = false };
        host.Show();
        var window = (Window)constructor.Invoke(arguments);
        try
        {
            bool closed = false;
            int stateEvents = 0;
            window.StateChanged += (_, _) => stateEvents++;
            window.Closed += (_, _) => closed = true;
            window.ShowInTaskbar = false;
            // Use the native owner path used for Codex, not a managed WPF Owner.
            new WindowInteropHelper(window).Owner = new WindowInteropHelper(host).Handle;
            window.Show();
            Pump();
            var handle = new WindowInteropHelper(window).Handle;
            object originalContent = window.Content;
            for (int cycle = 1; cycle <= 3; cycle++)
            {
                if (cycle == 2)
                    _ = SendMessage(handle, 0x0112, (IntPtr)0xF020, IntPtr.Zero); // Native title-bar minimize command.
                else
                    window.WindowState = WindowState.Minimized;
                WaitFor(() => !window.IsVisible);
                bool hidden = !window.IsVisible && !IsWindowVisible(handle);
                Evidence.Add(new { kind = "auxiliary-minimized", typeName, cycle, hidden, closed });
                Require(hidden && !closed, $"{typeName} left a visible minimized desktop caption or closed the window: managedVisible={window.IsVisible}, nativeVisible={IsWindowVisible(handle)}, state={window.WindowState}, closed={closed}, stateEvents={stateEvents}.");
                var controllerType = typeof(CodexProfileOverlay.App).Assembly.GetType("CodexProfileOverlay.OverlayController")!;
                controllerType.GetMethod("BringToFront", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [window]);
                Pump();
                bool restored = window.IsVisible && IsWindowVisible(handle) && !IsIconic(handle) && window.WindowState == WindowState.Normal;
                bool reused = ReferenceEquals(originalContent, window.Content) && new WindowInteropHelper(window).Handle == handle;
                Evidence.Add(new { kind = "auxiliary-restored", typeName, cycle, restored, reused });
                Require(restored && reused && !closed, $"{typeName} failed to restore the same window and controls.");
            }
            window.WindowState = WindowState.Minimized;
            window.Close();
            Pump();
            Evidence.Add(new { kind = "auxiliary-close-during-minimize", typeName, closed });
            Require(closed && !IsWindowVisible(handle), $"{typeName} failed to close during a queued minimize.");
        }
        finally { window.Close(); host.Close(); Pump(); }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr handle);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr handle);
    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr handle, int message, IntPtr wParam, IntPtr lParam);

    private sealed class FixtureUsageProvider : IUsageProvider
    {
        public UsageProviderCapability Capability => UsageProviderCapability.Supported;
        public Task<UsageSnapshot?> GetUsageAsync(string directory, CancellationToken token) => Task.FromResult<UsageSnapshot?>(null);
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
    private static void WaitFor(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(2);
        do { Pump(); } while (!condition() && DateTime.UtcNow < deadline);
    }

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
