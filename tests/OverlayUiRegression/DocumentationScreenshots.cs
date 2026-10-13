using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CodexProfileOverlay.Core.Models;
using CodexProfileOverlay.Core.Services;
using Button = System.Windows.Controls.Button;
using Brush = System.Windows.Media.Brush;
using Panel = System.Windows.Controls.Panel;

internal static partial class Program
{
    // Render real application controls with synthetic account names and quotas.
    // This never starts a controller, reads credentials or captures the desktop.
    private static void RenderDocumentation(string destination, LanguagePreference language)
    {
        destination = System.IO.Path.GetFullPath(destination);
        System.IO.Directory.CreateDirectory(destination);
        int rendered = 0;
        string root = System.IO.Path.Combine(AppContext.BaseDirectory, "local-test-data", "Documentation");
        System.IO.Directory.CreateDirectory(root);
        var settings = new OverlaySettings { Language = language, Theme = AppTheme.Light, DisplayMode = OverlayDisplayMode.Expanded, PositionPreset = PositionPreset.AfterMenu, OffsetX = 340, OffsetY = 0, ShowAutomaticLimitIndicators = true };
        var logger = new SafeLogger(root);
        var assembly = typeof(CodexProfileOverlay.App).Assembly;
        Type localizerType = assembly.GetType("CodexProfileOverlay.Localizer")!;
        object localizer = Activator.CreateInstance(localizerType, settings.Language)!;
        bool english = language == LanguagePreference.English;
        ProfileInfo[] profiles = [new("demo-main", root, "unused") { DisplayName = english ? "Work account" : "工作账号", Initials = english ? "W" : "工" }, new("demo-work", root, "unused") { DisplayName = english ? "Personal account" : "个人账号", Initials = english ? "P" : "个" }, new("demo-study", root, "unused") { DisplayName = english ? "Free account" : "备用账号", Initials = english ? "F" : "备" }];
        var now = DateTimeOffset.Now;
        var document = new ProfileStatusDocument();
        document.Snapshots[profiles[0].Name] = new UsageSnapshot { PlanName = "Plus", LongWindowRemainingPercent = 83,
            LongWindowResetAt = now.AddDays(6).AddHours(16), CapturedAt = now, CreditsBalance = 0, ResetCreditsRemaining = 0 };
        document.Snapshots[profiles[1].Name] = new UsageSnapshot { PlanName = "Pro", ShortWindowRemainingPercent = 26,
            ShortWindowResetAt = now.AddHours(2).AddMinutes(17), LongWindowRemainingPercent = 74,
            LongWindowResetAt = now.AddDays(6).AddHours(16), CapturedAt = now, CreditsBalance = 12.5m, ResetCreditsRemaining = 1,
            ResetCreditsExpiresAt = now.AddDays(25) };
        document.Snapshots[profiles[2].Name] = new UsageSnapshot { PlanName = "Free", CapturedAt = now, CreditsBalance = 0, ResetCreditsRemaining = 0,
            Windows = [new() { Name = "720h", Duration = TimeSpan.FromDays(30), RemainingPercent = 90, ResetAt = now.AddDays(29).AddHours(19) }] };
        var overlay = (Window)Activator.CreateInstance(OverlayType, settings, logger)!;
        try
        {
            OverlayType.GetProperty("Localizer")!.SetValue(overlay, localizer);
            Call(overlay, "SetProfiles", profiles, profiles[0].Name);
            Call(overlay, "SetStatusDocument", document, null);
            overlay.Show();
            Pump();
            SaveImage((FrameworkElement)overlay.Content, "expanded-mode.png");
            var overviewButton = Descendants((DependencyObject)overlay.Content).OfType<Button>()
                .Single(button => AutomationProperties.GetAutomationId(button) == "AccountsOverview");
            overviewButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var overview = (Popup)overviewButton.ToolTip;
            Pump();
            SaveImage((FrameworkElement)overview.Child, "accounts-overview.png");
            overview.IsOpen = false;
            Call(overlay, "MinimizeToBall"); Pump();
            SaveImage((FrameworkElement)overlay.Content, "floating-ball.png");
            Call(overlay, "RestoreFromBall"); Pump();
            var menu = Descendants((DependencyObject)overlay.Content).OfType<Button>().Single(button => button.ContextMenu is not null).ContextMenu!;
            menu.PlacementTarget = (UIElement)overlay.Content;
            menu.StaysOpen = true;
            menu.IsOpen = true;
            Pump();
            SaveImage(menu, "action-menu.png");
            menu.IsOpen = false;
            OverlayType.GetField("currentMode", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(overlay, OverlayDisplayMode.Compact);
            settings.DisplayMode = OverlayDisplayMode.Compact;
            Call(overlay, "ApplySettings");
            Pump();
            SaveImage((FrameworkElement)overlay.Content, "compact-mode.png");
            var popup = (Popup)OverlayType.GetField("compactPopup", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(overlay)!;
            popup.StaysOpen = true;
            Open(overlay);
            SaveImage((FrameworkElement)popup.Child, "profile-menu.png");
            popup.IsOpen = false;
        }
        finally { overlay.Close(); Pump(); }

        if (!english)
        {
            var actionType = assembly.GetType("CodexProfileOverlay.AccountActionDialog")!;
            var action = (Window)actionType.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).Single().Invoke(new object[]
            {
                LocalizationCatalog.Text(language, "ChooseAccountActionTitle", profiles[1].DisplayName),
                LocalizationCatalog.Text(language, "ChooseAccountActionPrompt"),
                LocalizationCatalog.Text(language, "OpenIndependentWindow"),
                LocalizationCatalog.Text(language, "SwitchCurrentAccount"),
                LocalizationCatalog.Text(language, "Cancel"), true,
            });
            try { action.Show(); Pump(); SaveImage((FrameworkElement)action.Content, "account-action.png"); }
            finally { action.Close(); Pump(); }
        }

        settings.DisplayMode = OverlayDisplayMode.Expanded;
        using var service = new ProfileStatusService(new ProfileStatusStore(System.IO.Path.Combine(root, "status.json")), new FixtureUsageProvider(), logger);
        Type windowType = assembly.GetType("CodexProfileOverlay.SettingsWindow")!;
        var args = windowType.GetConstructors().Single().GetParameters().Select(parameter =>
            parameter.ParameterType == typeof(OverlaySettings) ? (object)settings
            : parameter.ParameterType == typeof(IReadOnlyList<ProfileInfo>) ? profiles
            : parameter.ParameterType == localizerType ? localizer
            : parameter.ParameterType == typeof(ProfileStatusService) ? service
            : parameter.ParameterType == typeof(BackupMaintenanceService) ? new BackupMaintenanceService(new AppPaths(root, root))
            : parameter.ParameterType == typeof(SettingsService) ? new SettingsService(System.IO.Path.Combine(root, "settings.json"))
            : parameter.ParameterType == typeof(Action<OverlaySettings>) ? (Action<OverlaySettings>)(_ => { })
            : parameter.ParameterType == typeof(Func<string, Task>) ? (Func<string, Task>)(_ => Task.CompletedTask)
            : (Action)(() => { })).ToArray();
        var window = (Window)windowType.GetConstructors().Single().Invoke(args);
        try
        {
            window.ShowInTaskbar = false;
            window.Show();
            SetPage("Appearance");
            SaveImage((FrameworkElement)window.Content, "appearance.png", window.Background);
            SetPage("Hotkeys");
            var firstClear = Descendants((DependencyObject)window.Content).OfType<Button>().First(button => Equals(button.Content, LocalizationCatalog.Text(language, "ClearHotkey")));
            firstClear.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Pump();
            SaveImage((FrameworkElement)window.Content, "hotkeys.png", window.Background);
            var recording = Descendants((DependencyObject)window.Content).OfType<Button>().First(button => button.MinWidth == 190);
            recording.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Pump();
            SaveImage((FrameworkElement)window.Content, "hotkey-recording.png", window.Background);
            SetPage("Status");
            windowType.GetMethod("RefreshQuotaCards")!.Invoke(window, [document]);
            ((TextBlock)windowType.GetField("statusText", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!).Text = string.Empty;
            Pump();
            SaveImage((FrameworkElement)window.Content, "quota-settings.png", window.Background);
        }
        finally { window.Close(); Pump(); }
        System.Console.WriteLine($"Rendered {rendered} {language} documentation images using synthetic accounts and quotas.");

        void SetPage(string name)
        {
            var field = windowType.GetField("page", BindingFlags.Instance | BindingFlags.NonPublic)!;
            field.SetValue(window, Enum.Parse(field.FieldType, name));
            windowType.GetMethod("Rebuild", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);
            Pump();
        }
        void SaveImage(FrameworkElement element, string name, Brush? background = null)
        {
            element.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth * 1.5), (int)Math.Ceiling(element.ActualHeight * 1.5), 144, 144, PixelFormats.Pbgra32);
            if (background is not null && element is Panel panel)
            {
                // Content inherits the native window background on screen. Supply
                // that same brush when rendering its root without the native frame.
                panel.Background = background;
                element.UpdateLayout();
                Pump();
            }
            bitmap.Render(element);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var file = System.IO.File.Create(System.IO.Path.Combine(destination, name));
            encoder.Save(file);
            rendered++;
        }
    }
}
