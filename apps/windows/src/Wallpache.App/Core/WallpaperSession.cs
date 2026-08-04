using System;
using Wallpache.App.Desktop;
using Wallpache.App.Displays;
using Wallpache.App.Library;
using Wallpache.App.Playback;
using Wallpache.App.Support;
using Wallpache.App.Windowing;

namespace Wallpache.App.Core;

/// <summary>
/// One display's live wallpaper: its window, its player, and its settings.
///
/// A session is the only object that knows both halves of the engine, which
/// keeps window code out of the player and vice versa.
/// </summary>
public sealed class WallpaperSession : IDisposable
{
    private readonly WallpaperWindowController _windowController;
    private readonly VideoLoopPlayer _player;

    public WallpaperSession(
        DesktopHostService desktopHost,
        DisplayDescriptor display,
        WallpaperRecord record,
        string videoPath,
        DisplayWallpaperConfiguration configuration)
    {
        DisplayId = display.Id;
        WallpaperId = record.Id;
        Configuration = configuration.Copy();

        _windowController = new WallpaperWindowController(desktopHost, display);
        _player = new VideoLoopPlayer(videoPath, configuration.IsMuted, configuration.PlaybackRate);

        // The video's pixel size must be set before the player is attached: the
        // composition surface is sized and bound (MediaPlayer.SetSurfaceSize then
        // GetSurface) on attach, and resizing the surface again right after
        // GetSurface has already handed out a bound surface stalls the frame
        // pump after the first frame instead of raising an error.
        _windowController.Apply(configuration.ScalingMode, record.Width, record.Height);
        _windowController.Attach(_player);
        _windowController.Show();

        _player.UnrecoverableFailure += error => PlaybackFailed?.Invoke(DisplayId, error);
    }

    public event Action<string, Exception?>? PlaybackFailed;

    public string DisplayId { get; }

    public Guid WallpaperId { get; private set; }

    public DisplayWallpaperConfiguration Configuration { get; private set; }

    /// <summary>Whether the window and its drawing surface came up at all.</summary>
    public bool IsUsable => _windowController.IsUsable;

    // MARK: - Transport

    public void Play() => _player.Play();

    public void Pause() => _player.Pause();

    /// <summary>Tears the session down completely; the plain Windows wallpaper reappears.</summary>
    public void Stop()
    {
        _player.Stop();
        _windowController.Close();
    }

    // MARK: - Updates

    public void Update(DisplayWallpaperConfiguration configuration, int? videoWidth, int? videoHeight)
    {
        if (configuration.ScalingMode != Configuration.ScalingMode)
        {
            _windowController.Apply(configuration.ScalingMode, videoWidth, videoHeight);
        }

        if (configuration.IsMuted != Configuration.IsMuted)
        {
            _player.SetMuted(configuration.IsMuted);
        }

        if (Math.Abs(configuration.PlaybackRate - Configuration.PlaybackRate) > 0.0001)
        {
            _player.SetRate(configuration.PlaybackRate);
        }

        Configuration = configuration.Copy();
    }

    /// <summary>
    /// Swaps the video without recreating the window, so applying a different
    /// wallpaper never flashes the desktop.
    /// </summary>
    public void ReplaceVideo(Guid wallpaperId, string videoPath, int? videoWidth, int? videoHeight)
    {
        WallpaperId = wallpaperId;

        // Same ordering requirement as construction: the new size must be in
        // place before ReplaceVideo rebuilds the player and re-binds the
        // surface, or the post-bind resize stalls the frame pump.
        _windowController.Apply(Configuration.ScalingMode, videoWidth, videoHeight);
        _player.ReplaceVideo(videoPath);
    }

    public void MoveTo(DisplayDescriptor display) => _windowController.MoveTo(display);

    /// <summary>
    /// Re-establishes both halves after wake, an Explorer restart, or a display
    /// reconfiguration. Idempotent by design: it can run on every event.
    /// </summary>
    public void Recover(bool shouldPlay)
    {
        _windowController.ReassertDesktopPlacement();
        _player.RecoverIfNeeded();

        if (shouldPlay)
        {
            _player.Play();
        }
        else
        {
            _player.Pause();
        }
    }

    public void Dispose() => Stop();
}
