using Wallpache.App.Displays;
using Wallpache.App.Playback;
using Xunit;

namespace Wallpache.Tests;

/// <summary>
/// The scaling modes are the one place where the video, the desktop still, and
/// the UI label all have to agree, so each mapping is pinned down.
/// </summary>
public class ScalingModeTests
{
    [Theory]
    [InlineData(ScalingMode.Fill, "Fill")]
    [InlineData(ScalingMode.Fit, "Fit")]
    [InlineData(ScalingMode.Stretch, "Stretch")]
    [InlineData(ScalingMode.Center, "Center")]
    public void DisplayNameMatchesTheMacLabel(ScalingMode mode, string expected) =>
        Assert.Equal(expected, mode.DisplayName());

    [Theory]
    [InlineData(ScalingMode.Fill, 4)]    // DWPOS_FILL
    [InlineData(ScalingMode.Fit, 3)]     // DWPOS_FIT
    [InlineData(ScalingMode.Stretch, 2)] // DWPOS_STRETCH
    [InlineData(ScalingMode.Center, 0)]  // DWPOS_CENTER
    public void DesktopWallpaperPositionFramesTheStillTheSameWay(ScalingMode mode, int expected) =>
        Assert.Equal(expected, mode.ToDesktopWallpaperPosition());

    [Fact]
    public void EveryModeIsOfferedInTheUi() =>
        Assert.Equal(4, Enum.GetValues<ScalingMode>().Length);

    [Fact]
    public void SupportedRatesCoverTheDocumentedRange()
    {
        Assert.Equal(
            [0.25, 0.5, 0.75, 1.0, 1.25, 1.5, 2.0],
            DisplayWallpaperConfiguration.SupportedRates);
    }

    [Fact]
    public void DefaultConfigurationIsMutedFillAtNormalSpeed()
    {
        var configuration = new DisplayWallpaperConfiguration("DISPLAY1");

        Assert.Equal(ScalingMode.Fill, configuration.ScalingMode);
        Assert.Equal(1.0, configuration.PlaybackRate);
        Assert.True(configuration.IsMuted);
        Assert.False(configuration.SetsDesktopPicture);
        Assert.Null(configuration.WallpaperId);
    }

    [Fact]
    public void CopyIsIndependentOfTheOriginal()
    {
        var original = new DisplayWallpaperConfiguration("DISPLAY1") { ScalingMode = ScalingMode.Fit };
        var copy = original.Copy();

        copy.ScalingMode = ScalingMode.Center;

        Assert.Equal(ScalingMode.Fit, original.ScalingMode);
        Assert.False(original.Matches(copy));
    }
}
