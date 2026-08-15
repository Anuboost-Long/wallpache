using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Wallpache.App.Support;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Media.Editing;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;

namespace Wallpache.App.Library;

/// <summary>
/// Converts an animated GIF into an H.264 file at import time.
///
/// Nothing downstream understands GIF: playback goes through Media Foundation,
/// which has no GIF source at all, and the thumbnail and still both come out of
/// a media clip. Converting once on the way in leaves every one of those paths
/// untouched, and the library ends up holding a file that behaves like any
/// other wallpaper.
///
/// WIC hands back each GIF frame as the sub-rectangle the encoder stored, not
/// as the picture the user sees, so the frames are composited onto a canvas here
/// exactly as a viewer would draw them.
/// </summary>
public static class GifTranscoder
{
    /// <summary>What a converted GIF is stored as.</summary>
    public const string TranscodedFileExtension = ".mp4";

    /// <summary>
    /// Countless GIFs declare a delay of 0 or 1 hundredths. Browsers show those
    /// at 100ms, so matching that is what makes a converted GIF run at the speed
    /// the user is used to seeing.
    /// </summary>
    private const double ShortestHonouredDelay = 0.02;
    private const double SubstituteDelay = 0.1;

    /// <summary>GIF disposal methods, from the graphic control extension.</summary>
    private const uint RestoreToBackground = 2;
    private const uint RestoreToPrevious = 3;

    private const string DelayProperty = "/grctlext/Delay";
    private const string DisposalProperty = "/grctlext/Disposal";
    private const string LeftProperty = "/imgdesc/Left";
    private const string TopProperty = "/imgdesc/Top";
    private const string ScreenWidthProperty = "/logscrdesc/Width";
    private const string ScreenHeightProperty = "/logscrdesc/Height";

    public static bool IsGif(string path) =>
        string.Equals(Path.GetExtension(path), ".gif", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Frame timing and size, without composing or converting anything. Used to
    /// describe a file in the import tray while it waits for confirmation.
    /// </summary>
    public static async Task<VideoMetadata> InspectAsync(string path)
    {
        var name = Path.GetFileName(path);

        if (!File.Exists(path))
        {
            throw WallpaperException.FileMissing(name);
        }

        var decoder = await OpenAsync(path, name);
        var (width, height) = await CanvasSizeAsync(decoder);

        if (decoder.FrameCount == 0)
        {
            throw WallpaperException.NoVideoTrack(name);
        }

        var duration = 0.0;
        for (uint index = 0; index < decoder.FrameCount; index++)
        {
            duration += await FrameDelayAsync(decoder, index);
        }

        if (duration <= 0)
        {
            throw WallpaperException.EmptyDuration(name);
        }

        return new VideoMetadata(duration, width, height);
    }

    /// <summary>
    /// Writes <paramref name="sourcePath"/> to <paramref name="destinationPath"/>
    /// as H.264 and reports what it wrote.
    ///
    /// The source is validated first, so a GIF that cannot be read leaves
    /// nothing behind, and a failure part way through removes the half-written
    /// file: an unplayable one must never enter the library.
    /// </summary>
    public static async Task<VideoMetadata> TranscodeAsync(string sourcePath, string destinationPath)
    {
        var name = Path.GetFileName(sourcePath);
        var metadata = await InspectAsync(sourcePath);

        var scratch = Path.Combine(Path.GetTempPath(), "Wallpache", $"gif-{Guid.NewGuid():N}");
        Directory.CreateDirectory(scratch);

        try
        {
            var frames = await ComposeFramesAsync(sourcePath, scratch, name, metadata);
            await RenderAsync(frames, destinationPath, metadata);

            Log.Library.Info($"Converted a GIF of {frames.Count} frames");
            return metadata;
        }
        catch (WallpaperException)
        {
            Delete(destinationPath);
            throw;
        }
        catch (Exception error)
        {
            Delete(destinationPath);
            throw WallpaperException.ImportFailed(name, error.Message);
        }
        finally
        {
            try
            {
                Directory.Delete(scratch, recursive: true);
            }
            catch (IOException)
            {
                // A leftover scratch folder is not worth failing an import over.
            }
        }
    }

    // MARK: - Frames

    /// <summary>
    /// Draws every frame onto a running canvas and writes each result as a PNG.
    /// Going through files rather than memory keeps a long GIF from costing its
    /// whole decoded size at once.
    /// </summary>
    private static async Task<List<(StorageFile File, TimeSpan Delay)>> ComposeFramesAsync(
        string sourcePath,
        string scratchDirectory,
        string name,
        VideoMetadata metadata)
    {
        var width = metadata.Width!.Value;
        var height = metadata.Height!.Value;

        var decoder = await OpenAsync(sourcePath, name);
        var folder = await StorageFolder.GetFolderFromPathAsync(scratchDirectory);

        var canvas = new byte[width * height * 4];
        var frames = new List<(StorageFile, TimeSpan)>();

        for (uint index = 0; index < decoder.FrameCount; index++)
        {
            var frame = await decoder.GetFrameAsync(index);
            var properties = await ReadPropertiesAsync(frame.BitmapProperties, [LeftProperty, TopProperty, DisposalProperty]);

            var left = (int)Number(properties, LeftProperty, 0);
            var top = (int)Number(properties, TopProperty, 0);
            var disposal = Number(properties, DisposalProperty, 0);

            var restorePoint = disposal == RestoreToPrevious ? (byte[])canvas.Clone() : null;

            var pixels = await frame.GetPixelDataAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Straight,
                new BitmapTransform(),
                ExifOrientationMode.IgnoreExifOrientation,
                ColorManagementMode.DoNotColorManage);

            Draw(
                canvas,
                width,
                height,
                pixels.DetachPixelData(),
                (int)frame.PixelWidth,
                (int)frame.PixelHeight,
                left,
                top);

            var file = await folder.CreateFileAsync($"{index:D5}.png", CreationCollisionOption.ReplaceExisting);
            await WritePngAsync(file, canvas, width, height);
            frames.Add((file, TimeSpan.FromSeconds(await FrameDelayAsync(frame))));

            if (disposal == RestoreToBackground)
            {
                Clear(canvas, width, height, left, top, (int)frame.PixelWidth, (int)frame.PixelHeight);
            }
            else if (restorePoint is not null)
            {
                canvas = restorePoint;
            }
        }

        if (frames.Count == 0)
        {
            throw WallpaperException.NoVideoTrack(name);
        }

        return frames;
    }

