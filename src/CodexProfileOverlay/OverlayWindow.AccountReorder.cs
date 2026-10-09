using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Button = System.Windows.Controls.Button;
using Point = System.Windows.Point;

namespace CodexProfileOverlay;

internal sealed partial class OverlayWindow
{
    private readonly DispatcherTimer accountHoldTimer = new() { Interval = TimeSpan.FromMilliseconds(450) };
    private readonly DispatcherTimer accountScrollTimer = new() { Interval = TimeSpan.FromMilliseconds(80) };
    private Button? accountHoldButton;
    private Point accountHoldPoint;
    private bool accountReordering;
    private bool accountPressMoved;
    private int accountInsertion;
    private Button? accountDropCue;
    private Thickness savedCueThickness;
    private Brush? savedCueBrush;
    private sealed class AccountDragVisual
    {
        internal required Transform Original;
        internal required TranslateTransform Shift;
        internal required double Slot;
        internal required double Span;
        internal int ZIndex;
        internal double Target;
    }
    private readonly Dictionary<Button, AccountDragVisual> accountDragVisuals = new();
    private readonly Dictionary<Button, (TransformGroup Group, Transform Original)> accountSettleVisuals = new();
    private double accountPointerOffset;

    private void InitializeAccountDragVisuals(Button source)
    {
        ClearAccountSettleVisuals();
        foreach (var button in profileButtons)
        {
            var shift = new TranslateTransform();
            var original = button.RenderTransform;
            var group = new TransformGroup();
            group.Children.Add(original);
            group.Children.Add(shift);
            accountDragVisuals[button] = new AccountDragVisual { Original = original, Shift = shift,
                Slot = button.TranslatePoint(new Point(), (UIElement)System.Windows.Media.VisualTreeHelper.GetParent(button)).X,
                Span = button.ActualWidth + button.Margin.Left + button.Margin.Right, ZIndex = Panel.GetZIndex(button) };
            button.RenderTransform = group;
        }
        accountPointerOffset = accountHoldPoint.X - source.TransformToAncestor(this).Transform(new Point()).X;
        Panel.SetZIndex(source, 100);
    }

