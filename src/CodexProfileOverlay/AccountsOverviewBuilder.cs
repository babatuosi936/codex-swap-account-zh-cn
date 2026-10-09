using System;
using System.Collections.Generic;
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
    public static Popup Build(IReadOnlyList<ProfileInfo> profiles, string? active, Func<string, UsageSnapshot?> snapshotFor, LanguagePreference language)
    {
        Brush Brush(string key) => (Brush)Application.Current.FindResource(key);
        string Text(string key) => LocalizationCatalog.Text(language, key);
        var surface = new Border { Padding = new Thickness(8), Background = Brushes.Transparent };
        var popup = new Popup
        {
            Child = surface, AllowsTransparency = true, StaysOpen = true, Placement = PlacementMode.Custom,
            VerticalOffset = 4,
            CustomPopupPlacementCallback = (size, target, offset) =>
            [new(new Point(offset.X, target.Height + offset.Y), PopupPrimaryAxis.Horizontal),
             new(new Point(offset.X, -size.Height - offset.Y), PopupPrimaryAxis.Horizontal)],
        };
        void Refresh()
        {
            var list = new StackPanel();
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
                list.Children.Add(new Border { Child = rows, Padding = new Thickness(14, 12, 14, 12), CornerRadius = new CornerRadius(9),
                    BorderThickness = new Thickness(1), BorderBrush = Brush(profile.Name == active ? "AccentHoverBrush" : "BorderBrush"),
                    Background = Brush("Surface1Brush"), Margin = new Thickness(0, 0, 0, 8) });
            }
            var root = new StackPanel();
            root.Children.Add(new TextBlock { Text = Text("AllAccountsOverview") + " · " + profiles.Count,
                Foreground = Brush("StrongTextBrush"), FontSize = 15, FontWeight = FontWeights.SemiBold, Margin = new Thickness(4, 0, 0, 12) });
            root.Children.Add(new ScrollViewer { Content = list, MaxHeight = Math.Min(480, SystemParameters.WorkArea.Height * 0.65),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
            surface.Child = new Border { Child = root, Width = Math.Min(510, SystemParameters.WorkArea.Width - 32),
                Padding = new Thickness(14), CornerRadius = new CornerRadius(12), Background = Brush("OverlayBackgroundBrush"),
                BorderBrush = Brush("OverlayBorderBrush"), BorderThickness = new Thickness(1),
                Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 16, ShadowDepth = 3, Opacity = 0.14 } };
        }
        Refresh();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        timer.Tick += (_, _) => Refresh();
        popup.Opened += (_, _) => { Refresh(); timer.Start(); };
        popup.Closed += (_, _) => timer.Stop();
        return popup;
    }
}
