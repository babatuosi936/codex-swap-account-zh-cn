using System.Text.Json;
using System.Text.Json.Serialization;
using CodexProfileOverlay.Core.Models;

namespace CodexProfileOverlay.Core.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly JsonSerializerOptions ImportOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },
    };

    private readonly string settingsFile;

    public SettingsService(string settingsFile)
    {
        this.settingsFile = Path.GetFullPath(settingsFile);
    }

    public OverlaySettings Load()
    {
        if (!File.Exists(settingsFile))
        {
            return new OverlaySettings();
        }

        try
        {
            using FileStream stream = File.OpenRead(settingsFile);
            return Normalize(JsonSerializer.Deserialize<OverlaySettings>(stream, SerializerOptions));
        }
        catch (JsonException)
        {
            return new OverlaySettings();
        }
        catch (IOException)
        {
            return new OverlaySettings();
        }
    }

    public void Save(OverlaySettings settings)
    {
        WriteSettings(settings, settingsFile);
    }

    public void ExportTo(OverlaySettings settings, string destinationFile)
    {
        WriteSettings(settings, Path.GetFullPath(destinationFile));
    }

    public OverlaySettings ReadImport(string sourceFile)
    {
        using FileStream stream = File.OpenRead(Path.GetFullPath(sourceFile));
        if (stream.Length > 1024 * 1024)
        {
            throw new InvalidDataException("Settings file exceeds the supported size.");
        }

        OverlaySettings imported = JsonSerializer.Deserialize<OverlaySettings>(stream, ImportOptions)
            ?? throw new InvalidDataException("Settings file must contain a settings object.");
        if (!Enum.IsDefined(imported.DisplayMode) || !Enum.IsDefined(imported.PositionPreset)
            || !Enum.IsDefined(imported.Language) || !Enum.IsDefined(imported.Theme))
        {
            throw new InvalidDataException("Settings file contains unsupported option values.");
        }

        Normalize(imported);
        const HotkeyModifiers allowedModifiers = HotkeyModifiers.Alt | HotkeyModifiers.Control | HotkeyModifiers.Shift | HotkeyModifiers.Windows;
        foreach (HotkeyGesture? gesture in imported.Hotkeys.ProfileHotkeys.Prepend(imported.Hotkeys.ToggleOverlay))
        {
            if (gesture is not null && ((gesture.Modifiers & ~allowedModifiers) != 0 || gesture.Key is < 0 or > 255))
            {
                throw new InvalidDataException("Settings file contains unsupported hotkey values.");
            }
        }

        return imported;
    }

    private static void WriteSettings(OverlaySettings settings, string destinationFile)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Normalize(settings);
        Directory.CreateDirectory(Path.GetDirectoryName(destinationFile)!);

        string temp = destinationFile + ".tmp";
        using (FileStream stream = new(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(stream, settings, SerializerOptions);
        }

        if (File.Exists(destinationFile))
        {
            File.Replace(temp, destinationFile, null);
        }
        else
        {
            File.Move(temp, destinationFile);
        }
    }

    private static OverlaySettings Normalize(OverlaySettings? settings)
    {
        settings ??= new OverlaySettings();
        settings.OffsetX = ClampFinite(settings.OffsetX, 0, 4000, 396);
        settings.OffsetY = ClampFinite(settings.OffsetY, 0, 4000, 2);
        settings.Scale = ClampFinite(settings.Scale, 0.8, 1.4, 1);
        settings.GracefulCloseTimeoutSeconds = Math.Clamp(settings.GracefulCloseTimeoutSeconds, 1, 60);
        settings.SettingsWindowWidth = ClampFinite(settings.SettingsWindowWidth, 900, 1800, 1000);
        settings.SettingsWindowHeight = ClampFinite(settings.SettingsWindowHeight, 620, 1400, 720);
        settings.SettingsWindowLeft = NormalizeCoordinate(settings.SettingsWindowLeft, -1);
        settings.SettingsWindowTop = NormalizeCoordinate(settings.SettingsWindowTop, -1);
        settings.Hotkeys ??= HotkeySettings.CreateDefault();
        settings.Hotkeys.ProfileHotkeys ??= [];
        settings.YellowThresholdPercent = Math.Clamp(settings.YellowThresholdPercent, 1, 99);
        settings.GreenThresholdPercent = Math.Clamp(settings.GreenThresholdPercent, settings.YellowThresholdPercent + 1, 100);
        settings.StaleDataThresholdMinutes = Math.Clamp(settings.StaleDataThresholdMinutes, 10, 1440);
        settings.LowWarningThresholdPercent = Math.Clamp(settings.LowWarningThresholdPercent, 1, 99);
        settings.ActiveProfileRefreshIntervalMinutes = Math.Clamp(settings.ActiveProfileRefreshIntervalMinutes, 10, 1440);
        settings.InactiveProfileRefreshIntervalMinutes = Math.Clamp(settings.InactiveProfileRefreshIntervalMinutes, 10, 1440);
        settings.ActiveProfileRefreshIntervalSeconds = Math.Clamp(settings.ActiveProfileRefreshIntervalSeconds, 15, 3600);
        settings.InactiveProfileRefreshIntervalSeconds = Math.Clamp(settings.InactiveProfileRefreshIntervalSeconds, 30, 86400);
        return settings;
    }

    private static double NormalizeCoordinate(double value, double fallback)
    {
        return double.IsFinite(value) && Math.Abs(value) <= 100000
            ? value
            : fallback;
    }

    private static double ClampFinite(double value, double minimum, double maximum, double fallback)
    {
        return double.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : fallback;
    }
}
