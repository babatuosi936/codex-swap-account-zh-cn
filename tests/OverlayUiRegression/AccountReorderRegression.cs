using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using CodexProfileOverlay.Core.Models;
using CodexProfileOverlay.Core.Services;
using Button = System.Windows.Controls.Button;
using Point = System.Windows.Point;
using Panel = System.Windows.Controls.Panel;

internal static partial class Program
{
    private static void RunAccountReorderScenario()
    {
        foreach (double scale in new[] { 0.8, 1.0, 1.4 })
        {
            var settings = new OverlaySettings { DisplayMode = OverlayDisplayMode.Expanded, Scale = scale, ShowAutomaticLimitIndicators = true, AnimationsEnabled = scale != 0.8 };
            var overlay = (Window)Activator.CreateInstance(OverlayType, settings, new SafeLogger(System.IO.Path.Combine(AppContext.BaseDirectory, "fixture-logs")))!;
            try
            {
                var original = Enumerable.Range(0, 3).Select(i => new ProfileInfo("sort-" + i, "unused", "unused") { DisplayName = "账号 " + i }).ToArray();
                int saves = 0, switches = 0;
                string[]? saved = null;
                OverlayType.GetProperty("OnSwitchProfile")!.SetValue(overlay, (Action<string>)(_ => switches++));
                OverlayType.GetProperty("OnReorderProfiles")!.SetValue(overlay, (Action<IReadOnlyList<string>>)(order =>
                {
                    saves++; saved = order.ToArray();
                    Call(overlay, "SetProfiles", order.Select(id => original.Single(p => p.Name == id)).ToArray(), original[0].Name);
                }));
                Call(overlay, "SetProfiles", original, original[0].Name);
                Call(overlay, "SetStatusDocument", Document(original, 0), null);
                overlay.Left = 250; overlay.Top = 200; overlay.Show(); Pump();
                Button[] Buttons() => Descendants((DependencyObject)overlay.Content).OfType<Button>().Where(b => b.Tag is string).ToArray();
                string[] Order() => Buttons().Select(b => (string)b.Tag).ToArray();
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var track = OverlayType.GetMethod("TrackAccountHold", flags)!;
                foreach (var account in Buttons())
                {
                    var label = Descendants(account).OfType<TextBlock>().First();
                    var point = account.TransformToAncestor(overlay).Transform(new Point(10, 20));
                    OverlayType.GetField("dragPending", flags)!.SetValue(overlay, true);
                    Require((bool)track.Invoke(overlay, new object[] { label, point })!, "The entire account card, including nested text, must reserve the sorting gesture.");
                    Require(!(bool)OverlayType.GetField("dragPending", flags)!.GetValue(overlay)!, "Account presses must not leave overlay movement pending.");
                    double left = overlay.Left, top = overlay.Top;
                    Call(overlay, "UpdateAccountHold", new Point(point.X + 30, point.Y));
                    Require(ReferenceEquals(account, OverlayType.GetField("accountHoldButton", flags)!.GetValue(overlay)), "Moving during the hold delay must retain the account gesture rather than switch to overlay movement.");
                    Require(overlay.Left == left && overlay.Top == top, "Account movement must not change the overlay position.");
                    var release = new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice,
                        Environment.TickCount, System.Windows.Input.MouseButton.Left) { RoutedEvent = System.Windows.Input.Mouse.PreviewMouseUpEvent };
                    OverlayType.GetMethod("OnMouseLeftButtonUp", flags | System.Reflection.BindingFlags.DeclaredOnly)!.Invoke(overlay, new object[] { overlay, release });
                    Require(release.Handled && switches == 0 && saves == 0, "A moved tab released before the hold delay must not switch accounts or save a new overlay position.");
                }
                foreach (var region in Descendants((DependencyObject)overlay.Content).OfType<Button>().Where(b => b.Tag is not string).Cast<DependencyObject>().Append((DependencyObject)overlay.Content))
                    Require(!(bool)track.Invoke(overlay, new object[] { region, new Point() })!, "Overview, collapse controls and background must remain available for whole-overlay dragging.");
                Evidence.Add(new { scenario = "account-drag-regions", scale });
                void StartSort(Button source)
                {
                    // Seed a held-pointer gesture without generating real desktop mouse
                    // input; Windows sees the real released button in this fixture.
                    OverlayType.GetField("accountHoldButton", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(overlay, source);
                    OverlayType.GetField("accountReordering", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(overlay, true);
                    var hover = OverlayType.GetField("usageHover", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(overlay)!;
                    hover.GetType().GetProperty("Suspended")!.SetValue(hover, true);
                    OverlayType.GetField("accountHoldPoint", flags)!.SetValue(overlay,
                        source.TransformToAncestor(overlay).Transform(new Point(source.ActualWidth / 2, 20)));
                    Call(overlay, "InitializeAccountDragVisuals", source);
                    source.Opacity = 0.95;
                }
                foreach (bool toRight in new[] { true, false })
                {
                    var buttons = Buttons();
                    var source = toRight ? buttons[0] : buttons[^1];
                    StartSort(source);
                    source.RaiseEvent(new System.Windows.Input.MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount)
                        { RoutedEvent = System.Windows.Input.Mouse.MouseEnterEvent });
                    for (int i = 0; i < 3; i++) Pump();
                    Require(!((System.Windows.Controls.Primitives.Popup)source.ToolTip).IsOpen, "Quota popups must not open over an active sorting gesture.");
                    buttons.Single(b => (string)b.Tag == "sort-1").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Require(switches == 0, "Sorting must suppress account switching.");
                    Call(overlay, "SetStatusDocument", Document(original, saves + 1), null);
                    Require(ReferenceEquals(source, Buttons().Single(b => Equals(b.Tag, source.Tag))), "Quota refresh must not rebuild tabs during sorting.");
                    Point drop = (toRight ? buttons[^1] : buttons[0]).TransformToAncestor(overlay)
                        .Transform(new Point(toRight ? buttons[^1].ActualWidth + 3 : -3, 20));
                    Call(overlay, "UpdateAccountReorder", drop);
                    var sourceShift = ((System.Windows.Media.TransformGroup)source.RenderTransform).Children.OfType<System.Windows.Media.TranslateTransform>().Last();
                    Require(Math.Abs(sourceShift.X) > 10 && Panel.GetZIndex(source) == 100, "The grabbed account must follow the pointer and render above neighboring tabs.");
                    Require(Math.Abs(source.TransformToAncestor(overlay).Transform(new Point(source.ActualWidth / 2, 20)).X - drop.X) < 0.5,
                        "The pointer grip must stay aligned with the grabbed card at every overlay scale.");
                    var neighborShifts = buttons.Where(b => !ReferenceEquals(b, source)).Select(b =>
                        ((System.Windows.Media.TransformGroup)b.RenderTransform).Children.OfType<System.Windows.Media.TranslateTransform>().Last()).ToArray();
                    Require(neighborShifts.Any(shift => Math.Abs((double)shift.GetAnimationBaseValue(System.Windows.Media.TranslateTransform.XProperty)) > 10),
                        "Neighboring tabs must move into preview slots before release.");
                    Require(neighborShifts.Any(shift => shift.HasAnimatedProperties) == settings.AnimationsEnabled,
                        "Preview motion must animate only when animations are enabled.");
                    int insertion = (int)OverlayType.GetField("accountInsertion", flags)!.GetValue(overlay)!;
                    Call(overlay, "UpdateAccountReorder", drop);
                    Require(insertion == (int)OverlayType.GetField("accountInsertion", flags)!.GetValue(overlay)!, "Animated neighbor positions must not change insertion hit testing.");
                    if (!settings.AnimationsEnabled)
                        Require(buttons.Any(b => b.BorderThickness.Left == 3 || b.BorderThickness.Right == 3), "Without animation, sorting must indicate the insertion edge.");
                    Call(overlay, "FinishAccountReorder", true); Pump();
                    var expected = toRight ? new[] { "sort-1", "sort-2", "sort-0" } : new[] { "sort-0", "sort-1", "sort-2" };
                    Require(saved!.SequenceEqual(expected) && Order().SequenceEqual(expected), "Moving the first/last account must persist and render the new order.");
                    Require(!overlay.IsMouseCaptured && switches == 0, "Completing sorting must release capture without switching accounts.");
                    for (int i = 0; i < 2; i++) Pump();
                    Require(Buttons().All(b => !b.RenderTransform.HasAnimatedProperties && b.RenderTransform.Value.IsIdentity),
                        "After settling, tab transforms must return to normal without accumulating offsets.");
                    Require(AutomationProperties.GetAutomationId(Descendants((DependencyObject)overlay.Content).OfType<Button>().First()) == "AccountsOverview",
                        "The fixed Overview entry must remain first.");
                    Evidence.Add(new { scenario = "account-reorder", scale, toRight });
                }
                var middle = Buttons()[1];
                StartSort(middle);
                Call(overlay, "FinishAccountReorder", false); Pump();
                Require(saves == 2 && Order().SequenceEqual(original.Select(p => p.Name)), "Cancelling a reorder must retain the stored order.");
                middle = Buttons()[1];
                StartSort(middle);
                overlay.Hide(); Pump();
                Require(!overlay.IsMouseCaptured && saves == 2, "Hiding the overlay must cancel an unfinished reorder.");
                Evidence.Add(new { scenario = "account-reorder-cancel", scale });
            }
            finally { overlay.Close(); Pump(); }
        }
    }
}