    /// <summary>
    /// Composites one frame's pixels over the canvas. GIF transparency is all or
    /// nothing, so a transparent source pixel simply leaves the canvas alone.
    /// </summary>
    private static void Draw(
        byte[] canvas,
        int canvasWidth,
        int canvasHeight,
        byte[] pixels,
        int frameWidth,
        int frameHeight,
        int left,
        int top)
    {
        for (var y = 0; y < frameHeight; y++)
        {
            var canvasY = top + y;
            if (canvasY < 0 || canvasY >= canvasHeight)
            {
                continue;
            }

            for (var x = 0; x < frameWidth; x++)
            {
                var canvasX = left + x;
                if (canvasX < 0 || canvasX >= canvasWidth)
                {
                    continue;
                }

                var source = ((y * frameWidth) + x) * 4;
                if (pixels[source + 3] == 0)
                {
                    continue;
                }

                var target = ((canvasY * canvasWidth) + canvasX) * 4;
                canvas[target] = pixels[source];
                canvas[target + 1] = pixels[source + 1];
                canvas[target + 2] = pixels[source + 2];
                canvas[target + 3] = 255;
            }
        }
    }

    private static void Clear(
        byte[] canvas,
        int canvasWidth,
        int canvasHeight,
        int left,
        int top,
        int frameWidth,
        int frameHeight)
    {
        for (var y = top; y < Math.Min(top + frameHeight, canvasHeight); y++)
        {
            for (var x = left; x < Math.Min(left + frameWidth, canvasWidth); x++)
            {
                if (x < 0 || y < 0)
                {
                    continue;
                }

                Array.Clear(canvas, ((y * canvasWidth) + x) * 4, 4);
            }
        }
    }

    // MARK: - Encoding

