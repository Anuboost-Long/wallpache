using Wallpache.App.Library;
using Xunit;

namespace Wallpache.Tests;

/// <summary>
/// The storage side of the library: layout, deletion, and orphan cleanup.
///
/// Import itself is not covered here — it decodes the file through WinRT, which
/// only exists on Windows — so the checklist covers it on real hardware.
/// </summary>
public class WallpaperLibraryServiceTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "WallpacheTests", Guid.NewGuid().ToString("N"));

    private readonly WallpaperStorage _storage;
    private readonly WallpaperLibraryService _service;

    public WallpaperLibraryServiceTests()
    {
        _storage = new WallpaperStorage(_root);
        _storage.PrepareDirectories();
        _service = new WallpaperLibraryService(_storage);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp folder is not worth failing a test over.
        }

        GC.SuppressFinalize(this);
    }

    private WallpaperRecord MakeRecord(string name = "Clip")
    {
        var id = Guid.NewGuid();
        var record = new WallpaperRecord
        {
            Id = id,
            Name = name,
            RelativePath = WallpaperStorage.RelativePath(WallpaperStorage.VideosDirectoryName, $"{id:D}.mp4"),
            ThumbnailRelativePath = WallpaperStorage.RelativePath(WallpaperStorage.ThumbnailsDirectoryName, $"{id:D}.png"),
            StillRelativePath = WallpaperStorage.RelativePath(WallpaperStorage.StillsDirectoryName, $"{id:D}.png")
        };

        File.WriteAllText(_storage.VideoPath(record), "video");
        File.WriteAllText(_storage.ThumbnailPath(record)!, "thumb");
        File.WriteAllText(_storage.StillPath(record)!, "still");
        return record;
    }

    [Fact]
    public void StorageLaysFilesOutUnderTheDocumentedFolders()
    {
        var record = MakeRecord();

        Assert.Equal(Path.Combine(_root, "Wallpapers"), _storage.VideosDirectory);
        Assert.Equal(Path.Combine(_root, "Thumbnails"), _storage.ThumbnailsDirectory);
        Assert.Equal(Path.Combine(_root, "Stills"), _storage.StillsDirectory);
        Assert.Equal(Path.Combine(_root, "settings.json"), _storage.SettingsPath);
        Assert.StartsWith(_storage.VideosDirectory, _storage.VideoPath(record));
    }

    [Fact]
    public void PreviewImagePrefersTheFullResolutionStill()
    {
        var record = MakeRecord();

        Assert.Equal(_storage.StillPath(record), _storage.PreviewImagePath(record));

        File.Delete(_storage.StillPath(record)!);
        Assert.Equal(_storage.ThumbnailPath(record), _storage.PreviewImagePath(record));

        File.Delete(_storage.ThumbnailPath(record)!);
        Assert.Null(_storage.PreviewImagePath(record));
    }

    [Fact]
    public void DeleteRemovesTheVideoAndBothImages()
    {
        var record = MakeRecord();

        _service.Delete(record);

        Assert.False(File.Exists(_storage.VideoPath(record)));
        Assert.False(File.Exists(_storage.ThumbnailPath(record)!));
        Assert.False(File.Exists(_storage.StillPath(record)!));
    }

    [Fact]
    public void DeleteSucceedsWhenTheFilesAreAlreadyGone()
    {
        var record = MakeRecord();
        _service.Delete(record);

        // Removing a record must always succeed from the user's point of view.
        _service.Delete(record);

        Assert.False(_service.FileExists(record));
    }

    [Fact]
    public void FileExistsFollowsTheVideoRatherThanTheRecord()
    {
        var record = MakeRecord();
        Assert.True(_service.FileExists(record));

        File.Delete(_storage.VideoPath(record));
        Assert.False(_service.FileExists(record));
    }

    [Fact]
    public void OrphanCleanupKeepsEveryFileAKeptRecordPointsAt()
    {
        var kept = MakeRecord("Kept");
        var abandoned = MakeRecord("Abandoned");

        _service.RemoveOrphanedFiles([kept]);

        Assert.True(File.Exists(_storage.VideoPath(kept)));
        Assert.True(File.Exists(_storage.ThumbnailPath(kept)!));
        Assert.True(File.Exists(_storage.StillPath(kept)!));

        Assert.False(File.Exists(_storage.VideoPath(abandoned)));
        Assert.False(File.Exists(_storage.ThumbnailPath(abandoned)!));
        Assert.False(File.Exists(_storage.StillPath(abandoned)!));
    }

    [Fact]
    public void OrphanCleanupWithAnEmptyLibraryClearsStorage()
    {
        MakeRecord();

        _service.RemoveOrphanedFiles([]);

        Assert.Empty(Directory.GetFiles(_storage.VideosDirectory));
        Assert.Empty(Directory.GetFiles(_storage.ThumbnailsDirectory));
        Assert.Empty(Directory.GetFiles(_storage.StillsDirectory));
    }

    [Fact]
    public void RecordFormatsItsOwnSubtitle()
    {
        var record = new WallpaperRecord { Duration = 75, Width = 1920, Height = 1080 };

        Assert.Equal("1:15", record.DurationDescription);
        Assert.Equal("1920 × 1080", record.ResolutionDescription);
        Assert.Equal("1:15 · 1920 × 1080", record.Subtitle);
    }

    [Fact]
    public void RecordWithoutDimensionsFallsBackToDurationAlone()
    {
        var record = new WallpaperRecord { Duration = 5 };

        Assert.Null(record.ResolutionDescription);
        Assert.Equal("0:05", record.Subtitle);
    }
}
