using System;
using System.Text.Json.Serialization;
using Wallpache.App.Playback;

namespace Wallpache.App.Displays;

/// <summary>The persisted wallpaper assignment for one display.</summary>
public sealed class DisplayWallpaperConfiguration
{
    /// <summary>Playback rates offered in the UI.</summary>
    public static readonly double[] SupportedRates = [0.25, 0.5, 0.75, 1.0, 1.25, 1.5, 2.0];

    public DisplayWallpaperConfiguration()
    {
        DisplayId = string.Empty;
    }

    public DisplayWallpaperConfiguration(string displayId)
    {
        DisplayId = displayId;
    }

    [JsonPropertyName("displayId")]
    public string DisplayId { get; set; }

    [JsonPropertyName("wallpaperId")]
    public Guid? WallpaperId { get; set; }

    [JsonPropertyName("scalingMode")]
    public ScalingMode ScalingMode { get; set; } = ScalingMode.Fill;

    [JsonPropertyName("playbackRate")]
    public double PlaybackRate { get; set; } = 1.0;

    [JsonPropertyName("muted")]
    public bool IsMuted { get; set; } = true;

    /// <summary>
    /// Whether the video's still frame is also set as the Windows desktop
    /// picture, so the display keeps a matching background once the app quits.
    /// </summary>
    [JsonPropertyName("setsDesktopPicture")]
    public bool SetsDesktopPicture { get; set; }

    public DisplayWallpaperConfiguration Copy() => new(DisplayId)
    {
        WallpaperId = WallpaperId,
        ScalingMode = ScalingMode,
        PlaybackRate = PlaybackRate,
        IsMuted = IsMuted,
        SetsDesktopPicture = SetsDesktopPicture
    };

    /// <summary>Clamps values a hand-edited or older settings file could carry.</summary>
    public void Normalize()
    {
        if (PlaybackRate is <= 0 or > 4)
        {
            PlaybackRate = 1.0;
        }

        if (!Enum.IsDefined(ScalingMode))
        {
            ScalingMode = ScalingMode.Fill;
        }
    }

    public bool Matches(DisplayWallpaperConfiguration other) =>
        WallpaperId == other.WallpaperId
        && ScalingMode == other.ScalingMode
        && Math.Abs(PlaybackRate - other.PlaybackRate) < 0.0001
        && IsMuted == other.IsMuted
        && SetsDesktopPicture == other.SetsDesktopPicture;
}