    private void AnimateAccountShift(TranslateTransform shift, double destination)
    {
        double start = shift.X;
        shift.BeginAnimation(TranslateTransform.XProperty, null);
        shift.X = destination;
        if (!settings.AnimationsEnabled) return;
        shift.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(start, destination, TimeSpan.FromMilliseconds(160))
        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop });
    }

    private void UpdateAccountDragVisuals(Point point)
    {
        if (accountHoldButton is null || !accountDragVisuals.TryGetValue(accountHoldButton, out var source)) return;
        double baseLeft = accountHoldButton.TransformToAncestor(this).Transform(new Point()).X - source.Shift.X * SanitizedScale;
        source.Shift.X = (point.X - accountPointerOffset - baseLeft) / SanitizedScale;
        var preview = profileButtons.Where(button => !ReferenceEquals(button, accountHoldButton)).ToList();
        preview.Insert(Math.Clamp(accountInsertion, 0, preview.Count), accountHoldButton);
        double slot = accountDragVisuals[profileButtons[0]].Slot;
        foreach (var button in preview)
        {
            var visual = accountDragVisuals[button];
            double target = slot - visual.Slot;
            if (!ReferenceEquals(button, accountHoldButton) && Math.Abs(visual.Target - target) > 0.01)
            {
                visual.Target = target;
                AnimateAccountShift(visual.Shift, target);
            }
            slot += visual.Span;
        }
    }

    private Dictionary<string, double> ResetAccountDragVisuals()
    {
        var positions = accountDragVisuals.Keys.ToDictionary(button => (string)button.Tag,
            button => button.TransformToAncestor(this).Transform(new Point()).X);
        foreach (var pair in accountDragVisuals)
        {
            pair.Value.Shift.BeginAnimation(TranslateTransform.XProperty, null);
            pair.Key.RenderTransform = pair.Value.Original;
            Panel.SetZIndex(pair.Key, pair.Value.ZIndex);
        }
        accountDragVisuals.Clear();
        return positions;
    }

    private void SettleAccountTabs(Dictionary<string, double> previousPositions)
    {
        if (!IsVisible || !settings.AnimationsEnabled) return;
        ClearAccountSettleVisuals();
        UpdateLayout();
        foreach (var button in profileButtons)
        {
            if (!previousPositions.TryGetValue((string)button.Tag, out double previous)) continue;
            double offset = (previous - button.TransformToAncestor(this).Transform(new Point()).X) / SanitizedScale;
            if (Math.Abs(offset) < 0.5) continue;
            var original = button.RenderTransform;
            var shift = new TranslateTransform(offset, 0);
            var group = new TransformGroup(); group.Children.Add(original); group.Children.Add(shift);
            button.RenderTransform = group;
            accountSettleVisuals[button] = (group, original);
            var animation = new DoubleAnimation(offset, 0, TimeSpan.FromMilliseconds(160))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop };
            animation.Completed += (_, _) =>
            {
                if (ReferenceEquals(button.RenderTransform, group)) button.RenderTransform = original;
                if (accountSettleVisuals.TryGetValue(button, out var visual) && ReferenceEquals(visual.Group, group)) accountSettleVisuals.Remove(button);
            };
            shift.X = 0;
            shift.BeginAnimation(TranslateTransform.XProperty, animation);
        }
    }

    private void ClearAccountSettleVisuals()
    {
        foreach (var pair in accountSettleVisuals)
            if (ReferenceEquals(pair.Key.RenderTransform, pair.Value.Group)) pair.Key.RenderTransform = pair.Value.Original;
        accountSettleVisuals.Clear();
    }

    private void InitializeAccountReorder()
    {
        accountHoldTimer.Tick += (_, _) =>
        {
            accountHoldTimer.Stop();
            if (accountHoldButton is not null && Mouse.LeftButton == MouseButtonState.Pressed && !isDragging)
                BeginAccountReorder(accountHoldButton);
        };
        accountScrollTimer.Tick += (_, _) => UpdateAccountReorder(Mouse.GetPosition(this));
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && accountReordering)
            {
                FinishAccountReorder(commit: false);
                e.Handled = true;
            }
        };
        Closed += (_, _) => FinishAccountReorder(commit: false);
    }

    private bool TrackAccountHold(DependencyObject? element, Point point)
    {
        FinishAccountReorder(commit: false);
        if (currentMode != Core.Models.OverlayDisplayMode.Expanded) return false;
        while (element is not null && element is not Button)
            element = element is Visual ? VisualTreeHelper.GetParent(element) : null;
        if (element is not Button button || !profileButtons.Contains(button)) return false;
        // Account tabs never participate in moving the overlay, even before
        // the hold delay has elapsed. All other header regions retain dragging.
        dragPending = false;
        accountHoldButton = button;
        accountHoldPoint = point;
        if (!isSwitching && profiles.Count >= 2) accountHoldTimer.Start();
        return true;
    }

    private void UpdateAccountHold(Point point)
    {
        if (accountHoldButton is null) return;
        if (Math.Abs(point.X - accountHoldPoint.X) >= SystemParameters.MinimumHorizontalDragDistance
            || Math.Abs(point.Y - accountHoldPoint.Y) >= SystemParameters.MinimumVerticalDragDistance)
        {
            accountPressMoved = true;
        }
    }

    private void BeginAccountReorder(Button source)
    {
        accountHoldTimer.Stop();
        dragPending = false;
        usageHover.Close();
        usageHover.Suspended = true;
        accountHoldButton = source;
        accountReordering = true;
        accountInsertion = profiles.ToList().FindIndex(profile => profile.Name == (string)source.Tag);
        if (!CaptureMouse()) { FinishAccountReorder(commit: false); return; }
        if (!accountReordering) return;
        InitializeAccountDragVisuals(source);
        source.Opacity = 0.95;
        Cursor = Cursors.SizeWE;
        accountScrollTimer.Start();
    }

    private void UpdateAccountReorder(Point point)
    {
        if (!accountReordering || accountHoldButton is null) return;
        var remaining = profileButtons.Where(button => !ReferenceEquals(button, accountHoldButton)).ToArray();
        DependencyObject? ancestor = VisualTreeHelper.GetParent(accountHoldButton);
        while (ancestor is not null && ancestor is not ScrollViewer) ancestor = VisualTreeHelper.GetParent(ancestor);
        if (ancestor is ScrollViewer scroll)
        {
            var origin = scroll.TransformToAncestor(this).Transform(new Point());
            if (point.X < origin.X + 24) scroll.ScrollToHorizontalOffset(scroll.HorizontalOffset - 18);
            else if (point.X > origin.X + scroll.ActualWidth * SanitizedScale - 24) scroll.ScrollToHorizontalOffset(scroll.HorizontalOffset + 18);
        }
        // Hit-test stable layout slots, not the currently animated positions,
        // so cards do not oscillate as neighbors slide beneath the pointer.
        accountInsertion = remaining.Count(button => point.X > button.TransformToAncestor(this)
            .Transform(new Point(button.ActualWidth / 2, 0)).X
            - (accountDragVisuals.TryGetValue(button, out var visual) ? visual.Shift.X * SanitizedScale : 0));
        UpdateAccountDragVisuals(point);
        ClearAccountDropCue();
        // The animated gap is the insertion cue. Avoid changing border widths
        // during animation, which would remeasure and nudge the tab positions.
        if (settings.AnimationsEnabled && accountDragVisuals.Count > 0) return;
        if (remaining.Length == 0) return;
        accountDropCue = remaining[Math.Min(accountInsertion, remaining.Length - 1)];
        savedCueThickness = accountDropCue.BorderThickness;
        savedCueBrush = accountDropCue.BorderBrush;
        accountDropCue.BorderBrush = FindBrush("AccentBrush");
        accountDropCue.BorderThickness = accountInsertion == remaining.Length ? new Thickness(0, 0, 3, 0) : new Thickness(3, 0, 0, 0);
    }

    private void ClearAccountDropCue()
    {
        if (accountDropCue is null) return;
        accountDropCue.BorderThickness = savedCueThickness;
        accountDropCue.BorderBrush = savedCueBrush;
        accountDropCue = null;
    }

    private void FinishAccountReorder(bool commit)
    {
        accountHoldTimer.Stop();
        accountScrollTimer.Stop();
        bool wasReordering = accountReordering;
        var previousPositions = ResetAccountDragVisuals();
        string? source = accountHoldButton?.Tag as string;
        if (accountHoldButton is not null) accountHoldButton.Opacity = 1;
        ClearAccountDropCue();
        accountHoldButton = null;
        accountReordering = false;
        accountPressMoved = false;
        usageHover.Suspended = false;
        if (!wasReordering) return;
        Cursor = null;
        if (IsMouseCaptured) ReleaseMouseCapture();
        if (commit && source is not null)
        {
            var order = profiles.Select(profile => profile.Name).ToList();
            int oldIndex = order.IndexOf(source);
            order.Remove(source);
            order.Insert(Math.Clamp(accountInsertion, 0, order.Count), source);
            if (oldIndex != accountInsertion)
            {
                try { OnReorderProfiles?.Invoke(order); }
                catch (Exception exception) { logger.Error("Failed to save account order.", exception); }
            }
        }
        if (quotaRebuildPending)
        {
            quotaRebuildPending = false;
            RebuildContent();
        }
        SettleAccountTabs(previousPositions);
    }
}
