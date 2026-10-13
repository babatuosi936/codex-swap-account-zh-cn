using System.Reflection;
using System.Text.Json;
using CodexProfileOverlay.Core.Models;
using CodexProfileOverlay.Core.Services;

namespace CodexProfileOverlay.Tests;

public sealed class SettingsTransferTests
{
    [Fact]
    public void ReadImport_CustomApplicationDataDirectoryDoesNotTouchDefaultStore()
    {
        using var temp = new TempDirectory();
        string localAppData = Path.Combine(temp.Path, "local");
        var defaultPaths = new AppPaths(Path.Combine(temp.Path, "user"), localAppData);
        var customPaths = new AppPaths(Path.Combine(temp.Path, "user"), localAppData,
            Path.Combine(temp.Path, "custom-application-data"));
        var defaultService = new SettingsService(defaultPaths.SettingsFile);
        var customService = new SettingsService(customPaths.SettingsFile);
        defaultService.Save(new OverlaySettings { OffsetX = 111, StartWithWindows = false });
        customService.Save(new OverlaySettings { OffsetX = 222, UseIsolatedAccountWindows = false });
        byte[] defaultBefore = File.ReadAllBytes(defaultPaths.SettingsFile);
        byte[] customBefore = File.ReadAllBytes(customPaths.SettingsFile);
        string sourceFile = Path.Combine(temp.Path, "import.json");
        File.WriteAllText(sourceFile, """{"offsetX":333,"useIsolatedAccountWindows":true}""");

        OverlaySettings imported = customService.ReadImport(sourceFile);

        Assert.Equal(333, imported.OffsetX);
        Assert.True(imported.UseIsolatedAccountWindows);
        Assert.Equal(defaultBefore, File.ReadAllBytes(defaultPaths.SettingsFile));
        Assert.Equal(customBefore, File.ReadAllBytes(customPaths.SettingsFile));

        customService.Save(imported);

        Assert.Equal(333, customService.Load().OffsetX);
        Assert.Equal(defaultBefore, File.ReadAllBytes(defaultPaths.SettingsFile));
        Assert.False(Directory.Exists(customPaths.SharedCodexDirectory));
        Assert.False(Directory.Exists(customPaths.ProfilesDirectory));
    }

