using System;

namespace Wallpache.App.Support;

/// <summary>
/// Errors surfaced to the user. Every case describes a recoverable situation:
/// the app falls back to the plain Windows wallpaper rather than crashing.
/// </summary>
public sealed class WallpaperException : Exception
{
    private WallpaperException(string message, string? recoverySuggestion)
        : base(message)
    {
        RecoverySuggestion = recoverySuggestion;
    }

    public string? RecoverySuggestion { get; }

    private const string FormatSuggestion = "Try an H.264 .mp4, .mov, or .m4v file, or an animated .gif.";

    public static WallpaperException FileMissing(string name) => new(
        $"“{name}” could not be found. It may have been moved or deleted.",
        "Import the video again.");

    public static WallpaperException NotPlayable(string name) => new(
        $"“{name}” cannot be played on this PC. Its format may be unsupported.",
        FormatSuggestion);

    public static WallpaperException ProtectedContent(string name) => new(
        $"“{name}” is protected by DRM and cannot be used as a wallpaper.",
        FormatSuggestion);

    public static WallpaperException NoVideoTrack(string name) => new(
        $"“{name}” does not contain a video track.",
        FormatSuggestion);

    public static WallpaperException EmptyDuration(string name) => new(
        $"“{name}” has no playable duration.",
        FormatSuggestion);

    public static WallpaperException ImportFailed(string name, string reason) => new(
        $"“{name}” could not be imported. {reason}",
        null);

    public static WallpaperException StorageUnavailable(string reason) => new(
        $"Wallpache could not open its storage folder. {reason}",
        null);

    /// <summary>Combines the message and its suggestion so the user sees what to do next.</summary>
    public static string Describe(Exception error)
    {
        var suggestion = (error as WallpaperException)?.RecoverySuggestion;
        return string.IsNullOrEmpty(suggestion) ? error.Message : $"{error.Message} {suggestion}";
    }
}
