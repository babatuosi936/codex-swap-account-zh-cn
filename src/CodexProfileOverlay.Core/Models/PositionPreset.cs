namespace CodexProfileOverlay.Core.Models;

public enum PositionPreset
{
    AfterMenu,
    // Retain the serialized names for existing settings. These three preset
    // slots now place the overlay at the bottom left, center and right.
    TopLeft,
    TopCenter,
    TopRight,
    Custom,
}