    [Fact]
    public void ExportTo_UsesLiveSettingsInsteadOfPersistedOrDefaultApplicationData()
    {
        using var temp = new TempDirectory();
        string localAppData = Path.Combine(temp.Path, "local");
        var defaultPaths = new AppPaths(Path.Combine(temp.Path, "user"), localAppData);
        var customPaths = new AppPaths(Path.Combine(temp.Path, "user"), localAppData,
            Path.Combine(temp.Path, "custom-application-data"));
        var defaultService = new SettingsService(defaultPaths.SettingsFile);
        var service = new SettingsService(customPaths.SettingsFile);
        defaultService.Save(new OverlaySettings { OffsetX = 111, Theme = AppTheme.Dark });
        service.Save(new OverlaySettings { OffsetX = 222, UseIsolatedAccountWindows = false });
        byte[] defaultBefore = File.ReadAllBytes(defaultPaths.SettingsFile);
        byte[] activeBefore = File.ReadAllBytes(customPaths.SettingsFile);
        string otherStore = Path.Combine(customPaths.ApplicationDataDirectory, "profiles.json");
        File.WriteAllText(otherStore, "unrelated metadata sentinel");
        string destination = Path.Combine(temp.Path, "export", "settings.json");
        var live = new OverlaySettings
        {
            OffsetX = 333,
            Theme = AppTheme.Light,
            Language = LanguagePreference.ChineseSimplified,
            UseIsolatedAccountWindows = true,
            Hotkeys = new HotkeySettings
            {
                ToggleOverlay = new HotkeyGesture(HotkeyModifiers.Control | HotkeyModifiers.Shift, 'K'),
                ProfileHotkeys = [null, new HotkeyGesture(HotkeyModifiers.Alt, '2')],
            },
        };

        service.ExportTo(live, destination);

        OverlaySettings exported = new SettingsService(destination).ReadImport(destination);
        Assert.Equal(333, exported.OffsetX);
        Assert.Equal(AppTheme.Light, exported.Theme);
        Assert.Equal(LanguagePreference.ChineseSimplified, exported.Language);
        Assert.True(exported.UseIsolatedAccountWindows);
        Assert.Equal(live.Hotkeys.ToggleOverlay, exported.Hotkeys.ToggleOverlay);
        Assert.Equal(live.Hotkeys.ProfileHotkeys, exported.Hotkeys.ProfileHotkeys);
        Assert.Equal(defaultBefore, File.ReadAllBytes(defaultPaths.SettingsFile));
        Assert.Equal(activeBefore, File.ReadAllBytes(customPaths.SettingsFile));
        Assert.Equal("unrelated metadata sentinel", File.ReadAllText(otherStore));
        Assert.False(File.Exists(destination + ".tmp"));
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(destination));
        string[] properties = document.RootElement.EnumerateObject().Select(property => property.Name).ToArray();
        Assert.DoesNotContain("auth", properties);
        Assert.DoesNotContain("profiles", properties);
        Assert.DoesNotContain("sessions", properties);
        Assert.DoesNotContain("unrelated metadata sentinel", File.ReadAllText(destination));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{ not valid json")]
    [InlineData("[]")]
    [InlineData("{\"displayMode\":\"FutureMode\"}")]
    [InlineData("{\"positionPreset\":\"FuturePosition\"}")]
    [InlineData("{\"theme\":\"FutureTheme\"}")]
    [InlineData("{\"language\":\"FutureLanguage\"}")]
    [InlineData("{\"displayMode\":1}")]
    [InlineData("{\"positionPreset\":1}")]
    [InlineData("{\"theme\":1}")]
    [InlineData("{\"language\":1}")]
    [InlineData("{\"hotkeys\":{\"toggleOverlay\":{\"modifiers\":1,\"key\":75}}}")]
    [InlineData("{\"hotkeys\":{\"toggleOverlay\":{\"modifiers\":\"FutureModifier\",\"key\":75}}}")]
    public void ReadImport_InvalidDocumentIsRejectedAndActiveSettingsStayUnchanged(string json)
    {
        using var temp = new TempDirectory();
        string activeFile = Path.Combine(temp.Path, "active", "settings.json");
        var service = new SettingsService(activeFile);
        service.Save(new OverlaySettings { OffsetX = 123, StartWithWindows = false });
        byte[] before = File.ReadAllBytes(activeFile);
        string sourceFile = Path.Combine(temp.Path, "invalid.json");
        File.WriteAllText(sourceFile, json);

        Exception? exception = Record.Exception(() => service.ReadImport(sourceFile));

        Assert.NotNull(exception);
        Assert.True(exception is JsonException or InvalidDataException,
            $"Import should report a validation error, not {exception.GetType().Name}.");
        Assert.Equal(before, File.ReadAllBytes(activeFile));
        Assert.Equal(123, service.Load().OffsetX);
        Assert.False(service.Load().StartWithWindows);
    }

    [Fact]
    public void ReadImport_OversizedDocumentIsRejectedBeforeSaving()
    {
        using var temp = new TempDirectory();
        string activeFile = Path.Combine(temp.Path, "active.json");
        var service = new SettingsService(activeFile);
        service.Save(new OverlaySettings { OffsetX = 456 });
        byte[] before = File.ReadAllBytes(activeFile);
        string sourceFile = Path.Combine(temp.Path, "oversized.json");
        File.WriteAllText(sourceFile, new string(' ', 1024 * 1024) + "{}");

        Assert.Throws<InvalidDataException>(() => service.ReadImport(sourceFile));

        Assert.Equal(before, File.ReadAllBytes(activeFile));
    }

    [Fact]
    public void ReadImport_OlderJsonUsesStartupAndIndependentWindowDefaults()
    {
        using var temp = new TempDirectory();
        string activeFile = Path.Combine(temp.Path, "active.json");
        string sourceFile = Path.Combine(temp.Path, "older-settings.json");
        File.WriteAllText(sourceFile, """{"displayMode":"Compact","offsetX":42}""");

        OverlaySettings imported = new SettingsService(activeFile).ReadImport(sourceFile);

        Assert.Equal(OverlayDisplayMode.Compact, imported.DisplayMode);
        Assert.Equal(42, imported.OffsetX);
        Assert.True(imported.StartWithWindows);
        Assert.True(imported.UseIsolatedAccountWindows);
        Assert.NotNull(imported.Hotkeys);
        Assert.NotNull(imported.Hotkeys.ProfileHotkeys);
        Assert.False(File.Exists(activeFile));
    }

    [Fact]
    public void ReadImport_NormalizesNumbersWithoutMutatingActiveStore()
    {
        using var temp = new TempDirectory();
        string activeFile = Path.Combine(temp.Path, "active.json");
        var service = new SettingsService(activeFile);
        service.Save(new OverlaySettings { OffsetX = 456 });
        byte[] before = File.ReadAllBytes(activeFile);
        string sourceFile = Path.Combine(temp.Path, "out-of-range.json");
        File.WriteAllText(sourceFile, """
            {
              "offsetX": -12,
              "offsetY": 9999,
              "scale": 8,
              "gracefulCloseTimeoutSeconds": 0,
              "settingsWindowLeft": 200000,
              "settingsWindowWidth": 1,
              "settingsWindowHeight": 9999,
              "yellowThresholdPercent": 0,
              "greenThresholdPercent": 0,
              "staleDataThresholdMinutes": 0,
              "activeProfileRefreshIntervalSeconds": 0,
              "inactiveProfileRefreshIntervalSeconds": 0
            }
            """);

        OverlaySettings imported = service.ReadImport(sourceFile);

        Assert.Equal(0, imported.OffsetX);
        Assert.Equal(4000, imported.OffsetY);
        Assert.Equal(1.4, imported.Scale);
        Assert.Equal(1, imported.GracefulCloseTimeoutSeconds);
        Assert.Equal(-1, imported.SettingsWindowLeft);
        Assert.Equal(900, imported.SettingsWindowWidth);
        Assert.Equal(1400, imported.SettingsWindowHeight);
        Assert.Equal(1, imported.YellowThresholdPercent);
        Assert.Equal(2, imported.GreenThresholdPercent);
        Assert.Equal(10, imported.StaleDataThresholdMinutes);
        Assert.Equal(15, imported.ActiveProfileRefreshIntervalSeconds);
        Assert.Equal(30, imported.InactiveProfileRefreshIntervalSeconds);
        Assert.Equal(before, File.ReadAllBytes(activeFile));
    }

    [Fact]
    public void ExportTo_NormalizesValuesAndAtomicallyReplacesExistingDestination()
    {
        using var temp = new TempDirectory();
        string activeFile = Path.Combine(temp.Path, "active.json");
        var service = new SettingsService(activeFile);
        service.Save(new OverlaySettings { OffsetX = 456 });
        byte[] before = File.ReadAllBytes(activeFile);
        string destination = Path.Combine(temp.Path, "exported.json");
        File.WriteAllText(destination, "old export sentinel");

        service.ExportTo(new OverlaySettings
        {
            OffsetX = double.NaN,
            OffsetY = double.PositiveInfinity,
            Scale = double.NaN,
            GracefulCloseTimeoutSeconds = 999,
            SettingsWindowWidth = double.NaN,
            ActiveProfileRefreshIntervalSeconds = 1,
        }, destination);

        OverlaySettings exported = new SettingsService(destination).ReadImport(destination);
        Assert.Equal(340, exported.OffsetX);
        Assert.Equal(0, exported.OffsetY);
        Assert.Equal(1, exported.Scale);
        Assert.Equal(60, exported.GracefulCloseTimeoutSeconds);
        Assert.Equal(1000, exported.SettingsWindowWidth);
        Assert.Equal(15, exported.ActiveProfileRefreshIntervalSeconds);
        Assert.Equal(before, File.ReadAllBytes(activeFile));
        Assert.False(File.Exists(destination + ".tmp"));
    }

    [Fact]
    public void CopyFrom_PreservesTargetReferenceCopiesAllPropertiesAndClonesHotkeys()
    {
        var source = new OverlaySettings
        {
            DisplayMode = OverlayDisplayMode.Compact,
            PositionPreset = PositionPreset.Custom,
            OffsetX = 123,
            OffsetY = 234,
            Scale = 1.2,
            AnimationsEnabled = false,
            StartWithWindows = false,
            ShowAutomaticallyWhenCodexOpens = false,
            LaunchCodexWhenOverlayStarts = true,
            LaunchCodexAfterSwitching = false,
            UseIsolatedAccountWindows = false,
            GracefulCloseTimeoutSeconds = 12,
            ForceCloseFallback = false,
            ConfirmBeforeForceClose = false,
            Hotkeys = new HotkeySettings
            {
                ToggleOverlay = new HotkeyGesture(HotkeyModifiers.Alt | HotkeyModifiers.Shift, 'J'),
                ProfileHotkeys = [new HotkeyGesture(HotkeyModifiers.Control, '2'), null],
            },
            Language = LanguagePreference.ChineseSimplified,
            Theme = AppTheme.Light,
            SettingsWindowLeft = 321,
            SettingsWindowTop = 432,
            SettingsWindowWidth = 1100,
            SettingsWindowHeight = 800,
            ShowAutomaticLimitIndicators = true,
            GreenThresholdPercent = 70,
            YellowThresholdPercent = 40,
            StaleDataThresholdMinutes = 100,
            LowWarningThresholdPercent = 30,
            ShowIndicatorsInOverlay = false,
            ShowManualProfileEmojiInOverlay = true,
            WarnWhenNearlyExhausted = false,
            ActiveProfileRefreshIntervalMinutes = 20,
            InactiveProfileRefreshIntervalMinutes = 120,
            ActiveProfileRefreshIntervalSeconds = 45,
            InactiveProfileRefreshIntervalSeconds = 150,
        };
        var target = new OverlaySettings();
        OverlaySettings windowReference = target;

        target.CopyFrom(source);

        Assert.Same(windowReference, target);
        foreach (PropertyInfo property in typeof(OverlaySettings).GetProperties()
                     .Where(property => property.Name != nameof(OverlaySettings.Hotkeys)))
        {
            Assert.Equal(property.GetValue(source), property.GetValue(windowReference));
        }
        Assert.NotSame(source.Hotkeys, target.Hotkeys);
        Assert.NotSame(source.Hotkeys.ProfileHotkeys, target.Hotkeys.ProfileHotkeys);
        Assert.Equal(source.Hotkeys.ToggleOverlay, target.Hotkeys.ToggleOverlay);
        Assert.Equal(source.Hotkeys.ProfileHotkeys, target.Hotkeys.ProfileHotkeys);

        source.Hotkeys.ToggleOverlay = null;
        source.Hotkeys.ProfileHotkeys[0] = null;
        source.Hotkeys.ProfileHotkeys.Add(new HotkeyGesture(HotkeyModifiers.Windows, '3'));

        Assert.NotNull(windowReference.Hotkeys.ToggleOverlay);
        Assert.Equal(new HotkeyGesture(HotkeyModifiers.Control, '2'), windowReference.Hotkeys.ProfileHotkeys[0]);
        Assert.Equal(2, windowReference.Hotkeys.ProfileHotkeys.Count);
    }

    [Fact]
    public void CopyFrom_DefaultSettingsResetsExistingInstanceForWindowObservers()
    {
        var current = new OverlaySettings
        {
            StartWithWindows = false,
            UseIsolatedAccountWindows = false,
            Theme = AppTheme.Light,
            Scale = 1.4,
            OffsetX = 999,
            Hotkeys = new HotkeySettings { ToggleOverlay = null, ProfileHotkeys = [] },
        };
        OverlaySettings windowReference = current;

        current.CopyFrom(new OverlaySettings());

        Assert.Same(current, windowReference);
        Assert.True(windowReference.StartWithWindows);
        Assert.True(windowReference.UseIsolatedAccountWindows);
        Assert.Equal(AppTheme.Dark, windowReference.Theme);
        Assert.Equal(1, windowReference.Scale);
        Assert.Equal(340, windowReference.OffsetX);
        Assert.Equal(0, windowReference.OffsetY);
        Assert.NotNull(windowReference.Hotkeys.ToggleOverlay);
        Assert.Equal(9, windowReference.Hotkeys.ProfileHotkeys.Count);
    }
}
