using Wallpache.App.Library;
using Xunit;

namespace Wallpache.Tests;

/// <summary>
/// GIFs are converted to H.264 on the way in. The conversion itself decodes and
/// re-encodes through WinRT, which only exists on Windows, so what runs on any
/// host is the routing around it: which files take the converted path and what
/// they are stored as. The checklist covers a converted GIF on real hardware.
/// </summary>
public class GifTranscoderTests
{
    [Theory]
    [InlineData("Clip.gif")]
    [InlineData("Clip.GIF")]
    [InlineData(@"C:\Videos\Holiday clip.Gif")]
    public void GifsTakeTheConvertedPath(string path) => Assert.True(GifTranscoder.IsGif(path));

    [Theory]
    [InlineData("Clip.mp4")]
    [InlineData("Clip.mov")]
    [InlineData("Clip.m4v")]
    [InlineData("Clip.gif.mp4")]
    public void EverythingElseIsCopiedAsItIs(string path) => Assert.False(GifTranscoder.IsGif(path));

    [Fact]
    public void GifsAreOfferedAtImport() =>
        Assert.Contains(".gif", WallpaperLibraryService.SupportedExtensions);

    [Fact]
    public void AConvertedGifIsStoredAsMp4() =>
        Assert.Equal(".mp4", GifTranscoder.TranscodedFileExtension);
}
