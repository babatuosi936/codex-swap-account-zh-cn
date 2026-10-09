using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;

namespace CodexProfileOverlay;

// Own the hover lifetime: the built-in tooltip service closes as soon as
// the pointer leaves its owner, before it can cross the gap to the card.
internal sealed class UsageHoverController
{
    private readonly DispatcherTimer timer = new();
    private Popup? current;
    private bool opening;
    private bool overAccount;
    private bool overCard;
    private bool pinned;

    public UsageHoverController()
    {
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (current is null) return;
            if (opening && overAccount) current.IsOpen = true;
            else if (!pinned && !overAccount && !overCard) Close();
        };
    }

    public void Attach(FrameworkElement account, Popup card, FrameworkElement? placementTarget = null, bool openOnClick = false)
    {
        card.PlacementTarget = placementTarget ?? account;
        card.StaysOpen = true;
        card.IsHitTestVisible = true;
        card.Focusable = false;
        ToolTipService.SetIsEnabled(account, false);
        account.MouseEnter += (_, _) =>
        {
            if (pinned) return;
            if (!ReferenceEquals(current, card))
            {
                Close();
                current = card;
            }
            overAccount = true;
            timer.Stop();
            if (!card.IsOpen) Schedule(true, 250);
        };
        account.MouseLeave += (_, _) =>
        {
            if (!ReferenceEquals(current, card)) return;
            overAccount = false;
            if (!pinned) Schedule(false, 500);
        };
        card.Child.MouseEnter += (_, _) =>
        {
            if (!ReferenceEquals(current, card)) return;
            overCard = true;
            timer.Stop();
        };
        card.Child.MouseLeave += (_, _) =>
        {
            if (!ReferenceEquals(current, card)) return;
            overCard = false;
            if (!pinned) Schedule(false, 500);
        };
        account.Unloaded += (_, _) => { if (ReferenceEquals(current, card)) Close(); };
        if (openOnClick && account is Button button)
        {
            button.Click += (_, _) =>
            {
                if (!ReferenceEquals(current, card)) Close();
                timer.Stop();
                current = card;
                pinned = true;
                card.StaysOpen = false;
                card.IsOpen = true;
            };
            card.Closed += (_, _) =>
            {
                card.StaysOpen = true;
                if (ReferenceEquals(current, card))
                {
                    timer.Stop();
                    current = null;
                    pinned = false;
                    overAccount = false;
                    overCard = false;
                }
            };
        }
        else account.PreviewMouseDown += (_, _) => Close();
        card.Child.PreviewKeyDown += (_, args) =>
        {
            if (args.Key == Key.Escape) { Close(); args.Handled = true; }
        };
    }

    public void Close()
    {
        timer.Stop();
        if (current is not null) current.IsOpen = false;
        current = null;
        pinned = false;
        overAccount = false;
        overCard = false;
    }

    private void Schedule(bool show, int milliseconds)
    {
        timer.Stop();
        opening = show;
        timer.Interval = TimeSpan.FromMilliseconds(milliseconds);
        timer.Start();
    }
}
