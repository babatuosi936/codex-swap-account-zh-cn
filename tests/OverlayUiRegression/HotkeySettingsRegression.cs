using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using CodexProfileOverlay.Core.Models;
using CodexProfileOverlay.Core.Services;
using Button = System.Windows.Controls.Button;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

internal static partial class Program
{
    private static void RunHotkeysScenario()
    {
        string root = System.IO.Path.Combine(AppContext.BaseDirectory, "local-test-data", "Hotkeys");
        System.IO.Directory.CreateDirectory(root);
        var store = new SettingsService(System.IO.Path.Combine(root, "settings.json"));
        var settings = new OverlaySettings { Language = LanguagePreference.ChineseSimplified };
        store.Save(settings);
        using var status = new ProfileStatusService(new ProfileStatusStore(System.IO.Path.Combine(root, "status.json")), new FixtureUsageProvider(), new SafeLogger(root));
        var assembly = typeof(CodexProfileOverlay.App).Assembly;
        Type windowType = assembly.GetType("CodexProfileOverlay.SettingsWindow")!;
        Type localizerType = assembly.GetType("CodexProfileOverlay.Localizer")!;
        object localizer = Activator.CreateInstance(localizerType, LanguagePreference.ChineseSimplified)!;
        ProfileInfo[] profiles = [new("hotkey-one", root, "unused") { DisplayName = "主账号" }, new("hotkey-two", root, "unused") { DisplayName = "475账号" }, new("hotkey-three", root, "unused") { DisplayName = "199账号" }];
        int saveCalls = 0;
        bool failSave = false;
        Window? window = null;
        Window CreateWindow()
        {
            var arguments = windowType.GetConstructors().Single().GetParameters().Select(parameter =>
                parameter.ParameterType == typeof(OverlaySettings) ? (object)settings
                : parameter.ParameterType == typeof(IReadOnlyList<ProfileInfo>) ? profiles
                : parameter.ParameterType == localizerType ? localizer
                : parameter.ParameterType == typeof(ProfileStatusService) ? status
                : parameter.ParameterType == typeof(BackupMaintenanceService) ? new BackupMaintenanceService(new AppPaths(root, root))
                : parameter.ParameterType == typeof(Action<OverlaySettings>) ? (Action<OverlaySettings>)(updated =>
                {
                    if (failSave) throw new System.IO.IOException("Synthetic save failure");
                    store.Save(updated);
                    saveCalls++;
                    if (window is not null)
                    {
                        // The real controller rebuilds the settings page after saving.
                        windowType.GetMethod("RefreshTheme")!.Invoke(window, null);
                        windowType.GetMethod("SetConflicts")!.Invoke(window, [Array.Empty<string>()]);
                    }
                })
                : parameter.ParameterType == typeof(Func<string, Task>) ? (Func<string, Task>)(_ => Task.CompletedTask)
                : (Action)(() => { })).ToArray();
            var result = (Window)windowType.GetConstructors().Single().Invoke(arguments);
            var page = windowType.GetField("page", BindingFlags.Instance | BindingFlags.NonPublic)!;
            page.SetValue(result, Enum.Parse(page.FieldType, "Hotkeys"));
            windowType.GetMethod("Rebuild", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(result, null);
            result.ShowInTaskbar = false;
            result.Show();
            Pump();
            return result;
        }
        Button ActionButton(string label) => Descendants((DependencyObject)window!.Content).OfType<Button>().Single(button => Equals(button.Content, label));
        Button[] Fields() => Descendants((DependencyObject)window!.Content).OfType<Button>().Where(button => button.MinWidth == 190).ToArray();
        void Click(Button button) { button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump(); }
        void Press(Button button, Key key) => button.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window!), 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
        void Check(string stage, bool condition) { Require(condition, "Hotkey draft regression: " + stage); Evidence.Add(new { kind = "hotkey-draft", stage, saveCalls }); }
        bool AllEmpty(HotkeySettings hotkeys) => hotkeys.ToggleOverlay is null && hotkeys.ProfileHotkeys.All(gesture => gesture is null);
        bool AllSet(HotkeySettings hotkeys) => hotkeys.ToggleOverlay is { IsEmpty: false } && hotkeys.ProfileHotkeys.All(gesture => gesture is { IsEmpty: false });
        try
        {
            window = CreateWindow();
            var clearButtons = Descendants((DependencyObject)window.Content).OfType<Button>().Where(button => Equals(button.Content, "清除")).ToArray();
            Require(clearButtons.Length == 4, "Each visible hotkey needs its own Clear button.");
            Click(clearButtons[0]);
            Check("left-click-clear-is-only-a-draft", Equals(Fields()[0].Content, "未设置") && AllSet(settings.Hotkeys) && AllSet(store.Load().Hotkeys) && saveCalls == 0);
            Click(Fields()[0]);
            Check("recording-field-shows-backspace", Fields()[0].Content is TextBlock hint && hint.Text.Contains("Backspace 清除") && hint.Text.Contains("Esc 取消"));
            var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            bitmap.Render(window);
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            using (var preview = System.IO.File.Create(System.IO.Path.Combine(AppContext.BaseDirectory, "hotkey-settings-preview.png"))) encoder.Save(preview);
            Press(Fields()[0], Key.Escape);
            Check("escape-preserves-current-draft", Equals(Fields()[0].Content, "未设置") && saveCalls == 0);
            Fields()[1].RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Right) { RoutedEvent = UIElement.PreviewMouseRightButtonUpEvent });
            Check("right-click-clear-is-only-a-draft", Equals(Fields()[1].Content, "未设置") && AllSet(settings.Hotkeys) && saveCalls == 0);
            Click(Fields()[2]);
            Press(Fields()[2], Key.Back);
            Check("backspace-clear-is-only-a-draft", Equals(Fields()[2].Content, "未设置") && AllSet(settings.Hotkeys) && saveCalls == 0);
            Click(ActionButton("全部清除"));
            Check("clear-all-waits-for-save", Fields().All(button => Equals(button.Content, "未设置")) && ActionButton("保存").IsEnabled && AllSet(settings.Hotkeys) && AllSet(store.Load().Hotkeys));
            // Other settings and window geometry also save through this method.
            windowType.GetMethod("Save", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);
            Check("other-setting-save-does-not-commit-hotkey-draft", AllSet(settings.Hotkeys) && AllSet(store.Load().Hotkeys));
            Click(ActionButton("保存"));
            Check("explicit-save-persists-no-hotkeys", AllEmpty(settings.Hotkeys) && AllEmpty(store.Load().Hotkeys) && Fields().All(button => Equals(button.Content, "未设置")) && !ActionButton("保存").IsEnabled);
            var managerType = assembly.GetType("CodexProfileOverlay.HotkeyManager")!;
            using (var manager = (IDisposable)Activator.CreateInstance(managerType, new WindowInteropHelper(window).Handle)!)
            {
                var conflicts = (IReadOnlyList<string>)managerType.GetMethod("Register")!.Invoke(manager, [settings.Hotkeys, profiles.Length])!;
                var registrations = (System.Collections.IDictionary)managerType.GetField("registrations", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(manager)!;
                Check("no-hotkeys-mode-registers-nothing", conflicts.Count == 0 && registrations.Count == 0);
            }
            Click(ActionButton("重置快捷键"));
            failSave = true;
            Click(ActionButton("保存"));
            Check("failed-save-preserves-live-settings", AllEmpty(settings.Hotkeys) && AllEmpty(store.Load().Hotkeys) && ActionButton("保存").IsEnabled);
            failSave = false;
            window.Close();
            window = null;
            Check("closing-discards-unsaved-reset", AllEmpty(store.Load().Hotkeys));
            settings = store.Load();
            window = CreateWindow();
            Check("reopening-keeps-hotkeys-unset", Fields().Length == 4 && Fields().All(button => Equals(button.Content, "未设置")));
            Click(ActionButton("重置快捷键"));
            Check("reset-waits-for-save", AllEmpty(settings.Hotkeys) && AllEmpty(store.Load().Hotkeys));
            Click(ActionButton("保存"));
            Check("saved-reset-restores-defaults", AllSet(settings.Hotkeys) && AllSet(store.Load().Hotkeys));
        }
        finally { failSave = false; window?.Close(); Pump(); }
    }
}
