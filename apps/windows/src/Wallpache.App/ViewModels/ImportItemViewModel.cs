using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Wallpache.App.Library;
using Wallpache.App.Support;
using Wallpache.App.UI;

namespace Wallpache.App.ViewModels;

/// <summary>One staged file: its preview frame, what it is, and how far it has got.</summary>
public sealed partial class ImportItemViewModel : ViewModelBase
{
    public enum ImportState
    {
        Inspecting,

        Ready,

        /// <summary>The file cannot be imported at all, e.g. a wrong format.</summary>
        Rejected,

        Importing,

        Imported,

        Failed
    }

    public ImportItemViewModel(string sourcePath)
    {
        SourcePath = sourcePath;
        Name = Path.GetFileNameWithoutExtension(sourcePath);
    }

    public string SourcePath { get; }

    public string Name { get; }

    [ObservableProperty]
    public partial Bitmap? Preview { get; private set; }

    [ObservableProperty]
    public partial string Subtitle { get; private set; } = "Checking…";

    [ObservableProperty]
    public partial ImportState State { get; private set; } = ImportState.Inspecting;

    public bool IsReady => State == ImportState.Ready;

    public bool IsImporting => State == ImportState.Importing;

    public bool IsImported => State == ImportState.Imported;

    public bool HasProblem => State is ImportState.Rejected or ImportState.Failed;

    /// <summary>Inspection and the copy itself both show a spinner.</summary>
    public bool IsBusy => State is ImportState.Inspecting or ImportState.Importing;

    /// <summary>
    /// Removing is offered until the copy starts; after that the item has to
    /// finish, and it leaves the tray on its own.
    /// </summary>
    public bool CanRemove => State is not (ImportState.Importing or ImportState.Imported);

    /// <summary>
    /// Validates the file and pulls a preview frame, without copying anything.
    /// The same validation runs again at import time, so a file that becomes
    /// unusable in between is still caught.
    /// </summary>
    public async Task InspectAsync()
    {
        var name = Path.GetFileName(SourcePath);
        var extension = Path.GetExtension(SourcePath).ToLowerInvariant();

        if (!WallpaperLibraryService.SupportedExtensions.Contains(extension))
        {
            MarkRejected(WallpaperException.NotPlayable(name).Message);
            return;
        }

        try
        {
            // A GIF has no media source to read, and it is only converted once
            // the user confirms, so it is measured and previewed as an image.
            if (GifTranscoder.IsGif(SourcePath))
            {
                var gif = await GifTranscoder.InspectAsync(SourcePath);

                Preview = await ThumbnailLoader.LoadAsync(SourcePath, ThumbnailLoader.CellPixelWidth);
                Subtitle = Describe(gif);
                State = ImportState.Ready;
                return;
            }

            var metadata = await VideoMetadataReader.ReadAsync(SourcePath);
            var frame = await ThumbnailGenerator.FrameAsync(
                SourcePath,
                metadata.Duration,
                metadata.Width,
                metadata.Height,
                ThumbnailGenerator.ThumbnailPixelWidth);

            Preview = Decode(frame);
            Subtitle = Describe(metadata);
            State = ImportState.Ready;
        }
        catch (Exception error)
        {
            MarkRejected(error.Message);
        }
    }

    public void MarkImporting()
    {
        Subtitle = "Importing…";
        State = ImportState.Importing;
    }

    public void MarkImported()
    {
        Subtitle = "Imported";
        State = ImportState.Imported;
    }

    public void MarkFailed(string reason)
    {
        Subtitle = reason;
        State = ImportState.Failed;
    }

    // MARK: - Private

    private void MarkRejected(string reason)
    {
        Subtitle = reason;
        State = ImportState.Rejected;
    }

    partial void OnStateChanged(ImportState value)
    {
        OnPropertyChanged(nameof(IsReady));
        OnPropertyChanged(nameof(IsImporting));
        OnPropertyChanged(nameof(IsImported));
        OnPropertyChanged(nameof(HasProblem));
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(CanRemove));
    }

    private static Bitmap? Decode(byte[]? frame)
    {
        if (frame is null)
        {
            return null;
        }

        try
        {
            using var stream = new MemoryStream(frame);
            return new Bitmap(stream);
        }
        catch (Exception error)
        {
            // A missing preview is cosmetic; the card falls back to its placeholder.
            Log.Library.Error($"Could not decode a staged preview: {error.Message}");
            return null;
        }
    }

    private static string Describe(VideoMetadata metadata)
    {
        var total = (int)Math.Round(metadata.Duration);
        var duration = $"{total / 60}:{total % 60:00}";

        return metadata.Width is > 0 && metadata.Height is > 0
            ? $"{duration} · {metadata.Width} × {metadata.Height}"
            : duration;
    }
}
