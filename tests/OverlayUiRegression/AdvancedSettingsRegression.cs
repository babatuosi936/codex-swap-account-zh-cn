using System.Reflection;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CodexProfileOverlay.Core.Models;
using CodexProfileOverlay.Core.Services;
using Button = System.Windows.Controls.Button;
using ComboBox = System.Windows.Controls.ComboBox;
using TextBox = System.Windows.Controls.TextBox;

internal static partial class Program
{
    // This fixture deliberately uses an alternate application-data directory.
    // Its save callback models the controller without starting window discovery,
    // registering shortcuts, editing startup, querying quotas, or using accounts.
    private static void RunAdvancedSettingsScenario()
    {
        string fixture = System.IO.Path.Combine(AppContext.BaseDirectory, "advanced-settings-fixture", Guid.NewGuid().ToString("N"));
        var paths = new AppPaths(System.IO.Path.Combine(fixture, "user"), System.IO.Path.Combine(fixture, "local"),
            System.IO.Path.Combine(fixture, "alternate-application-data"));
        var store = new SettingsService(paths.SettingsFile);
        var settings = new OverlaySettings { Language = LanguagePreference.ChineseSimplified, Theme = AppTheme.Light, Scale = 0.9 };
        store.Save(settings);
        var assembly = typeof(CodexProfileOverlay.App).Assembly;
        Type settingsType = assembly.GetType("CodexProfileOverlay.SettingsWindow")!;
        Type localizerType = assembly.GetType("CodexProfileOverlay.Localizer")!;
        object localizer = Activator.CreateInstance(localizerType, settings.Language)!;
        var logger = new SafeLogger(paths.LogDirectory);
        using var status = new ProfileStatusService(new ProfileStatusStore(paths.ProfileStatusFile), new FixtureUsageProvider(), logger);
        status.Save(new ProfileStatusDocument());
        var profiles = new[]
        {
            new ProfileInfo("advanced-one", System.IO.Path.Combine(paths.ProfilesDirectory, "advanced-one"), "unused-auth-one"),
            new ProfileInfo("advanced-two", System.IO.Path.Combine(paths.ProfilesDirectory, "advanced-two"), "unused-auth-two"),
        };
        System.IO.Directory.CreateDirectory(paths.SharedCodexDirectory);
        System.IO.File.WriteAllText(paths.SharedAuthFile, "synthetic authentication sentinel");
        System.IO.File.WriteAllText(paths.ProfilesMetadataFile, "{\"syntheticMetadata\":true}");
        string normalStore = System.IO.Path.Combine(paths.LocalAppData, "CodexProfileOverlay", "settings.json");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(normalStore)!);
        System.IO.File.WriteAllText(normalStore, "alternate-store routing sentinel");
        string[] protectedFiles = [paths.SharedAuthFile, paths.ProfilesMetadataFile, paths.ProfileStatusFile, normalStore];
        Dictionary<string, byte[]> originalHashes = protectedFiles.ToDictionary(path => path,
            path => SHA256.HashData(System.IO.File.ReadAllBytes(path)));

