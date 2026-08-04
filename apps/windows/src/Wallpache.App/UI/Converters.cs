using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Wallpache.App.Playback;

namespace Wallpache.App.UI;

/// <summary>Presentation-only formatting, so the models stay free of display strings.</summary>
public static class Converters
{
    /// <summary>Fill, Fit, Stretch, Center.</summary>
    public static readonly IValueConverter ScalingModeName =
        new FuncValueConverter<ScalingMode, string>(mode => mode.DisplayName());

    /// <summary>Playback rate as "1×", "0.5×", "1.25×".</summary>
    public static readonly IValueConverter PlaybackRate =
        new FuncValueConverter<double, string>(rate => $"{rate.ToString("0.##", CultureInfo.CurrentCulture)}×");

    /// <summary>"Main" next to the primary display, blank otherwise.</summary>
    public static readonly IValueConverter PrimaryBadge =
        new FuncValueConverter<bool, string>(isPrimary => isPrimary ? "Main" : string.Empty);
}
