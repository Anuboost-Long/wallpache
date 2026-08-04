using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Wallpache.App.Support;

namespace Wallpache.App.UI;

/// <summary>
/// Decodes wallpaper stills off the UI thread.
///
/// Decoding is capped at a maximum width so that pointing this at a
/// full-resolution still costs what the view draws rather than the whole 4K
/// frame.
/// </summary>
public static class ThumbnailLoader
{
    /// <summary>Longest edge to decode for a library cell, in pixels.</summary>
    public const int CellPixelWidth = 640;

    /// <summary>Shown at 640 points, so twice that on a high-DPI display.</summary>
    public const int PreviewPixelWidth = 1280;

    public static async Task<Bitmap?> LoadAsync(string? path, int maximumPixelWidth)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        try
        {
            return await Task.Run(() =>
            {
                if (!File.Exists(path))
                {
                    return null;
                }

                using var stream = File.OpenRead(path);
                return Bitmap.DecodeToWidth(stream, maximumPixelWidth);
            });
        }
        catch (Exception error)
        {
            // A missing preview is cosmetic; the cell falls back to its placeholder.
            Log.Library.Error($"Could not load preview {Path.GetFileName(path)}: {error.Message}");
            return null;
        }
    }
}