        var overlay = (Window)Activator.CreateInstance(OverlayType, settings, logger)!;
        OverlayType.GetProperty("Localizer")!.SetValue(overlay, localizer);
        Window? window = null;
        int saves = 0, replacements = 0;
        bool failApply = false;
        void Apply(OverlaySettings candidate)
        {
            if (failApply) throw new System.IO.IOException("Synthetic persistence failure; private details must stay hidden.");
            bool replacing = !ReferenceEquals(candidate, settings);
            store.Save(candidate);
            settings.CopyFrom(candidate);
            saves++;
            if (replacing) replacements++;
            CodexProfileOverlay.App.ApplyTheme(settings.Theme);
            localizerType.GetMethod("SetLanguage")!.Invoke(localizer, [settings.Language]);
            Call(overlay, "ApplySettings");
            if (window is not null) settingsType.GetMethod(replacing ? "RefreshSettings" : "RefreshTheme")!.Invoke(window, null);
        }
        Window CreateSettingsWindow()
        {
            object[] arguments = settingsType.GetConstructors().Single().GetParameters().Select(parameter =>
                parameter.ParameterType == typeof(OverlaySettings) ? (object)settings
                : parameter.ParameterType == typeof(SettingsService) ? store
                : parameter.ParameterType == typeof(IReadOnlyList<ProfileInfo>) ? profiles
                : parameter.ParameterType == localizerType ? localizer
                : parameter.ParameterType == typeof(ProfileStatusService) ? status
                : parameter.ParameterType == typeof(BackupMaintenanceService) ? new BackupMaintenanceService(paths)
                : parameter.ParameterType == typeof(Action<OverlaySettings>) ? (Action<OverlaySettings>)Apply
                : parameter.ParameterType == typeof(Func<string, Task>) ? (Func<string, Task>)(_ => Task.CompletedTask)
                : parameter.Name == "resetSettings" ? (Action)(() => Apply(new OverlaySettings()))
                : parameter.Name == "resetPosition" ? (Action)(() => { settings.ResetPosition(); Apply(settings); })
                : (Action)(() => { })).ToArray();
            var result = (Window)settingsType.GetConstructors().Single().Invoke(arguments);
            result.ShowInTaskbar = false;
            result.Show();
            Pump();
            return result;
        }
        void Page(string name)
        {
            FieldInfo page = settingsType.GetField("page", BindingFlags.Instance | BindingFlags.NonPublic)!;
            page.SetValue(window, Enum.Parse(page.FieldType, name));
            settingsType.GetMethod("Rebuild", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);
            Pump();
        }
        bool InvokeFileAction(string method, string file)
        {
            bool succeeded = (bool)settingsType.GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [file])!;
            Pump();
            return succeeded;
        }
        bool DraftDirty() => (bool)settingsType.GetField("hotkeysDirty", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
        HotkeySettings? Draft() => (HotkeySettings?)settingsType.GetField("hotkeyDraft", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window);
        string StatusText() => ((TextBlock)settingsType.GetField("statusText", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!).Text;
        void Check(string stage, bool condition)
        {
            Require(condition, "Advanced settings regression: " + stage);
            Evidence.Add(new { kind = "advanced-settings", stage, saves, replacements });
        }
        bool SameHotkeys(HotkeySettings first, HotkeySettings second) => Equals(first.ToggleOverlay, second.ToggleOverlay)
            && first.ProfileHotkeys.SequenceEqual(second.ProfileHotkeys);
        void CheckSharedReferences(string stage)
        {
            Check(stage, ReferenceEquals(settingsType.GetField("settings", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window), settings)
                && ReferenceEquals(OverlayType.GetField("settings", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(overlay), settings));
        }
        try
        {
            overlay.ShowInTaskbar = false;
            overlay.Show();
            window = CreateSettingsWindow();
            Page("Hotkeys");
            Button clear = Descendants((DependencyObject)window.Content).OfType<Button>().First(button => Equals(button.Content, "清除"));
            clear.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Pump();
            Check("shortcut-edit-is-an-unsaved-draft", DraftDirty() && Draft()!.ToggleOverlay is null && settings.Hotkeys.ToggleOverlay is not null);
            settingsType.GetMethod("Save", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);
            Pump();
            Check("ordinary-save-preserves-shortcut-draft", DraftDirty() && Draft()!.ToggleOverlay is null
                && store.Load().Hotkeys.ToggleOverlay is not null);

            var imported = new OverlaySettings
            {
                Language = LanguagePreference.ChineseSimplified,
                Theme = AppTheme.Light,
                Scale = 1.2,
                PositionPreset = PositionPreset.Custom,
                OffsetX = 333,
                OffsetY = 27,
                AnimationsEnabled = false,
                UseIsolatedAccountWindows = false,
                StaleDataThresholdMinutes = 45,
                SettingsWindowWidth = 1180,
                SettingsWindowHeight = 780,
                Hotkeys = new HotkeySettings
                {
                    ToggleOverlay = new HotkeyGesture(HotkeyModifiers.Control | HotkeyModifiers.Shift, 'K'),
                    ProfileHotkeys = [new HotkeyGesture(HotkeyModifiers.Alt, '1'), new HotkeyGesture(HotkeyModifiers.Alt, '2')],
                },
            };
            string importFile = System.IO.Path.Combine(fixture, "selected-import.json");
            new SettingsService(importFile).Save(imported);
            bool importedSuccessfully = InvokeFileAction("ImportSettingsFromFile", importFile);
            Check("import-is-applied-live-instead-of-closing", importedSuccessfully && window.IsVisible && replacements == 1 && settings.Scale == 1.2
                && settings.OffsetX == 333 && !settings.UseIsolatedAccountWindows && settings.StaleDataThresholdMinutes == 45);
            CheckSharedReferences("import-retains-window-settings-references");
            Check("replacement-clears-old-shortcut-draft", !DraftDirty() && Draft() is { } importedDraft
                && Equals(importedDraft.ToggleOverlay, settings.Hotkeys.ToggleOverlay)
                && importedDraft.ProfileHotkeys.Take(settings.Hotkeys.ProfileHotkeys.Count).SequenceEqual(settings.Hotkeys.ProfileHotkeys)
                && importedDraft.ProfileHotkeys.Skip(settings.Hotkeys.ProfileHotkeys.Count).All(gesture => gesture is null));
            var shell = (Border)OverlayType.GetField("shell", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(overlay)!;
            Check("import-updates-the-live-floating-window", shell.LayoutTransform is ScaleTransform { ScaleX: 1.2, ScaleY: 1.2 });
            Check("import-applies-settings-window-size", window.Width == 1180 && window.Height == 780);
            window.Close();
            window = null;
            Pump();
            OverlaySettings afterClose = store.Load();
            Check("closing-cannot-overwrite-the-import", afterClose.Scale == 1.2 && afterClose.OffsetX == 333
                && !afterClose.UseIsolatedAccountWindows && SameHotkeys(afterClose.Hotkeys, imported.Hotkeys));
            window = CreateSettingsWindow();
            CheckSharedReferences("reopening-keeps-the-applied-settings");

            string exportFile = System.IO.Path.Combine(fixture, "selected-export.json");
            bool exportedSuccessfully = InvokeFileAction("ExportSettingsToFile", exportFile);
            OverlaySettings exported = new SettingsService(exportFile).Load();
            Check("export-uses-current-settings-and-selected-destination", exportedSuccessfully && System.IO.File.Exists(exportFile) && exported.Scale == 1.2
                && exported.OffsetX == 333 && !exported.UseIsolatedAccountWindows && SameHotkeys(exported.Hotkeys, settings.Hotkeys));

            byte[] appliedFile = System.IO.File.ReadAllBytes(paths.SettingsFile);
            int appliedSaves = saves;
            string invalidFile = System.IO.Path.Combine(fixture, "private-invalid-import.json");
            System.IO.File.WriteAllText(invalidFile, "{ not valid json");
            bool invalidAccepted = InvokeFileAction("ImportSettingsFromFile", invalidFile);
            Check("invalid-import-preserves-live-and-persisted-settings", !invalidAccepted && saves == appliedSaves && settings.Scale == 1.2
                && System.IO.File.ReadAllBytes(paths.SettingsFile).SequenceEqual(appliedFile));
            Check("invalid-import-shows-a-safe-failure", StatusText() == LocalizationCatalog.Text(settings.Language, "CouldNotImportSettings")
                && !StatusText().Contains(invalidFile, StringComparison.OrdinalIgnoreCase));

            string oversizedFile = System.IO.Path.Combine(fixture, "oversized-import.json");
            System.IO.File.WriteAllText(oversizedFile, "{\"padding\":\"" + new string('x', 1024 * 1024) + "\"}");
            bool oversizedAccepted = InvokeFileAction("ImportSettingsFromFile", oversizedFile);
            Check("oversized-import-cannot-change-settings", !oversizedAccepted && saves == appliedSaves
                && System.IO.File.ReadAllBytes(paths.SettingsFile).SequenceEqual(appliedFile));
            failApply = true;
            bool failedSaveAccepted = InvokeFileAction("ImportSettingsFromFile", importFile);
            failApply = false;
            Check("failed-save-cannot-partially-apply-an-import", !failedSaveAccepted && saves == appliedSaves && settings.Scale == 1.2
                && System.IO.File.ReadAllBytes(paths.SettingsFile).SequenceEqual(appliedFile)
                && StatusText() == LocalizationCatalog.Text(settings.Language, "CouldNotImportSettings")
                && !StatusText().Contains("private details", StringComparison.OrdinalIgnoreCase));

            Page("Advanced");
            string resetPositionLabel = LocalizationCatalog.Text(settings.Language, "ResetPosition");
            Button resetPosition = Descendants((DependencyObject)window.Content).OfType<Button>().Single(button =>
                Descendants(button).OfType<TextBlock>().Any(text => text.Text == resetPositionLabel));
            resetPosition.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Pump();
            OverlaySettings afterPositionReset = store.Load();
            Check("position-reset-restores-menu-preset-live-and-persisted", settings.PositionPreset == PositionPreset.AfterMenu
                && settings.OffsetX == 340 && settings.OffsetY == 0 && afterPositionReset.PositionPreset == PositionPreset.AfterMenu
                && afterPositionReset.OffsetX == 340 && afterPositionReset.OffsetY == 0);
            Check("position-reset-preserves-scale-and-animation-choice", settings.Scale == 1.2 && !settings.AnimationsEnabled
                && afterPositionReset.Scale == 1.2 && !afterPositionReset.AnimationsEnabled);
            Page("Appearance");
            ComboBox positionCombo = Descendants((DependencyObject)window.Content).OfType<ComboBox>()
                .Single(combo => combo.SelectedValue is PositionPreset);
            string? selectedPositionLabel = positionCombo.SelectedItem?.GetType().GetProperty("Label")?.GetValue(positionCombo.SelectedItem) as string;
            Check("position-reset-shows-menu-right-in-appearance", Equals(positionCombo.SelectedValue, PositionPreset.AfterMenu)
                && selectedPositionLabel == "菜单右侧");
            Check("menu-position-hides-custom-offset-editors", Descendants((DependencyObject)window.Content).OfType<TextBox>().Count() == 1
                && !Descendants((DependencyObject)window.Content).OfType<TextBlock>().Any(text =>
                    text.Text == LocalizationCatalog.Text(settings.Language, "OffsetX")
                    || text.Text == LocalizationCatalog.Text(settings.Language, "OffsetY")));
            window.Close();
            window = null;
            Pump();
            Check("closing-preserves-position-reset", store.Load().PositionPreset == PositionPreset.AfterMenu
                && store.Load().OffsetX == 340 && store.Load().OffsetY == 0 && store.Load().Scale == 1.2 && !store.Load().AnimationsEnabled);
            window = CreateSettingsWindow();
            Page("Advanced");
            string resetLabel = LocalizationCatalog.Text(settings.Language, "ResetSettings");
            Button reset = Descendants((DependencyObject)window.Content).OfType<Button>().Single(button =>
                Descendants(button).OfType<TextBlock>().Any(text => text.Text == resetLabel));
            reset.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Pump();
            Check("reset-applies-defaults-through-the-shared-route", settings.Scale == 1 && settings.UseIsolatedAccountWindows
                && store.Load().Scale == 1 && SameHotkeys(settings.Hotkeys, HotkeySettings.CreateDefault()) && !DraftDirty());
            CheckSharedReferences("reset-retains-window-settings-references");
            window.Close();
            window = null;
            Pump();
            Check("closing-cannot-restore-pre-reset-settings", store.Load().Scale == 1 && store.Load().UseIsolatedAccountWindows);
            Check("settings-operations-preserve-account-data-and-other-stores", protectedFiles.All(path =>
                SHA256.HashData(System.IO.File.ReadAllBytes(path)).SequenceEqual(originalHashes[path])));
        }
        finally
        {
            failApply = false;
            window?.Close();
            overlay.Close();
            Pump();
            CodexProfileOverlay.App.ApplyTheme(AppTheme.Light);
        }
    }
}
