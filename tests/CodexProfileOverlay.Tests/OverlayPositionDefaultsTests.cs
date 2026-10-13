using CodexProfileOverlay.Core.Models;
using CodexProfileOverlay.Core.Services;

namespace CodexProfileOverlay.Tests;

public sealed class OverlayPositionDefaultsTests
{
    [Fact]
    public void NewSettingsUseCurrentMenuRightPosition()
    {
        var settings = new OverlaySettings();
        Assert.Equal(PositionPreset.AfterMenu, settings.PositionPreset);
        Assert.Equal(340, settings.OffsetX);
        Assert.Equal(0, settings.OffsetY);
        Assert.Equal(1, settings.Scale);
    }

    [Fact]
    public void ResetPositionUsesCurrentDefaultsAndPreservesOtherPreferences()
    {
        using var temp = new TempDirectory();
        var store = new SettingsService(Path.Combine(temp.Path, "settings.json"));
        var settings = new OverlaySettings
        {
            PositionPreset = PositionPreset.TopRight,
            OffsetX = 999,
            OffsetY = 200,
            Scale = 1.2,
            AnimationsEnabled = false,
            UseIsolatedAccountWindows = false,
        };
        settings.ResetPosition();
        store.Save(settings);
        OverlaySettings loaded = store.Load();
        Assert.Equal(PositionPreset.AfterMenu, loaded.PositionPreset);
        Assert.Equal(340, loaded.OffsetX);
        Assert.Equal(0, loaded.OffsetY);
        Assert.Equal(1.2, loaded.Scale);
        Assert.False(loaded.AnimationsEnabled);
        Assert.False(loaded.UseIsolatedAccountWindows);
    }

    [Fact]
    public void ResetPlacementAndMenuFallbackAgreeOnCurrentCoordinates()
    {
        var settings = new OverlaySettings { OffsetX = 888, OffsetY = 50 };
        settings.ResetPosition();
        var service = new OverlayLayoutService();
        Assert.Equal(new OverlayPlacement(340, 0), service.CalculatePlacement(settings.PositionPreset,
            1600, 900, 900, 44, settings.OffsetX, settings.OffsetY));
        Assert.Equal(new OverlayPlacement(340, 0), service.CalculatePlacement(PositionPreset.AfterMenu,
            1600, 900, 900, 44, 0, -1));
    }
}
