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

        var details = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        string creditValue = snapshot?.CreditsUnlimited == true ? Text("UnlimitedCredits")
            : snapshot?.CreditsBalance?.ToString("0.################", CultureInfo.InvariantCulture)
                ?? snapshot?.CreditsRemaining?.ToString(CultureInfo.InvariantCulture) ?? Text("UnknownValue");
        AddDetail(Text("RemainingQuota"), creditValue);
        string resetValue = snapshot?.ResetCreditsRemaining?.ToString(CultureInfo.InvariantCulture) ?? Text("UnknownValue");
        if (snapshot?.ResetCreditsRemaining > 0 && snapshot.ResetCreditsExpiresAt is DateTimeOffset expiry)
            resetValue += " (" + Text("NextExpiry") + ": " + UsageDisplayFormatter.FormatLocal(expiry) + ")";
        AddDetail(Text("ResetCredits"), resetValue);
        rows.Children.Add(details);

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

        void AddDetail(string label, string value)
        {
            var line = new Grid { Margin = new Thickness(0, details.Children.Count == 0 ? 0 : 5, 0, 0) };
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            line.Children.Add(new TextBlock { Text = label, FontSize = 12, Foreground = Brush("MutedTextBrush"), Margin = new Thickness(0, 0, 16, 0) });
            var data = new TextBlock { Text = value, FontSize = 12, Foreground = Brush("MutedTextBrush"), TextAlignment = TextAlignment.Right, TextWrapping = TextWrapping.Wrap };
            Grid.SetColumn(data, 1); line.Children.Add(data); details.Children.Add(line);
        }

        void AddQuotaRow(string label, UsageLimitWindow? window)
        {
            if (window is null) return;
            var section = new StackPanel { Margin = new Thickness(0, rows.Children.Count == 0 ? 0 : 12, 0, 0) };
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.Children.Add(new TextBlock { Text = label, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = Brush("StrongTextBrush"), MinWidth = 48, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center });
            int percent = Math.Clamp(window.RemainingPercent ?? 0, 0, 100);
            var surface = (SolidColorBrush)Brush("Surface1Brush");
            bool dark = surface.Color.R + surface.Color.G + surface.Color.B < 384;
            var color = window.RemainingPercent is null ? Brush("MutedTextBrush")
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString(percent >= 70 ? dark ? "#34C759" : "#15803D" : percent >= 30 ? dark ? "#FF9500" : "#C76C00" : "#EF4444"));
            var percentage = new TextBlock { Text = window.RemainingPercent is int ? percent + "%" : "—", FontSize = 14,
                FontWeight = FontWeights.SemiBold, Foreground = color, HorizontalAlignment = HorizontalAlignment.Right };
            Grid.SetColumn(percentage, 1);
            row.Children.Add(percentage);
            section.Children.Add(row);
            var progress = new Grid { Height = 4 };
            progress.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(percent, GridUnitType.Star) });
            progress.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100 - percent, GridUnitType.Star) });
            progress.Children.Add(new Border { Background = color, CornerRadius = new CornerRadius(2) });
            section.Children.Add(new Border { Child = progress, Height = 4, Margin = new Thickness(0, 7, 0, 7), Background = Brush("TabBackgroundBrush"), CornerRadius = new CornerRadius(2), Tag = "QuotaProgress" });
            var reset = new TextBlock
            {
                Text = FormatReset(window.ResetAt, DateTimeOffset.UtcNow, language),
                FontSize = 12,
                Foreground = Brush("MutedTextBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                TextAlignment = TextAlignment.Right,
            };
            section.Children.Add(reset);
            resetLabels.Add((reset, window.ResetAt));
            rows.Children.Add(section);
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
