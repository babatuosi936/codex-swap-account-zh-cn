using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using CodexProfileOverlay.Core.Models;
using CodexProfileOverlay.Core.Services;

namespace CodexProfileOverlay;

internal static class AccountPlanBadgeBuilder
{
    public static Border Build(string? planName, LanguagePreference language, double fontSize = 9)
    {
        string? plan = AccountPlanFormatter.Normalize(planName);
        string label = plan ?? LocalizationCatalog.Text(language, "AccountPlanUnknown");
        var badge = new Border
        {
            Child = new TextBlock
            {
                Text = label,
                FontSize = fontSize,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.FindResource(plan is null ? "MutedTextBrush" : "AccentBrush"),
                VerticalAlignment = VerticalAlignment.Center,
            },
            Background = (Brush)Application.Current.FindResource("Surface2Brush"),
            BorderBrush = (Brush)Application.Current.FindResource("BorderBrush"),
            BorderThickness = new Thickness(0.5),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(4, 1, 4, 1),
            VerticalAlignment = VerticalAlignment.Center,
            Tag = plan,
        };
        AutomationProperties.SetAutomationId(badge, "AccountPlanBadge");
        AutomationProperties.SetName(badge, label);
        return badge;
    }

    // A finite name column yields to the badge; the natural width keeps the badge
    // beside short names instead of sending it to the opposite edge of the card.
    internal static Grid BesideName(TextBlock name, string? planName, LanguagePreference language, double fontSize = 9)
    {
        var row = new Grid { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(name);
        var badge = Build(planName, language, fontSize);
        badge.Margin = new Thickness(6, 0, 0, 0);
        Grid.SetColumn(badge, 1);
        row.Children.Add(badge);
        return row;
    }
}
