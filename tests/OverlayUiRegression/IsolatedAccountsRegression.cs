using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using CodexProfileOverlay.Core.Models;
using CodexProfileOverlay.Core.Services;

internal static partial class Program
{
    private static void RunIsolatedAccountsScenario()
    {
        string fixture = Path.Combine(AppContext.BaseDirectory, "isolated-accounts-ui-fixture");
        Directory.CreateDirectory(fixture);
        var settings = new OverlaySettings { Language = LanguagePreference.ChineseSimplified, Theme = AppTheme.Light };
        var profiles = new[] { new ProfileInfo("fixture", fixture, Path.Combine(fixture, "unused-auth.json")) { DisplayName = "测试账号" } };
        var assembly = typeof(CodexProfileOverlay.App).Assembly;
        Type localizerType = assembly.GetType("CodexProfileOverlay.Localizer")!;
        object localizer = Activator.CreateInstance(localizerType, settings.Language)!;
        using var status = new ProfileStatusService(new ProfileStatusStore(Path.Combine(fixture, "status.json")),
            new FixtureUsageProvider(), new SafeLogger(Path.Combine(fixture, "fixture-logs")));
        int saves = 0, opens = 0, removes = 0;
        object[] Arguments(Type type) => type.GetConstructors().Single().GetParameters().Select(parameter =>
            parameter.ParameterType == typeof(OverlaySettings) ? (object)settings
            : parameter.ParameterType == typeof(IReadOnlyList<ProfileInfo>) ? profiles
            : parameter.ParameterType == typeof(string) ? null!
            : parameter.ParameterType == localizerType ? localizer
            : parameter.ParameterType == typeof(ProfileStatusService) ? status
            : parameter.ParameterType == typeof(BackupMaintenanceService) ? new BackupMaintenanceService(new AppPaths(fixture, fixture))
            : parameter.ParameterType == typeof(SettingsService) ? new SettingsService(Path.Combine(fixture, "settings.json"))
            : parameter.ParameterType == typeof(Action<OverlaySettings>) ? (Action<OverlaySettings>)(_ => saves++)
            : parameter.ParameterType == typeof(Func<string, Task>) ? (Func<string, Task>)(_ => Task.CompletedTask)
            : parameter.ParameterType == typeof(Action<IReadOnlyList<string>>) ? (Action<IReadOnlyList<string>>)(_ => { })
            : parameter.ParameterType == typeof(Action<ProfileInfo>) ? (Action<ProfileInfo>)(profile =>
                { if (parameter.Name == "openAccountWindow") { Require(profile.Name == "fixture", "Wrong account selected."); opens++; }
                  if (parameter.Name == "removeProfile") removes++; })
            : (Action)(() => { })).ToArray();
        Type settingsType = assembly.GetType("CodexProfileOverlay.SettingsWindow")!;
        var window = (Window)settingsType.GetConstructors().Single().Invoke(Arguments(settingsType));
        try
        {
            window.ShowInTaskbar = false;
            window.Show();
            var page = settingsType.GetField("page", BindingFlags.NonPublic | BindingFlags.Instance)!;
            page.SetValue(window, Enum.Parse(page.FieldType, "General"));
            settingsType.GetMethod("Rebuild", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, null);
            Pump();
            var checks = Descendants((System.Windows.DependencyObject)window.Content).OfType<CheckBox>().ToArray();
            Require(checks.Length >= 5 && checks[0].IsChecked == true, "Independent windows must be the default visible setting.");
            checks[0].IsChecked = false; Pump();
            Require(!settings.UseIsolatedAccountWindows && saves > 0, "Explicit legacy mode must save.");
            checks[0].IsChecked = true; Pump();
            Require(settings.UseIsolatedAccountWindows, "Independent mode must be restorable.");
            Evidence.Add(new { stage = "independent-window-settings", settingVisible = true, saved = saves });
        }
        finally { window.Close(); Pump(); }

        Type actionDialogType = assembly.GetType("CodexProfileOverlay.AccountActionDialog")!;
        var actionDialog = (Window)actionDialogType.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).Single().Invoke(new object[]
        {
            "使用账号“测试账号”",
            "打开独立窗口会保留当前 Codex 窗口。",
            "打开新的独立窗口",
            "切换当前账户",
            "取消",
            true,
        });
        try
        {
            actionDialog.ShowInTaskbar = false;
            actionDialog.Show(); Pump();
            Button[] buttons = Descendants((System.Windows.DependencyObject)actionDialog.Content).OfType<Button>().ToArray();
            Button open = buttons.Single(item => Equals(item.Content, "打开新的独立窗口"));
            Button switchCurrent = buttons.Single(item => Equals(item.Content, "切换当前账户"));
            Require(open.IsDefault && !switchCurrent.IsDefault, "Separate-window action must be the default account-click choice.");
            Evidence.Add(new { stage = "account-action-choice", independentWindowDefault = open.IsDefault });
        }
        finally { actionDialog.Close(); Pump(); }

        Type managerType = assembly.GetType("CodexProfileOverlay.ProfileManagerWindow")!;
        var manager = (Window)managerType.GetConstructors().Single().Invoke(Arguments(managerType));
        try
        {
            manager.ShowInTaskbar = false;
            manager.Show(); Pump();
            Button button = Descendants((System.Windows.DependencyObject)manager.Content).OfType<Button>()
                .First(item => item.ContextMenu is not null);
            MenuItem open = button.ContextMenu!.Items.OfType<MenuItem>().First();
            Require(Equals(open.Header, "打开账号窗口"), "Account window action must be the first menu item.");
            open.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Pump();
            Require(opens == 1 && removes == 0, "Opening must select the account without invoking removal.");
            Evidence.Add(new { stage = "account-window-action", correctAccount = true, opens, removes });
        }
        finally { manager.Close(); Pump(); }
    }
}
