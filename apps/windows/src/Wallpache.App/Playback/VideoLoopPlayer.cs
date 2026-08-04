using System;
using System.IO;
using Avalonia.Threading;
using Wallpache.App.Support;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace Wallpache.App.Playback;

/// <summary>
/// A seamlessly looping, muted-by-default video player.
///
/// <c>MediaPlayer.IsLoopingEnabled</c> keeps the clip running without replacing
/// the source on every pass. The player can rebuild itself from the source path
/// after a failure or a stall, which is what makes the wallpaper survive
/// sleep/wake cycles and transient decoder errors.
/// </summary>
public sealed class VideoLoopPlayer : IDisposable
{
    private const int MaximumRebuildAttempts = 3;

    /// <summary>Raised when playback fails in a way the player could not repair itself.</summary>
    public event Action<Exception?>? UnrecoverableFailure;

    private readonly object _gate = new();
    private MediaPlayer? _player;
    private int _rebuildAttempts;
    private bool _isPlaying;
    private bool _isMuted;
    private double _rate;
    private bool _disposed;

    public VideoLoopPlayer(string path, bool isMuted, double rate)
    {
        FilePath = path;
        _isMuted = isMuted;
        _rate = rate <= 0 ? 1.0 : rate;

        _player = CreatePlayer();
    }

    public string FilePath { get; private set; }

    /// <summary>
    /// The underlying player, needed by the composition surface. Null once the
    /// player has been stopped.
    /// </summary>
    internal MediaPlayer? Player
    {
        get
        {
            lock (_gate)
            {
                return _player;
            }
        }
    }

    /// <summary>Raised when a rebuild swapped in a new player, so surfaces can rebind.</summary>
    public event Action<MediaPlayer>? PlayerReplaced;

    // MARK: - Transport

    public void Play()
    {
        _isPlaying = true;
        var player = Player;
        if (player is null)
        {
            return;
        }

        try
        {
            player.PlaybackSession.PlaybackRate = _rate;
            player.Play();
        }
        catch (Exception error)
        {
            Log.Playback.Error($"Play failed for {Path.GetFileName(FilePath)}: {error.Message}");
        }
    }

    public void Pause()
    {
        _isPlaying = false;
        try
        {
            Player?.Pause();
        }
        catch (Exception error)
        {
            Log.Playback.Error($"Pause failed: {error.Message}");
        }
    }

    /// <summary>Tears the player down completely. The player is not reusable afterwards.</summary>
    public void Stop()
    {
        _isPlaying = false;
        _disposed = true;

        MediaPlayer? player;
        lock (_gate)
        {
            player = _player;
            _player = null;
        }

        DestroyPlayer(player);
    }

    public void SetMuted(bool isMuted)
    {
        _isMuted = isMuted;
        var player = Player;
        if (player is not null)
        {
            player.IsMuted = isMuted;
        }
    }

    public void SetRate(double rate)
    {
        _rate = rate <= 0 ? 1.0 : rate;
        var player = Player;
        if (player is not null && _isPlaying)
        {
            player.PlaybackSession.PlaybackRate = _rate;
        }
    }

    public void ReplaceVideo(string newPath)
    {
        FilePath = newPath;
        _rebuildAttempts = 0;
        Rebuild();
    }

    /// <summary>
    /// Rebuilds the player when it is not in a healthy state. Safe to call after
    /// every wake, display change, or policy transition.
    /// </summary>
    public void RecoverIfNeeded()
    {
        if (_disposed)
        {
            return;
        }

        var player = Player;
        if (player is null)
        {
            Rebuild();
            return;
        }

        MediaPlaybackState state;
        try
        {
            state = player.PlaybackSession.PlaybackState;
        }
        catch (Exception)
        {
            Rebuild();
            return;
        }

        if (state == MediaPlaybackState.None)
        {
            Log.Playback.Info($"Recovering player for {Path.GetFileName(FilePath)}");
            Rebuild();
            return;
        }

        if (_isPlaying && state != MediaPlaybackState.Playing)
        {
            Play();
        }
    }

    public void Dispose() => Stop();

    // MARK: - Private

    private MediaPlayer? CreatePlayer()
    {
        try
        {
            var player = new MediaPlayer
            {
                IsMuted = _isMuted,
                IsLoopingEnabled = true,
                AutoPlay = false,
                // A wallpaper must not appear in the system transport controls
                // or steal the play/pause media keys.
                IsVideoFrameServerEnabled = false
            };

            player.CommandManager.IsEnabled = false;
            player.MediaFailed += OnMediaFailed;
            player.MediaOpened += (sender, _) =>
                Log.Playback.Info(
                    $"MediaOpened {Path.GetFileName(FilePath)}: " +
                    $"{sender.PlaybackSession.NaturalVideoWidth}x{sender.PlaybackSession.NaturalVideoHeight}");
            player.PlaybackSession.PlaybackStateChanged += OnPlaybackStateChanged;
            player.Source = MediaSource.CreateFromUri(new Uri(FilePath));
            player.PlaybackSession.PlaybackRate = _rate;

            return player;
        }
        catch (Exception error)
        {
            Log.Playback.Error($"Player creation failed for {Path.GetFileName(FilePath)}: {error.Message}");
            return null;
        }
    }

    private static void DestroyPlayer(MediaPlayer? player)
    {
        if (player is null)
        {
            return;
        }

        try
        {
            player.Pause();
            player.Source = null;
            player.Dispose();
        }
        catch (Exception error)
        {
            Log.Playback.Error($"Player teardown failed: {error.Message}");
        }
    }

    private void Rebuild()
    {
        if (_disposed)
        {
            return;
        }

        var wasPlaying = _isPlaying;

        MediaPlayer? old;
        MediaPlayer? replacement;
        lock (_gate)
        {
            old = _player;
            _player = null;
            replacement = CreatePlayer();
            _player = replacement;
        }

        DestroyPlayer(old);

        if (replacement is not null)
        {
            PlayerReplaced?.Invoke(replacement);
        }

        if (wasPlaying)
        {
            Play();
        }
    }

    /// <summary>
    /// Reaching <see cref="MediaPlaybackState.Playing"/> proves the current build
    /// works, so the rebuild budget resets and a long session cannot exhaust it.
    /// </summary>
    private void OnPlaybackStateChanged(MediaPlaybackSession sender, object args)
    {
        try
        {
            Log.Playback.Info($"PlaybackState -> {sender.PlaybackState} for {Path.GetFileName(FilePath)}");

            if (sender.PlaybackState == MediaPlaybackState.Playing)
            {
                _rebuildAttempts = 0;
            }
        }
        catch (Exception)
        {
            // The session can be torn down between the event and this read.
        }
    }

    /// <summary>
    /// Media failures arrive on a background thread; every repair runs on the UI
    /// thread so the surface rebind and the player swap stay ordered.
    /// </summary>
    private void OnMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
    {
        var error = args.ExtendedErrorCode;
        Dispatcher.UIThread.Post(() => HandleFailure(error));
    }

    private void HandleFailure(Exception? error)
    {
        if (_disposed)
        {
            return;
        }

        if (_rebuildAttempts >= MaximumRebuildAttempts)
        {
            Log.Playback.Error($"Giving up on {Path.GetFileName(FilePath)} after {_rebuildAttempts} rebuilds");
            UnrecoverableFailure?.Invoke(error);
            return;
        }

        _rebuildAttempts++;
        Log.Playback.Error($"Playback problem (attempt {_rebuildAttempts}): {error?.Message ?? "unknown"}");
        Rebuild();
    }
}
