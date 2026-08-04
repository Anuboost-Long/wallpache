using System;
using System.Text.Json.Serialization;

namespace Wallpache.App.Library;

/// <summary>
/// One imported video in the local library. Paths are stored relative to the
/// storage root so the library keeps working if the container path changes.
/// </summary>
public sealed class WallpaperRecord
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("relativePath")]
    public string RelativePath { get; set; } = string.Empty;

    /// <summary>Grid-size preview.</summary>
    [JsonPropertyName("thumbnailRelativePath")]
    public string? ThumbnailRelativePath { get; set; }

    /// <summary>
    /// Full-resolution still, used for the preview window and as the Windows
    /// desktop picture. Absent for entries imported before stills existed; the
    /// library backfills those on launch.
    /// </summary>
    [JsonPropertyName("stillRelativePath")]
    public string? StillRelativePath { get; set; }

    [JsonPropertyName("dateImported")]
    public DateTimeOffset DateImported { get; set; } = DateTimeOffset.Now;

    [JsonPropertyName("duration")]
    public double Duration { get; set; }

    [JsonPropertyName("width")]
    public int? Width { get; set; }

    [JsonPropertyName("height")]
    public int? Height { get; set; }

    /// <summary>Byte size of the imported copy, used to recognise a repeated import.</summary>
    [JsonPropertyName("fileSize")]
    public long FileSize { get; set; }

    /// <summary>
    /// The file name this was imported from. Duplicate detection uses it rather
    /// than <see cref="Name"/>, so renaming an entry cannot cause a re-import to
    /// be copied in a second time.
    /// </summary>
    [JsonPropertyName("sourceFileName")]
    public string? SourceFileName { get; set; }

    [JsonIgnore]
    public string DurationDescription
    {
        get
        {
            var total = (int)Math.Round(Duration);
            return $"{total / 60}:{total % 60:00}";
        }
    }

    [JsonIgnore]
    public string? ResolutionDescription =>
        Width is > 0 && Height is > 0 ? $"{Width} × {Height}" : null;

    [JsonIgnore]
    public string Subtitle => ResolutionDescription is null
        ? DurationDescription
        : $"{DurationDescription} · {ResolutionDescription}";
}