    /// <summary>
    /// Renders the frames as one clip each, at the duration the GIF asked for.
    /// The output frame rate follows the shortest frame, so a fast GIF does not
    /// lose frames to a slower encode.
    /// </summary>
    private static async Task RenderAsync(
        IReadOnlyList<(StorageFile File, TimeSpan Delay)> frames,
        string destinationPath,
        VideoMetadata metadata)
    {
        var composition = new MediaComposition();
        var shortest = double.MaxValue;

        foreach (var (file, delay) in frames)
        {
            composition.Clips.Add(await MediaClip.CreateFromImageFileAsync(file, delay));
            shortest = Math.Min(shortest, delay.TotalSeconds);
        }

        var profile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.HD720p);
        profile.Audio = null;
        profile.Video.Width = (uint)metadata.Width!.Value;
        profile.Video.Height = (uint)metadata.Height!.Value;
        profile.Video.FrameRate.Numerator = (uint)Math.Clamp(Math.Round(1 / shortest), 10, 30);
        profile.Video.FrameRate.Denominator = 1;

        var directory = Path.GetDirectoryName(destinationPath)!;
        var folder = await StorageFolder.GetFolderFromPathAsync(directory);
        var destination = await folder.CreateFileAsync(
            Path.GetFileName(destinationPath),
            CreationCollisionOption.ReplaceExisting);

        await composition.RenderToFileAsync(destination, MediaTrimmingPreference.Precise, profile);
    }

    private static async Task WritePngAsync(StorageFile file, byte[] bgra, int width, int height)
    {
        using var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);

        encoder.SetPixelData(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Ignore,
            (uint)width,
            (uint)height,
            96,
            96,
            bgra);

        await encoder.FlushAsync();
    }

    // MARK: - Reading

    private static async Task<BitmapDecoder> OpenAsync(string path, string name)
    {
        try
        {
            var file = await StorageFile.GetFileFromPathAsync(path);
            using var stream = await file.OpenReadAsync();
            return await BitmapDecoder.CreateAsync(BitmapDecoder.GifDecoderId, stream);
        }
        catch (Exception)
        {
            throw WallpaperException.NotPlayable(name);
        }
    }

    /// <summary>
    /// The logical screen the frames are drawn onto, trimmed to even sides:
    /// H.264 encodes over a 4:2:0 grid and cannot take odd dimensions.
    /// </summary>
    private static async Task<(int Width, int Height)> CanvasSizeAsync(BitmapDecoder decoder)
    {
        var properties = await ReadPropertiesAsync(
            decoder.BitmapContainerProperties,
            [ScreenWidthProperty, ScreenHeightProperty]);

        var width = (int)Number(properties, ScreenWidthProperty, decoder.PixelWidth);
        var height = (int)Number(properties, ScreenHeightProperty, decoder.PixelHeight);

        return (Even(width), Even(height));
    }

    private static async Task<double> FrameDelayAsync(BitmapDecoder decoder, uint index) =>
        await FrameDelayAsync(await decoder.GetFrameAsync(index));

    /// <summary>
    /// Asked for on its own rather than alongside the frame's placement: a frame
    /// with no graphic control extension has no delay, and a combined request
    /// would lose the placement with it.
    /// </summary>
    private static async Task<double> FrameDelayAsync(BitmapFrame frame)
    {
        var properties = await ReadPropertiesAsync(frame.BitmapProperties, [DelayProperty]);

        // GIF stores the delay in hundredths of a second.
        var declared = Number(properties, DelayProperty, 0) / 100.0;
        return declared < ShortestHonouredDelay ? SubstituteDelay : declared;
    }

    /// <summary>
    /// A frame that carries none of the requested metadata throws rather than
    /// returning an empty set, and every one of these values has a sane default,
    /// so a miss is not worth failing an import over.
    /// </summary>
    private static async Task<IDictionary<string, BitmapTypedValue>> ReadPropertiesAsync(
        BitmapPropertiesView view,
        IReadOnlyList<string> keys)
    {
        try
        {
            return await view.GetPropertiesAsync(keys);
        }
        catch (Exception)
        {
            return new Dictionary<string, BitmapTypedValue>();
        }
    }

    private static uint Number(IDictionary<string, BitmapTypedValue> properties, string key, uint fallback)
    {
        try
        {
            return properties.TryGetValue(key, out var value) && value.Value is not null
                ? Convert.ToUInt32(value.Value)
                : fallback;
        }
        catch (Exception)
        {
            return fallback;
        }
    }

    private static int Even(int value) => Math.Max(value / 2 * 2, 2);

    private static void Delete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception error)
        {
            Log.Library.Error($"Could not remove a half-written conversion: {error.Message}");
        }
    }
}
