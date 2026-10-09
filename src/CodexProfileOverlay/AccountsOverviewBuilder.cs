using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using CodexProfileOverlay.Core.Models;
using CodexProfileOverlay.Core.Services;

namespace CodexProfileOverlay;

internal static class AccountsOverviewBuilder
{
    internal static int ResolveColumns(int count, double availableWidth)
        => Math.Max(1, Math.Min(Math.Min(count, 3), (int)Math.Floor((availableWidth - 46) / 320)));

    private static Size WorkingArea(Popup popup)
    {
        if (popup.PlacementTarget is Visual target && PresentationSource.FromVisual(target)?.CompositionTarget is { } composition)
        {
            var point = target.PointToScreen(new Point());
            var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)point.X, (int)point.Y));
            var area = screen.WorkingArea;
            var size = composition.TransformFromDevice.Transform(new Vector(area.Width, area.Height));
            return new Size(size.X, size.Y);
        }
        return SystemParameters.WorkArea.Size;
    }

    public static Popup Build(IReadOnlyList<ProfileInfo> profiles, string? active, Func<string, UsageSnapshot?> snapshotFor, LanguagePreference language, Func<Task>? refreshAll = null)
    {
        Brush Brush(string key) => (Brush)Application.Current.FindResource(key);
        string Text(string key) => LocalizationCatalog.Text(language, key);
        var surface = new Border { Padding = new Thickness(8), Background = Brushes.Transparent };
        var refreshState = new QuotaRefreshButtonBuilder.State();
        var popup = new Popup
        {
            Child = surface, AllowsTransparency = true, StaysOpen = true, Placement = PlacementMode.Custom,
            VerticalOffset = 4,
            CustomPopupPlacementCallback = (size, target, offset) =>
            [new(new Point((target.Width - size.Width) / 2 + offset.X, target.Height + offset.Y), PopupPrimaryAxis.Horizontal),
             new(new Point((target.Width - size.Width) / 2 + offset.X, -size.Height - offset.Y), PopupPrimaryAxis.Horizontal)],
        };
        void Refresh()
        {
            var area = WorkingArea(popup);
            int columns = ResolveColumns(profiles.Count, area.Width - 32);
            var list = new Grid { Tag = "OverviewCards" };
            for (int column = 0; column < columns; column++) list.ColumnDefinitions.Add(new ColumnDefinition());
            for (int row = 0; row < (profiles.Count + columns - 1) / columns; row++)
                list.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            int index = 0;
            foreach (var profile in profiles)
            {
                var snapshot = snapshotFor(profile.Name);
                var heading = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
                var badge = new TextBlock { Text = profile.Name == active ? Text("Active") : snapshot?.IsStale == true ? Text("UsageDataStale") : "",
                    FontSize = 11, Foreground = Brush("MutedTextBrush"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
                DockPanel.SetDock(badge, Dock.Right);
                heading.Children.Add(badge);
                heading.Children.Add(new TextBlock { Text = profile.DisplayName, FontSize = 14, FontWeight = FontWeights.SemiBold,
                    Foreground = Brush("StrongTextBrush"), TextTrimming = TextTrimming.CharacterEllipsis });
                // Reuse the single-account quota layout and reset formatter, including
                // omission of absent windows. This embedded card has no native popup.
                Popup detail = UsageToolTipBuilder.Build(snapshot, language);
                var frame = (Border)detail.Child;
                var content = (Border)frame.Child;
                frame.Child = null;
                content.Effect = null;
                content.BorderThickness = new Thickness(0);
                content.Padding = new Thickness(0);
                content.MinWidth = 0;
                var rows = new StackPanel();
                rows.Children.Add(heading);
                rows.Children.Add(content);
                var card = new Border { Child = rows, Padding = new Thickness(14, 12, 14, 12), CornerRadius = new CornerRadius(9),
                    BorderThickness = new Thickness(1), BorderBrush = Brush(profile.Name == active ? "AccentHoverBrush" : "BorderBrush"),
                    Background = Brush("Surface1Brush"), Margin = new Thickness(0, 0, index % columns < columns - 1 ? 10 : 0, 10) };
                Grid.SetColumn(card, index % columns);
                Grid.SetRow(card, index / columns);
                list.Children.Add(card);
                index++;
            }
            var root = new StackPanel();
            var header = new DockPanel { Margin = new Thickness(4, 0, 0, 12) };
            var refreshButton = QuotaRefreshButtonBuilder.Build("RefreshAllQuota", language, refreshAll, Refresh, refreshState);
            DockPanel.SetDock(refreshButton, Dock.Right);
            header.Children.Add(refreshButton);
            header.Children.Add(new TextBlock { Text = Text("AllAccountsOverview") + " · " + profiles.Count,
                Foreground = Brush("StrongTextBrush"), FontSize = 15, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
            root.Children.Add(header);
            root.Children.Add(new ScrollViewer { Content = list, MaxHeight = Math.Min(480, area.Height * 0.65),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
            surface.Child = new Border { Child = root, Width = Math.Min(columns * 340 + 30, area.Width - 48),
                Padding = new Thickness(14), CornerRadius = new CornerRadius(12), Background = Brush("OverlayBackgroundBrush"),
                BorderBrush = Brush("OverlayBorderBrush"), BorderThickness = new Thickness(1),
                Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 16, ShadowDepth = 3, Opacity = 0.14 } };
        }
        Refresh();
        popup.Tag = (Action)Refresh;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        timer.Tick += (_, _) => Refresh();
        popup.Opened += (_, _) => { Refresh(); timer.Start(); };
        popup.Closed += (_, _) => timer.Stop();
        return popup;
    }
}
