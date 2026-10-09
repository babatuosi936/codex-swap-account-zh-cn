using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using System.Globalization;
using CodexProfileOverlay.Core.Models;
using CodexProfileOverlay.Core.Services;

namespace CodexProfileOverlay;

internal static class UsageToolTipBuilder
{
    public static Popup Build(UsageSnapshot? snapshot, LanguagePreference language)
    {
        string Text(string key) => LocalizationCatalog.Text(language, key);
        Brush Brush(string key) => (Brush)Application.Current.FindResource(key);
        var rows = new StackPanel();
        var resetLabels = new List<(TextBlock Label, DateTimeOffset? ResetAt)>();
        var windows = snapshot is null ? [] : UsageIntelligence.GetKnownWindows(snapshot);
        AddQuotaRow(Text("FiveHourWindow"), windows.FirstOrDefault(window => window.Duration == TimeSpan.FromHours(5) || window.Name is "5h" or "short"));
        AddQuotaRow(Text("WeeklyWindow"), windows.FirstOrDefault(window => window.Duration == TimeSpan.FromDays(7) || window.Name is "Weekly" or "weekly" or "7d" or "long"));
        if (rows.Children.Count == 0)
        {
            rows.Children.Add(new TextBlock { Text = Text("QuotaUnknown"), FontSize = 13, Foreground = Brush("MutedTextBrush") });
        }

        var updated = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        updated.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        updated.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        updated.Children.Add(new TextBlock { Text = Text("LastUpdated"), FontSize = 12, Foreground = Brush("MutedTextBrush"), Margin = new Thickness(0, 0, 16, 0) });
        var timestamp = new TextBlock
        {
            Text = snapshot is null ? "—" : UsageDisplayFormatter.FormatLocal(snapshot.CapturedAt),
            FontSize = 12,
            Foreground = Brush("MutedTextBrush"),
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        Grid.SetColumn(timestamp, 1);
        updated.Children.Add(timestamp);
        rows.Children.Add(new Border { Child = updated, BorderBrush = Brush("BorderBrush"), BorderThickness = new Thickness(0, 1, 0, 0), Margin = new Thickness(0, 8, 0, 0) });

        var card = new Border
        {
            Child = rows,
            Background = Brush("Surface1Brush"),
            BorderBrush = Brush("BorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16, 12, 16, 12),
            MinWidth = 350,
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 16, ShadowDepth = 3, Opacity = 0.14 },
        };
        var popup = new Popup
        {
            Child = new Border { Child = card, Padding = new Thickness(8), Background = Brushes.Transparent },
            AllowsTransparency = true,
            StaysOpen = true,
            Placement = PlacementMode.Custom,
            CustomPopupPlacementCallback = (popupSize, targetSize, offset) =>
            [
                new CustomPopupPlacement(
                    new Point((targetSize.Width - popupSize.Width) / 2 + offset.X, targetSize.Height + offset.Y),
                    PopupPrimaryAxis.Horizontal),
                new CustomPopupPlacement(
                    new Point((targetSize.Width - popupSize.Width) / 2 + offset.X, -popupSize.Height - offset.Y),
                    PopupPrimaryAxis.Horizontal),
            ],
            VerticalOffset = 4,
        };
        var countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        void UpdateCountdowns()
        {
            foreach (var entry in resetLabels)
                entry.Label.Text = FormatReset(entry.ResetAt, DateTimeOffset.UtcNow, language);
        }
        countdownTimer.Tick += (_, _) => UpdateCountdowns();
        popup.Opened += (_, _) => { UpdateCountdowns(); countdownTimer.Start(); };
        popup.Closed += (_, _) => countdownTimer.Stop();
        return popup;

        void AddQuotaRow(string label, UsageLimitWindow? window)
        {
            if (window is null) return;
            var row = new Grid { Margin = new Thickness(0, rows.Children.Count == 0 ? 0 : 8, 0, 0) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.Children.Add(new TextBlock { Text = label, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = Brush("StrongTextBrush"), MinWidth = 48, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center });
            var percentage = new Border
            {
                Background = Brush("TabBackgroundBrush"),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 3, 8, 3),
                MinWidth = 48,
                Margin = new Thickness(0, 0, 18, 0),
                Child = new TextBlock { Text = window.RemainingPercent is int value ? value + "%" : "—", FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = Brush("StrongTextBrush"), HorizontalAlignment = HorizontalAlignment.Center },
            };
            Grid.SetColumn(percentage, 1);
            row.Children.Add(percentage);
            var reset = new TextBlock
            {
                Text = FormatReset(window.ResetAt, DateTimeOffset.UtcNow, language),
                FontSize = 12,
                Foreground = Brush("MutedTextBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                TextAlignment = TextAlignment.Right,
            };
            Grid.SetColumn(reset, 2);
            row.Children.Add(reset);
            resetLabels.Add((reset, window.ResetAt));
            rows.Children.Add(row);
        }
    }

    internal static string FormatReset(DateTimeOffset? resetAt, DateTimeOffset now, LanguagePreference language)
    {
        if (resetAt is not DateTimeOffset at) return "—";
        if (language == LanguagePreference.SystemDefault)
        {
            language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName switch
            {
                "zh" => LanguagePreference.ChineseSimplified,
                "ru" => LanguagePreference.Russian,
                _ => LanguagePreference.English,
            };
        }
        string date = at.ToLocalTime().ToString("MM/dd HH:mm", CultureInfo.InvariantCulture);
        if (at <= now)
        {
            string due = language switch
            {
                LanguagePreference.ChineseSimplified => "已到重置时间",
                LanguagePreference.Russian => "Время сброса наступило",
                _ => "Reset time reached",
            };
            return $"{due} ({date})";
        }
        var remaining = TimeSpan.FromMinutes(Math.Ceiling((at - now).TotalMinutes));
        var parts = new List<string>();
        string Unit(int value, string chinese, string english, string russian) => language switch
        {
            LanguagePreference.ChineseSimplified => value + chinese,
            LanguagePreference.Russian => value + " " + russian,
            _ => value + english,
        };
        if (remaining.Days > 0) parts.Add(Unit(remaining.Days, "天", "d", "д"));
        if (remaining.Hours > 0) parts.Add(Unit(remaining.Hours, "小时", "h", "ч"));
        if (remaining.Minutes > 0) parts.Add(Unit(remaining.Minutes, "分钟", "m", "мин"));
        return $"{string.Join(" ", parts)} ({date})";
    }
}
