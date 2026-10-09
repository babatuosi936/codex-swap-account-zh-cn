using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Media;
using CodexProfileOverlay.Core.Models;
using CodexProfileOverlay.Core.Services;

namespace CodexProfileOverlay;

internal static class QuotaRefreshButtonBuilder
{
    internal sealed class State { internal bool Busy; internal bool Failed; }
    internal static Button Build(string labelKey, LanguagePreference language, Func<Task>? refresh, Action updated, State state)
    {
        string Text(string key) => LocalizationCatalog.Text(language, key);
        var button = new Button { Content = Text(state.Busy ? "QuotaRefreshing" : labelKey), HorizontalAlignment = HorizontalAlignment.Right,
            Padding = new Thickness(10, 4, 10, 4), FontSize = 12, IsEnabled = refresh is not null && !state.Busy,
            ToolTip = state.Failed ? Text("QuotaRefreshFailed") : null,
            Background = (Brush)Application.Current.FindResource("TabBackgroundBrush"),
            Foreground = (Brush)Application.Current.FindResource("StrongTextBrush"), BorderThickness = new Thickness(0) };
        AutomationProperties.SetAutomationId(button, labelKey);
        button.Click += async (_, _) =>
        {
            if (refresh is null || state.Busy || !button.IsEnabled) return;
            state.Busy = true;
            state.Failed = false;
            button.IsEnabled = false;
            button.Content = Text("QuotaRefreshing");
            try { await refresh(); }
            catch (OperationCanceledException) { }
            catch (Exception) { state.Failed = true; }
            finally { state.Busy = false; button.Content = Text(labelKey); button.IsEnabled = true; updated(); }
        };
        return button;
    }
}
