using System;
using System.IO;
using System.Threading.Tasks;
using Wallpache.App.Support;
using Windows.Graphics.Imaging;
using Windows.Media.Editing;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Wallpache.App.Library;

/// <summary>
/// Renders still frames from an imported video.
///
/// Two sizes are produced per import, because one file cannot serve both jobs:
///
/// <list type="bullet">
/// <item>A <b>still</b> near the video's own resolution, for the preview window
/// and for use as the Windows desktop picture. Anything smaller is visibly soft
/// the moment it is scaled up to a display.</item>
/// <item>A <b>thumbnail</b> at grid size, so a library of 4K wallpapers does not
/// load tens of megabytes of pixels just to draw a row of cells.</item>
/// </list>
///
/// Both are generated once at import time; nothing regenerates them while a
/// wallpaper is playing.
/// </summary>
public static class ThumbnailGenerator
{
    /// <summary>Grid size. Ample for the largest library cell.</summary>
    public const int ThumbnailPixelWidth = 640;

    /// <summary>Full size, bounded so an 8K source cannot produce an enormous PNG.</summary>
    public const int StillPixelWidth = 3840;

    /// <summary>
    /// Writes a PNG preview and returns <see langword="true"/> on success. A
    /// missing image is cosmetic, so failures are logged rather than thrown.
    /// </summary>
    public static async Task<bool> GenerateAsync(
        string videoPath,
        string destinationPath,
        double duration,
        int? sourceWidth,
        int? sourceHeight,
        int maximumPixelWidth)
    {
        var bytes = await FrameAsync(videoPath, duration, sourceWidth, sourceHeight, maximumPixelWidth);
        if (bytes is null)
        {
            return false;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            await File.WriteAllBytesAsync(destinationPath, bytes);
            return true;
        }
        catch (Exception error)
        {
            Log.Library.Error($"Thumbnail write failed for {Path.GetFileName(videoPath)}: {error.Message}");
            return false;
        }
    }

    /// <summary>
    /// Encodes one representative frame as PNG without writing it anywhere.
    /// Used to preview a file that has not been copied into storage yet.
    /// </summary>
    public static async Task<byte[]?> FrameAsync(
        string videoPath,
        double duration,
        int? sourceWidth,
        int? sourceHeight,
        int maximumPixelWidth)
    {
        try
        {
            var file = await StorageFile.GetFileFromPathAsync(videoPath);
            var clip = await MediaClip.CreateFromFileAsync(file);

            var composition = new MediaComposition();
            composition.Clips.Add(clip);

            var (width, height) = TargetSize(sourceWidth, sourceHeight, maximumPixelWidth);

            // Prefer a frame just inside the video; the very first frame is often
            // black on fade-in clips.
            var seconds = Math.Clamp(duration * 0.1, 0, Math.Max(duration - 0.1, 0));

            using var frame = await composition.GetThumbnailAsync(
                TimeSpan.FromSeconds(seconds),
                width,
                height,
                VideoFramePrecision.NearestFrame);

            return await EncodePngAsync(frame);
        }
        catch (Exception error)
        {
            Log.Library.Error($"Thumbnail generation failed for {Path.GetFileName(videoPath)}: {error.Message}");
            return null;
        }
    }

    /// <summary>
    /// Scales the longest edge down to <paramref name="maximumPixelWidth"/>
    /// while preserving the aspect ratio. Zero means "the clip's own size".
    /// </summary>
    private static (int Width, int Height) TargetSize(int? sourceWidth, int? sourceHeight, int maximumPixelWidth)
    {
        if (sourceWidth is not > 0 || sourceHeight is not > 0)
        {
            return (maximumPixelWidth, 0);
        }

        var longest = Math.Max(sourceWidth.Value, sourceHeight.Value);
        if (longest <= maximumPixelWidth)
        {
            return (sourceWidth.Value, sourceHeight.Value);
        }

        var scale = (double)maximumPixelWidth / longest;
        return (
            Math.Max((int)Math.Round(sourceWidth.Value * scale), 1),
            Math.Max((int)Math.Round(sourceHeight.Value * scale), 1));
    }

    /// <summary>
    /// Transcodes the frame to PNG in memory. Going through a byte array rather
    /// than a <c>StorageFile</c> keeps the write on plain file APIs, which is
    /// what every other part of the storage layer uses.
    /// </summary>
    private static async Task<byte[]?> EncodePngAsync(IRandomAccessStream frame)
    {
        var decoder = await BitmapDecoder.CreateAsync(frame);
        using var bitmap = await decoder.GetSoftwareBitmapAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied);

        using var memory = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, memory);
        encoder.SetSoftwareBitmap(bitmap);
        await encoder.FlushAsync();

        var size = (uint)memory.Size;
        if (size == 0)
        {
            return null;
        }

        var bytes = new byte[size];
        using var reader = new DataReader(memory.GetInputStreamAt(0));
        await reader.LoadAsync(size);
        reader.ReadBytes(bytes);
        return bytes;
    }
}
