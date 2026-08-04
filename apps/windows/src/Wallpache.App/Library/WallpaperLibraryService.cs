using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Wallpache.App.Support;

namespace Wallpache.App.Library;

/// <summary>
/// Imports, validates, and removes local wallpapers.
///
/// Imported files are copied into app-controlled storage. That keeps a
/// wallpaper working after the source is moved, renamed, or unplugged with an
/// external drive.
/// </summary>
public sealed class WallpaperLibraryService
{
    /// <summary>Container extensions accepted at import time.</summary>
    public static readonly string[] SupportedExtensions = [".mp4", ".mov", ".m4v"];

    public WallpaperLibraryService(WallpaperStorage storage)
    {
        Storage = storage;
    }

    public WallpaperStorage Storage { get; }

    /// <summary>
    /// Validates <paramref name="sourcePath"/>, copies it into storage, and
    /// returns the new record. <paramref name="existing"/> is used to skip
    /// re-importing the same file twice.
    /// </summary>
    public async Task<WallpaperRecord> ImportAsync(string sourcePath, IReadOnlyList<WallpaperRecord> existing)
    {
        var name = Path.GetFileName(sourcePath);
        var extension = Path.GetExtension(sourcePath).ToLowerInvariant();

        if (!SupportedExtensions.Contains(extension))
        {
            throw WallpaperException.NotPlayable(name);
        }

        // Validate before copying so an unusable file never enters storage.
        var metadata = await VideoMetadataReader.ReadAsync(sourcePath);
        var sourceSize = FileSize(sourcePath);

        var duplicate = existing.FirstOrDefault(record => IsDuplicate(record, sourcePath, sourceSize));
        if (duplicate is not null)
        {
            Log.Library.Info($"Skipping duplicate import of {name}");
            return duplicate;
        }

        var id = Guid.NewGuid();
        var relativePath = WallpaperStorage.RelativePath(
            WallpaperStorage.VideosDirectoryName,
            $"{id:D}{extension}");
        var destination = Storage.PathForRelative(relativePath);

        try
        {
            Storage.PrepareDirectories();
            File.Copy(sourcePath, destination, overwrite: false);
        }
        catch (Exception error)
        {
            throw WallpaperException.ImportFailed(name, error.Message);
        }

        var thumbnailPath = await MakeImageAsync(
            id,
            destination,
            metadata,
            WallpaperStorage.ThumbnailsDirectoryName,
            ThumbnailGenerator.ThumbnailPixelWidth);

        var stillPath = await MakeImageAsync(
            id,
            destination,
            metadata,
            WallpaperStorage.StillsDirectoryName,
            ThumbnailGenerator.StillPixelWidth);

        return new WallpaperRecord
        {
            Id = id,
            Name = Path.GetFileNameWithoutExtension(sourcePath),
            RelativePath = relativePath,
            ThumbnailRelativePath = thumbnailPath,
            StillRelativePath = stillPath,
            DateImported = DateTimeOffset.Now,
            Duration = metadata.Duration,
            Width = metadata.Width,
            Height = metadata.Height,
            FileSize = FileSize(destination),
            SourceFileName = name
        };
    }

    /// <summary>
    /// Deletes the imported copy and both generated images. Missing files are
    /// ignored: removing a record must always succeed from the user's point of
    /// view.
    /// </summary>
    public void Delete(WallpaperRecord record)
    {
        foreach (var path in new[] { Storage.VideoPath(record), Storage.ThumbnailPath(record), Storage.StillPath(record) })
        {
            if (path is null)
            {
                continue;
            }

            try
            {
                File.Delete(path);
            }
            catch (Exception error)
            {
                Log.Library.Error($"Could not delete {Path.GetFileName(path)}: {error.Message}");
            }
        }
    }

    /// <summary>
    /// Generates the full-resolution still for entries imported before stills
    /// existed, and returns the records that gained one so the caller can
    /// persist them. Entries whose video has gone are skipped.
    /// </summary>
    public async Task<IReadOnlyList<WallpaperRecord>> BackfillStillsAsync(IReadOnlyList<WallpaperRecord> records)
    {
        var updated = new List<WallpaperRecord>();

        foreach (var record in records.Where(record => record.StillRelativePath is null))
        {
            if (!FileExists(record))
            {
                continue;
            }

            var path = await MakeImageAsync(
                record.Id,
                Storage.VideoPath(record),
                new VideoMetadata(record.Duration, record.Width, record.Height),
                WallpaperStorage.StillsDirectoryName,
                ThumbnailGenerator.StillPixelWidth);

            if (path is null)
            {
                continue;
            }

            record.StillRelativePath = path;
            updated.Add(record);
        }

        return updated;
    }

    public bool FileExists(WallpaperRecord record) => File.Exists(Storage.VideoPath(record));

    /// <summary>
    /// Removes stored files that no record points at, e.g. after a crash between
    /// the copy and the configuration save.
    /// </summary>
    public void RemoveOrphanedFiles(IReadOnlyList<WallpaperRecord> records)
    {
        var keep = new HashSet<string>(
            records
                .SelectMany(record => new[] { record.RelativePath, record.ThumbnailRelativePath, record.StillRelativePath })
                .Where(path => !string.IsNullOrEmpty(path))
                .Select(path => Path.GetFileName(path!)),
            StringComparer.OrdinalIgnoreCase);

        foreach (var directory in new[] { Storage.VideosDirectory, Storage.ThumbnailsDirectory, Storage.StillsDirectory })
        {
            if (!Directory.Exists(directory))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(directory))
            {
                if (keep.Contains(Path.GetFileName(file)))
                {
                    continue;
                }

                try
                {
                    File.Delete(file);
                }
                catch (Exception error)
                {
                    Log.Library.Error($"Could not remove orphan {Path.GetFileName(file)}: {error.Message}");
                }
            }
        }
    }

    // MARK: - Private

    private async Task<string?> MakeImageAsync(
        Guid id,
        string videoPath,
        VideoMetadata metadata,
        string directory,
        int maximumPixelWidth)
    {
        var relativePath = WallpaperStorage.RelativePath(directory, $"{id:D}.png");
        var destination = Storage.PathForRelative(relativePath);

        var generated = await ThumbnailGenerator.GenerateAsync(
            videoPath,
            destination,
            metadata.Duration,
            metadata.Width,
            metadata.Height,
            maximumPixelWidth);

        return generated ? relativePath : null;
    }

    /// <summary>
    /// A repeat import is recognised by identical source file name and byte
    /// size. Records imported before renaming existed have no stored source
    /// name, so those fall back to the display name they were created with.
    /// </summary>
    private bool IsDuplicate(WallpaperRecord record, string sourcePath, long size)
    {
        var matchesName = record.SourceFileName is not null
            ? string.Equals(record.SourceFileName, Path.GetFileName(sourcePath), StringComparison.OrdinalIgnoreCase)
            : string.Equals(record.Name, Path.GetFileNameWithoutExtension(sourcePath), StringComparison.OrdinalIgnoreCase);

        return matchesName && record.FileSize == size && size > 0 && FileExists(record);
    }

    private static long FileSize(string path)
    {
        try
        {
            return new FileInfo(path).Length;
        }
        catch (Exception)
        {
            return 0;
        }
    }
}
