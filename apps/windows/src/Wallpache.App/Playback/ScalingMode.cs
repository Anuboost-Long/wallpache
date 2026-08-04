using System.Text.Json.Serialization;
using Windows.UI.Composition;

namespace Wallpache.App.Playback;

/// <summary>How a video is fitted into a display.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ScalingMode>))]
public enum ScalingMode
{
    /// <summary>Preserve aspect ratio, crop the overflow.</summary>
    Fill,

    /// <summary>Preserve aspect ratio, show the whole frame.</summary>
    Fit,

    /// <summary>Ignore aspect ratio and cover the display.</summary>
    Stretch,

    /// <summary>Keep the natural pixel size, centred on a black background.</summary>
    Center
}

public static class ScalingModeExtensions
{
    public static string DisplayName(this ScalingMode mode) => mode switch
    {
        ScalingMode.Fill => "Fill",
        ScalingMode.Fit => "Fit",
        ScalingMode.Stretch => "Stretch",
        ScalingMode.Center => "Center",
        _ => "Fill"
    };

    /// <summary>
    /// The composition equivalent. The video surface is created at the clip's
    /// natural pixel size, so the brush stretch is what actually resolves the
    /// mode against the display bounds.
    /// </summary>
    internal static CompositionStretch ToCompositionStretch(this ScalingMode mode) => mode switch
    {
        ScalingMode.Fill => CompositionStretch.UniformToFill,
        ScalingMode.Fit => CompositionStretch.Uniform,
        ScalingMode.Stretch => CompositionStretch.Fill,
        ScalingMode.Center => CompositionStretch.None,
        _ => CompositionStretch.UniformToFill
    };

    /// <summary>The Windows desktop-wallpaper position that frames a still the same way.</summary>
    internal static int ToDesktopWallpaperPosition(this ScalingMode mode) => mode switch
    {
        ScalingMode.Fill => 4,     // DWPOS_FILL
        ScalingMode.Fit => 3,      // DWPOS_FIT
        ScalingMode.Stretch => 2,  // DWPOS_STRETCH
        ScalingMode.Center => 0,   // DWPOS_CENTER
        _ => 4
    };
}
