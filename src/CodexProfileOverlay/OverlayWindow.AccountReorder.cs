using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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
        source.Opacity = 0.55;
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
            else if (point.X > origin.X + scroll.ActualWidth - 24) scroll.ScrollToHorizontalOffset(scroll.HorizontalOffset + 18);
        }
        accountInsertion = remaining.Count(button =>
            point.X > button.TransformToAncestor(this).Transform(new Point(button.ActualWidth / 2, 0)).X);
        ClearAccountDropCue();
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
    }
}
