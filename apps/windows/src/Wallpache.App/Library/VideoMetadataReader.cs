using System;
using System.IO;
using System.Threading.Tasks;
using Wallpache.App.Support;
using Windows.Media.Editing;
using Windows.Storage;

namespace Wallpache.App.Library;

public readonly record struct VideoMetadata(double Duration, int? Width, int? Height);

/// <summary>
/// Validates a candidate video and extracts the metadata the library needs.
///
/// Validation is deliberately strict: a file that reaches a
/// <c>WallpaperSession</c> must be playable, so the failure is reported at
/// import time rather than as a black desktop later.
/// </summary>
public static class VideoMetadataReader
{
    /// <summary>MF_E_DRM_UNSUPPORTED.</summary>
    private const int DrmUnsupported = unchecked((int)0xC00D0BB8);

    public static async Task<VideoMetadata> ReadAsync(string path)
    {
        var name = Path.GetFileName(path);

        if (!File.Exists(path))
        {
            throw WallpaperException.FileMissing(name);
        }

        StorageFile file;
        try
        {
            file = await StorageFile.GetFileFromPathAsync(path);
        }
        catch (Exception error)
        {
            throw WallpaperException.ImportFailed(name, error.Message);
        }

        var properties = await file.Properties.GetVideoPropertiesAsync();
        var width = (int)properties.Width;
        var height = (int)properties.Height;
        var duration = properties.Duration.TotalSeconds;

        // Creating a clip is what actually proves the installed codecs can
        // decode this file; the shell properties above come from the container.
        MediaClip clip;
        try
        {
            clip = await MediaClip.CreateFromFileAsync(file);
        }
        catch (Exception error) when (error.HResult == DrmUnsupported)
        {
            throw WallpaperException.ProtectedContent(name);
        }
        catch (Exception error)
        {
            Log.Library.Error($"Clip creation failed for {name}: {error.Message}");
            throw WallpaperException.NotPlayable(name);
        }

        if (duration <= 0)
        {
            duration = clip.OriginalDuration.TotalSeconds;
        }

        if (width <= 0 || height <= 0)
        {
            throw WallpaperException.NoVideoTrack(name);
        }

        if (double.IsNaN(duration) || double.IsInfinity(duration) || duration <= 0)
        {
            throw WallpaperException.EmptyDuration(name);
        }

        return new VideoMetadata(duration, width, height);
    }
}
