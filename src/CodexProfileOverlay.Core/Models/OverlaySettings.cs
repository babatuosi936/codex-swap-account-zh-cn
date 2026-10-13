namespace CodexProfileOverlay.Core.Models;

public sealed class OverlaySettings
{
    public const PositionPreset DefaultPositionPreset = PositionPreset.AfterMenu;
    public const double DefaultOffsetX = 340;
    public const double DefaultOffsetY = 0;
    public const double DefaultScale = 1;

    public OverlayDisplayMode DisplayMode { get; set; } = OverlayDisplayMode.Auto;

    public PositionPreset PositionPreset { get; set; } = DefaultPositionPreset;

    public double OffsetX { get; set; } = DefaultOffsetX;

    public double OffsetY { get; set; } = DefaultOffsetY;

    public double Scale { get; set; } = DefaultScale;

    public bool AnimationsEnabled { get; set; } = true;

    public bool StartWithWindows { get; set; } = true;

    public bool ShowAutomaticallyWhenCodexOpens { get; set; } = true;

    public bool LaunchCodexWhenOverlayStarts { get; set; }

    public bool LaunchCodexAfterSwitching { get; set; } = true;

    public bool UseIsolatedAccountWindows { get; set; } = true;

    public int GracefulCloseTimeoutSeconds { get; set; } = 5;

    public bool ForceCloseFallback { get; set; } = true;

    public bool ConfirmBeforeForceClose { get; set; } = true;

    public HotkeySettings Hotkeys { get; set; } = HotkeySettings.CreateDefault();

    public LanguagePreference Language { get; set; } = LanguagePreference.SystemDefault;

    public AppTheme Theme { get; set; } = AppTheme.Dark;

    public double SettingsWindowLeft { get; set; } = -1;

    public double SettingsWindowTop { get; set; } = -1;

    public double SettingsWindowWidth { get; set; } = 1000;

    public double SettingsWindowHeight { get; set; } = 720;

    public bool ShowAutomaticLimitIndicators { get; set; } = false;

    public int GreenThresholdPercent { get; set; } = 60;

    public int YellowThresholdPercent { get; set; } = 25;

    public int StaleDataThresholdMinutes { get; set; } = 90;

    public int LowWarningThresholdPercent { get; set; } = 20;

    public bool ShowIndicatorsInOverlay { get; set; } = true;

    public bool ShowManualProfileEmojiInOverlay { get; set; }

    public bool WarnWhenNearlyExhausted { get; set; } = true;

    public int ActiveProfileRefreshIntervalMinutes { get; set; } = 15;

    public int InactiveProfileRefreshIntervalMinutes { get; set; } = 60;

    public int ActiveProfileRefreshIntervalSeconds { get; set; } = 30;

    public int InactiveProfileRefreshIntervalSeconds { get; set; } = 60;

    public void ResetPosition()
    {
        PositionPreset = DefaultPositionPreset;
        OffsetX = DefaultOffsetX;
        OffsetY = DefaultOffsetY;
    }

    /// <summary>Applies a complete settings replacement without invalidating the windows' shared reference.</summary>
    public void CopyFrom(OverlaySettings source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (ReferenceEquals(this, source)) return;

        DisplayMode = source.DisplayMode;
        PositionPreset = source.PositionPreset;
        OffsetX = source.OffsetX;
        OffsetY = source.OffsetY;
        Scale = source.Scale;
        AnimationsEnabled = source.AnimationsEnabled;
        StartWithWindows = source.StartWithWindows;
        ShowAutomaticallyWhenCodexOpens = source.ShowAutomaticallyWhenCodexOpens;
        LaunchCodexWhenOverlayStarts = source.LaunchCodexWhenOverlayStarts;
        LaunchCodexAfterSwitching = source.LaunchCodexAfterSwitching;
        UseIsolatedAccountWindows = source.UseIsolatedAccountWindows;
        GracefulCloseTimeoutSeconds = source.GracefulCloseTimeoutSeconds;
        ForceCloseFallback = source.ForceCloseFallback;
        ConfirmBeforeForceClose = source.ConfirmBeforeForceClose;
        Hotkeys = new HotkeySettings
        {
            ToggleOverlay = source.Hotkeys.ToggleOverlay,
            ProfileHotkeys = [.. source.Hotkeys.ProfileHotkeys],
        };
        Language = source.Language;
        Theme = source.Theme;
        SettingsWindowLeft = source.SettingsWindowLeft;
        SettingsWindowTop = source.SettingsWindowTop;
        SettingsWindowWidth = source.SettingsWindowWidth;
        SettingsWindowHeight = source.SettingsWindowHeight;
        ShowAutomaticLimitIndicators = source.ShowAutomaticLimitIndicators;
        GreenThresholdPercent = source.GreenThresholdPercent;
        YellowThresholdPercent = source.YellowThresholdPercent;
        StaleDataThresholdMinutes = source.StaleDataThresholdMinutes;
        LowWarningThresholdPercent = source.LowWarningThresholdPercent;
        ShowIndicatorsInOverlay = source.ShowIndicatorsInOverlay;
        ShowManualProfileEmojiInOverlay = source.ShowManualProfileEmojiInOverlay;
        WarnWhenNearlyExhausted = source.WarnWhenNearlyExhausted;
        ActiveProfileRefreshIntervalMinutes = source.ActiveProfileRefreshIntervalMinutes;
        InactiveProfileRefreshIntervalMinutes = source.InactiveProfileRefreshIntervalMinutes;
        ActiveProfileRefreshIntervalSeconds = source.ActiveProfileRefreshIntervalSeconds;
        InactiveProfileRefreshIntervalSeconds = source.InactiveProfileRefreshIntervalSeconds;
    }
}
