using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using CodexProfileOverlay.Core.Models;
using CodexProfileOverlay.Core.Services;

internal static partial class Program
{
    private static void RunPlanVisibilityScenario()
    {
        foreach (var mode in new[] { OverlayDisplayMode.Expanded, OverlayDisplayMode.Compact })
        {
            string root = System.IO.Path.Combine(AppContext.BaseDirectory, "plan-visibility-fixture", Guid.NewGuid().ToString("N"));
            var paths = new AppPaths(root, System.IO.Path.Combine(root, "local"), System.IO.Path.Combine(root, "app"));
            var settings = new OverlaySettings { DisplayMode = mode, Language = LanguagePreference.ChineseSimplified, ShowAutomaticLimitIndicators = true };
            var store = new SettingsService(paths.SettingsFile);
            var logger = new SafeLogger(paths.LogDirectory);
            using var status = new ProfileStatusService(new ProfileStatusStore(paths.ProfileStatusFile), new FixtureUsageProvider(), logger);
            var profiles = Enumerable.Range(0, 3).Select(i => new ProfileInfo("visible-plan-" + i, "unused", "unused") { DisplayName = "账号 " + i }).ToArray();
            var document = Document(profiles, 0);
            var plans = new[] { "plus", "pro", "free" };
            for (int i = 0; i < 3; i++) document.Snapshots[profiles[i].Name].PlanName = plans[i];
            status.Save(document);
            var assembly = typeof(CodexProfileOverlay.App).Assembly;
            var settingsType = assembly.GetType("CodexProfileOverlay.SettingsWindow")!;
            var localizerType = assembly.GetType("CodexProfileOverlay.Localizer")!;
            var localizer = Activator.CreateInstance(localizerType, settings.Language)!;
            var overlay = (Window)Activator.CreateInstance(OverlayType, settings, logger)!;
            Window? window = null;
            try
            {
                OverlayType.GetProperty("Localizer")!.SetValue(overlay, localizer);
                OverlayType.GetField("currentMode", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(overlay, mode);
                Call(overlay, "SetProfiles", profiles, profiles[0].Name);
                Call(overlay, "SetStatusDocument", document, null);
                overlay.Show(); Pump();
                void Save(OverlaySettings candidate) { store.Save(candidate); settings.CopyFrom(candidate); Call(overlay, "ApplySettings"); }
                object[] arguments = settingsType.GetConstructors().Single().GetParameters().Select(parameter =>
                    parameter.ParameterType == typeof(OverlaySettings) ? (object)settings
                    : parameter.ParameterType == typeof(SettingsService) ? store
                    : parameter.ParameterType == typeof(IReadOnlyList<ProfileInfo>) ? profiles
                    : parameter.ParameterType == localizerType ? localizer
                    : parameter.ParameterType == typeof(ProfileStatusService) ? status
                    : parameter.ParameterType == typeof(BackupMaintenanceService) ? new BackupMaintenanceService(paths)
                    : parameter.ParameterType == typeof(Action<OverlaySettings>) ? (Action<OverlaySettings>)Save
                    : parameter.ParameterType == typeof(Func<string, Task>) ? (Func<string, Task>)(_ => Task.CompletedTask)
                    : (Action)(() => { })).ToArray();
                window = (Window)settingsType.GetConstructors().Single().Invoke(arguments);
                var appearance = (DependencyObject)settingsType.GetMethod("BuildAppearancePage", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null)!;
                var check = Descendants(appearance).OfType<System.Windows.Controls.CheckBox>().Single(item =>
                    item.Parent is DependencyObject parent && Descendants(parent).OfType<TextBlock>().Any(text => text.Text == "显示账号套餐"));
                Require(check.IsChecked == true, "Plan visibility must default on in the actual settings control.");
                foreach (bool visible in new[] { true, false, true })
                {
                    check.IsChecked = visible; Pump();
                    int Expected(int count) => visible ? count : 0;
                    int Badges(DependencyObject host) => Descendants(host).OfType<Border>().Count(border => AutomationProperties.GetAutomationId(border) == "AccountPlanBadge");
                    var shell = (Border)overlay.Content;
                    Require(Badges(shell) == Expected(mode == OverlayDisplayMode.Expanded ? 3 : 1), "Plan toggle must affect every visible account badge.");
                    var overviewButton = Descendants(shell).OfType<System.Windows.Controls.Button>().Single(button => AutomationProperties.GetAutomationId(button) == "AccountsOverview");
                    var overview = (Popup)overviewButton.ToolTip;
                    overview.IsOpen = true; Pump();
                    Require(Badges(overview.Child) == Expected(3), "Plan toggle must also affect the overview.");
                    overview.IsOpen = false; Pump();
                    var popup = (Popup)OverlayType.GetField("compactPopup", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(overlay)!;
                    popup.IsOpen = true; Pump();
                    Require(Badges(popup.Child) == Expected(3), "Plan toggle must affect the compact account list.");
                    popup.IsOpen = false; Pump();
                    Require(Descendants(shell).OfType<TextBlock>().Any(text => text.Text.Contains('%')), "Hiding plans must retain quota summaries.");
                    Require(document.Snapshots.Values.Select(snapshot => snapshot.PlanName).SequenceEqual(plans), "Visibility must not alter reported account plans.");
                    if (!visible) Require(!store.Load().ShowAccountPlanBadges, "The actual toggle callback must persist the hidden state.");
                }
                Evidence.Add(new { scenario = "account-plan-visibility-toggle", mode });
            }
            finally { window?.Close(); overlay.Close(); Pump(); }
        }
    }
}
