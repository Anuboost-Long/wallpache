using System;
using System.IO;
using Wallpache.App.Support;

namespace Wallpache.App.Library;

/// <summary>
/// Owns the on-disk layout of imported wallpapers.
///
/// <code>
/// %LOCALAPPDATA%\Wallpache\
/// ├── Wallpapers\&lt;guid&gt;.&lt;ext&gt;
/// ├── Stills\&lt;guid&gt;.png       full resolution, for preview and desktop picture
/// ├── Thumbnails\&lt;guid&gt;.png   grid size
/// ├── Logs\
/// └── settings.json
/// </code>
/// </summary>
public sealed class WallpaperStorage
{
    public const string VideosDirectoryName = "Wallpapers";
    public const string ThumbnailsDirectoryName = "Thumbnails";
    public const string StillsDirectoryName = "Stills";
    public const string LogsDirectoryName = "Logs";
    public const string SettingsFileName = "settings.json";

    public WallpaperStorage(string root)
    {
        Root = root;
    }

    public string Root { get; }

    public string VideosDirectory => Path.Combine(Root, VideosDirectoryName);

    public string ThumbnailsDirectory => Path.Combine(Root, ThumbnailsDirectoryName);

    public string StillsDirectory => Path.Combine(Root, StillsDirectoryName);

    public string LogsDirectory => Path.Combine(Root, LogsDirectoryName);

    public string SettingsPath => Path.Combine(Root, SettingsFileName);

    /// <summary>The default location under Local Application Data.</summary>
    public static WallpaperStorage MakeDefault()
    {
        try
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var storage = new WallpaperStorage(Path.Combine(localAppData, "Wallpache"));
            storage.PrepareDirectories();
            return storage;
        }
        catch (Exception error)
        {
            throw WallpaperException.StorageUnavailable(error.Message);
        }
    }

    public void PrepareDirectories()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(VideosDirectory);
        Directory.CreateDirectory(ThumbnailsDirectory);
        Directory.CreateDirectory(StillsDirectory);
        Directory.CreateDirectory(LogsDirectory);
    }

    public string PathForRelative(string relativePath) =>
        Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));

    public string VideoPath(WallpaperRecord record) => PathForRelative(record.RelativePath);

    public string? ThumbnailPath(WallpaperRecord record) =>
        record.ThumbnailRelativePath is null ? null : PathForRelative(record.ThumbnailRelativePath);

    /// <summary>
    /// The full-resolution still. Records imported before stills existed have
    /// none, so callers fall back to the thumbnail.
    /// </summary>
    public string? StillPath(WallpaperRecord record) =>
        record.StillRelativePath is null ? null : PathForRelative(record.StillRelativePath);

    /// <summary>The best available still, preferring full resolution.</summary>
    public string? PreviewImagePath(WallpaperRecord record)
    {
        var still = StillPath(record);
        if (still is not null && File.Exists(still))
        {
            return still;
        }

        var thumbnail = ThumbnailPath(record);
        return thumbnail is not null && File.Exists(thumbnail) ? thumbnail : null;
    }

    public static string RelativePath(string directory, string fileName) => $"{directory}/{fileName}";
}
