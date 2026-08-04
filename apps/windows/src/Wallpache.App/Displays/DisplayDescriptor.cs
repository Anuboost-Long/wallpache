using System;

namespace Wallpache.App.Displays;

/// <summary>
/// A snapshot of one connected display, keyed by an identifier that survives
/// reordering, sleep, and reconnection.
///
/// <see cref="Id"/> is the monitor's device interface path where Windows can
/// supply one, so docking a laptop does not shuffle wallpaper assignments the
/// way an index or a bare <c>\\.\DISPLAY1</c> name would.
/// </summary>
public sealed record DisplayDescriptor(
    string Id,
    IntPtr MonitorHandle,
    string DeviceName,
    string Name,
    int X,
    int Y,
    int Width,
    int Height,
    bool IsPrimary,
    double ScaleFactor)
{
    /// <summary>Pixel resolution, useful when explaining energy impact to the user.</summary>
    public string ResolutionDescription => $"{Width} × {Height}";

    /// <summary>
    /// Compares everything that would require a session to be moved or resized.
    /// The monitor handle is deliberately excluded: it changes on every
    /// reconfiguration without meaning the display did.
    /// </summary>
    public bool IsEquivalentTo(DisplayDescriptor other) =>
        Id == other.Id
        && DeviceName == other.DeviceName
        && Name == other.Name
        && X == other.X
        && Y == other.Y
        && Width == other.Width
        && Height == other.Height
        && IsPrimary == other.IsPrimary;
}
