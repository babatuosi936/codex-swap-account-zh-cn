using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Button = System.Windows.Controls.Button;
using Brushes = System.Windows.Media.Brushes;
using Forms = System.Windows.Forms;
using Point = System.Windows.Point;

internal static partial class Program
{
    // Exercise HWND placement using synthetic windows only. No account directories,
    // controller, network requests, or real Codex windows are opened by this fixture.
    private static void RunAccountActionPlacementScenario()
    {
        Assembly assembly = typeof(CodexProfileOverlay.App).Assembly;
        Type dialogType = assembly.GetType("CodexProfileOverlay.AccountActionDialog")!;
        MethodInfo placeBelow = assembly.GetType("CodexProfileOverlay.DialogPlacement")!
            .GetMethod("BelowAnchor", BindingFlags.Public | BindingFlags.Static)!;
        Require(placeBelow is not null, "Account action dialog must support placement below the whole overlay.");

        var workArea = Forms.Screen.PrimaryScreen!.WorkingArea;
        var cases = new[] { "top-wide-header", "custom-middle", "bottom-fallback", "left-edge", "right-edge", "loaded-recheck", "constrained-space" };
        foreach (string scenario in cases)
        {
            var anchor = new Window
            {
                Width = 780,
                Height = 60,
                WindowStartupLocation = WindowStartupLocation.Manual,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                ShowInTaskbar = false,
                Content = new Border
                {
                    Background = Brushes.White,
                    BorderBrush = Brushes.MediumPurple,
                    BorderThickness = new Thickness(1),
                    Child = new TextBlock { Text = "总览    主账号    475账号    199账号", FontSize = 18, Margin = new Thickness(12) },
                },
            };
            var dialog = (Window)dialogType.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).Single().Invoke(new object[]
            {
                "使用账号“475账号”",
                "打开独立窗口会保留当前 Codex 窗口；切换当前账户会关闭当前 Codex 后切换登录。",
                "打开新的独立窗口",
                "切换当前账户",
                "取消",
                true,
            });
            try
            {
                anchor.Show();
                Pump();
                IntPtr anchorHandle = new WindowInteropHelper(anchor).Handle;
                Matrix anchorDpi = HwndSource.FromHwnd(anchorHandle)!.CompositionTarget!.TransformToDevice;
                int anchorWidth = Math.Min(workArea.Width - 16, (int)Math.Ceiling(780 * anchorDpi.M11));
                int anchorHeight = Math.Min(workArea.Height / 5, (int)Math.Ceiling(60 * anchorDpi.M22));
                if (scenario is "left-edge" or "right-edge")
                    anchorWidth = Math.Min(workArea.Width - 16, (int)Math.Ceiling(250 * anchorDpi.M11));
                int anchorLeft = workArea.Left + (workArea.Width - anchorWidth) / 2;
                int anchorTop = workArea.Top + 16;
                if (scenario == "custom-middle" || scenario == "loaded-recheck")
                {
                    anchorTop = workArea.Top + workArea.Height / 3;
                    anchorLeft = workArea.Left + Math.Min(137, workArea.Width - anchorWidth - 8);
                }
                if (scenario == "bottom-fallback") anchorTop = workArea.Bottom - anchorHeight - 12;
                if (scenario == "left-edge") anchorLeft = workArea.Left + 2;
                if (scenario == "right-edge") anchorLeft = workArea.Right - anchorWidth - 2;
                if (scenario == "constrained-space")
                {
                    anchorTop = workArea.Top + 40;
                    anchorHeight = workArea.Height - 130;
                }
                SetPlacementFixtureBounds(anchor, anchorLeft, anchorTop, anchorWidth, anchorHeight);
                Pump();
                dialog.Owner = anchor;
                IntPtr dialogHandle = new WindowInteropHelper(dialog).EnsureHandle();
                AccountPlacementRect unplacedDialog = GetPlacementFixtureBounds(dialogHandle);
                placeBelow!.Invoke(null, [dialog, anchor]);
                AccountPlacementRect beforeShow = GetPlacementFixtureBounds(dialogHandle);
                AccountPlacementRect anchorBeforeShow = GetPlacementFixtureBounds(anchorHandle);
                bool beforeShowSeparate = beforeShow.Bottom <= anchorBeforeShow.Top || beforeShow.Top >= anchorBeforeShow.Bottom;
                Require(beforeShowSeparate, $"Account dialog overlaps the overlay before Show: {scenario}.");
                Evidence.Add(new { kind = "account-action-placement", scenario, stage = "before-show", beforeShowSeparate });

                // The placement is registered before ShowDialog. The Loaded pass must
                // follow the anchor's latest position, rather than the earlier one.
                if (scenario == "loaded-recheck")
                {
                    SetPlacementFixtureBounds(anchor, workArea.Left + (workArea.Width - anchorWidth) / 2,
                        workArea.Top + 30, anchorWidth, anchorHeight);
                    Pump();
                }
                dialog.Show();
                Pump();
                AccountPlacementRect actualAnchor = GetPlacementFixtureBounds(anchorHandle);
                AccountPlacementRect actualDialog = GetPlacementFixtureBounds(dialogHandle);
                Matrix dialogDpi = HwndSource.FromHwnd(dialogHandle)!.CompositionTarget!.TransformToDevice;
                double expectedGap = 8 * dialogDpi.M22;
                bool above = scenario == "bottom-fallback";
                double gap = above ? actualAnchor.Top - actualDialog.Bottom : actualDialog.Top - actualAnchor.Bottom;
                bool separate = actualDialog.Bottom <= actualAnchor.Top || actualDialog.Top >= actualAnchor.Bottom;
                bool visible = actualDialog.Left >= workArea.Left - 2 && actualDialog.Top >= workArea.Top - 2
                    && actualDialog.Right <= workArea.Right + 2 && actualDialog.Bottom <= workArea.Bottom + 2;
                double idealLeft = actualAnchor.Left + (actualAnchor.Width - actualDialog.Width) / 2.0;
                double expectedLeft = Math.Clamp(idealLeft, workArea.Left, workArea.Right - actualDialog.Width);
                bool centeredOrClamped = Math.Abs(actualDialog.Left - expectedLeft) <= 3;
                bool attached = Math.Abs(gap - expectedGap) <= 3;
                foreach ((string stage, bool passed) in new[]
                {
                    ("does-not-cover-account-bar", separate),
                    ("visible-in-working-area", visible),
                    ("centered-on-whole-account-bar", centeredOrClamped),
                    (above ? "falls-back-above-with-gap" : "directly-below-with-gap", attached),
                })
                {
                    Evidence.Add(new
                    {
                        kind = "account-action-placement", scenario, stage, passed, gap, expectedGap,
                        anchor = new { actualAnchor.Left, actualAnchor.Top, actualAnchor.Right, actualAnchor.Bottom },
                        dialog = new { actualDialog.Left, actualDialog.Top, actualDialog.Right, actualDialog.Bottom },
                    });
                    Require(passed, $"Account dialog placement failed at {scenario}/{stage}: "
                        + $"anchor=({actualAnchor.Left},{actualAnchor.Top})..({actualAnchor.Right},{actualAnchor.Bottom}), "
                        + $"dialog=({actualDialog.Left},{actualDialog.Top})..({actualDialog.Right},{actualDialog.Bottom}), gap={gap}, expectedGap={expectedGap}.");
                }
                if (scenario == "constrained-space")
                {
                    bool shrunk = actualDialog.Height < unplacedDialog.Height;
                    Require(shrunk, "When neither side fits, the account dialog must shrink to the larger free region without covering the bar.");
                    Evidence.Add(new { kind = "account-action-placement", scenario, stage = "shrinks-instead-of-covering-bar", shrunk });
                }
                Button[] buttons = Descendants((DependencyObject)dialog.Content).OfType<Button>().ToArray();
                Button open = buttons.Single(button => Equals(button.Content, "打开新的独立窗口"));
                Button switchCurrent = buttons.Single(button => Equals(button.Content, "切换当前账户"));
                Require(open.IsDefault && !switchCurrent.IsDefault, "Placement must preserve the default independent-window action.");
                Evidence.Add(new { kind = "account-action-placement", scenario, stage = "independent-window-remains-default", independentWindowDefault = true });
            }
            finally
            {
                dialog.Close();
                anchor.Close();
                Pump();
            }
        }
        RunAccountActionShowPlacementIntegration(dialogType);
    }

    // Cover the production caller too: a correct placement helper alone would
    // not fix the visible bug if AccountActionDialog.Show kept centering on owner.
    private static void RunAccountActionShowPlacementIntegration(Type dialogType)
    {
        var anchor = new Window
        {
            Width = 780,
            Height = 60,
            WindowStartupLocation = WindowStartupLocation.Manual,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            Content = new Border { Background = Brushes.White },
        };
        Window? modal = null;
        Exception? observedFailure = null;
        bool observed = false;
        var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(100) };
        timer.Tick += (_, _) =>
        {
            modal = System.Windows.Application.Current.Windows.Cast<Window>().SingleOrDefault(window => window.GetType() == dialogType);
            if (modal is null || !modal.IsLoaded) return;
            timer.Stop();
            try
            {
                observed = true;
                IntPtr anchorHandle = new WindowInteropHelper(anchor).Handle;
                IntPtr dialogHandle = new WindowInteropHelper(modal).Handle;
                AccountPlacementRect actualAnchor = GetPlacementFixtureBounds(anchorHandle);
                AccountPlacementRect actualDialog = GetPlacementFixtureBounds(dialogHandle);
                var workArea = Forms.Screen.FromHandle(anchorHandle).WorkingArea;
                Matrix dialogDpi = HwndSource.FromHwnd(dialogHandle)!.CompositionTarget!.TransformToDevice;
                double gap = actualDialog.Top - actualAnchor.Bottom;
                double expectedGap = 8 * dialogDpi.M22;
                double expectedLeft = Math.Clamp(actualAnchor.Left + (actualAnchor.Width - actualDialog.Width) / 2.0,
                    workArea.Left, workArea.Right - actualDialog.Width);
                Button[] buttons = Descendants((DependencyObject)modal.Content).OfType<Button>().ToArray();
                bool defaultIndependent = buttons.Single(button => Equals(button.Content, "打开新的独立窗口")).IsDefault
                    && !buttons.Single(button => Equals(button.Content, "切换当前账户")).IsDefault;
                foreach ((string stage, bool passed) in new[]
                {
                    ("production-show-does-not-cover-bar", actualDialog.Top >= actualAnchor.Bottom),
                    ("production-show-is-directly-below", Math.Abs(gap - expectedGap) <= 3),
                    ("production-show-centers-on-whole-bar", Math.Abs(actualDialog.Left - expectedLeft) <= 3),
                    ("production-show-stays-visible", actualDialog.Left >= workArea.Left - 2 && actualDialog.Top >= workArea.Top - 2
                        && actualDialog.Right <= workArea.Right + 2 && actualDialog.Bottom <= workArea.Bottom + 2),
                    ("production-show-preserves-default-action", defaultIndependent),
                })
                {
                    Evidence.Add(new { kind = "account-action-placement", scenario = "production-show", stage, passed, gap, expectedGap });
                    Require(passed, $"AccountActionDialog.Show integration failed at {stage}: "
                        + $"anchor=({actualAnchor.Left},{actualAnchor.Top})..({actualAnchor.Right},{actualAnchor.Bottom}), "
                        + $"dialog=({actualDialog.Left},{actualDialog.Top})..({actualDialog.Right},{actualDialog.Bottom}).");
                }
            }
            catch (Exception error) { observedFailure = error; }
            finally { modal.Close(); }
        };
        try
        {
            anchor.Show();
            Pump();
            IntPtr anchorHandle = new WindowInteropHelper(anchor).Handle;
            var workArea = Forms.Screen.FromHandle(anchorHandle).WorkingArea;
            Matrix anchorDpi = HwndSource.FromHwnd(anchorHandle)!.CompositionTarget!.TransformToDevice;
            int width = Math.Min(workArea.Width - 16, (int)Math.Ceiling(780 * anchorDpi.M11));
            int height = (int)Math.Ceiling(60 * anchorDpi.M22);
            SetPlacementFixtureBounds(anchor, workArea.Left + (workArea.Width - width) / 2, workArea.Top + 12, width, height);
            Pump();
            timer.Start();
            object? choice = dialogType.GetMethod("Show", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, new object[]
            {
                anchor, IntPtr.Zero, "使用账号“475账号”",
                "打开独立窗口会保留当前 Codex 窗口；切换当前账户会关闭当前 Codex 后切换登录。",
                "打开新的独立窗口", "切换当前账户", "取消", true,
            });
            if (observedFailure is not null) throw new InvalidOperationException("Production account-action placement regression.", observedFailure);
            Require(observed, "The production account-action dialog was never observed after Loaded.");
            Require(choice?.ToString() == "Cancel", "The placement integration fixture must close without dispatching an account action.");
        }
        finally
        {
            timer.Stop();
            modal?.Close();
            anchor.Close();
            Pump();
        }
    }

    private static void SetPlacementFixtureBounds(Window window, int left, int top, int width, int height)
    {
        IntPtr handle = new WindowInteropHelper(window).EnsureHandle();
        Matrix fromDevice = HwndSource.FromHwnd(handle)!.CompositionTarget!.TransformFromDevice;
        Point logical = fromDevice.Transform(new Point(left, top));
        window.Left = logical.X;
        window.Top = logical.Y;
        window.Width = width * fromDevice.M11;
        window.Height = height * fromDevice.M22;
        const uint noZOrderOrActivation = 0x0004 | 0x0010;
        Require(AccountPlacementSetWindowPos(handle, IntPtr.Zero, left, top, width, height, noZOrderOrActivation), "Could not position the synthetic overlay anchor.");
    }

    private static AccountPlacementRect GetPlacementFixtureBounds(IntPtr handle)
    {
        Require(AccountPlacementGetWindowRect(handle, out AccountPlacementRect bounds), "Could not read the synthetic window's physical bounds.");
        return bounds;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AccountPlacementRect
    {
        public int Left, Top, Right, Bottom;
        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowRect")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AccountPlacementGetWindowRect(IntPtr handle, out AccountPlacementRect rectangle);

    [DllImport("user32.dll", EntryPoint = "SetWindowPos")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AccountPlacementSetWindowPos(IntPtr handle, IntPtr insertAfter, int left, int top, int width, int height, uint flags);
}
