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
    private MediaPlayer? _pendingPlayer;
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
        MediaPlayer? pending;
        lock (_gate)
        {
            player = _player;
            _player = null;
            pending = _pendingPlayer;
            _pendingPlayer = null;
        }

        DestroyPlayer(player);
        DestroyPlayer(pending);
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

    /// <summary>
    /// Switches to a different clip without ever showing a blank frame: a
    /// second player opens and starts <paramref name="newPath"/> off-screen
    /// while the current one keeps playing, and only takes over once it has
    /// real frames flowing (see <see cref="BeginSwap"/>). <see cref="Rebuild"/>,
    /// used for recovery, still tears down and rebuilds immediately, since
    /// there is nothing worth preserving on screen when the player is already
    /// broken.
    /// </summary>
    public void ReplaceVideo(string newPath)
    {
        FilePath = newPath;
        _rebuildAttempts = 0;
        BeginSwap();
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
    /// Prepares a replacement player for <see cref="FilePath"/> off-screen -
    /// muted, unbound from any surface - while the current one keeps playing.
    /// <see cref="OnPlaybackStateChanged"/> promotes it via
    /// <see cref="CompleteSwap"/> once it reaches
    /// <see cref="MediaPlaybackState.Playing"/>, so the old player is never
    /// torn down before the new one has a frame ready to show.
    /// </summary>
    private void BeginSwap()
    {
        if (_disposed)
        {
            return;
        }

        var candidate = CreatePlayer();
        if (candidate is null)
        {
            Log.Playback.Error($"Could not prepare a replacement player for {Path.GetFileName(FilePath)}");
            return;
        }

        MediaPlayer? stale;
        lock (_gate)
        {
            // A swap was already pending; the newer request wins and the
            // half-prepared one is abandoned.
            stale = _pendingPlayer;
            _pendingPlayer = candidate;
        }

        DestroyPlayer(stale);

        candidate.IsMuted = true;

        try
        {
            candidate.Play();
        }
        catch (Exception error)
        {
            Log.Playback.Error($"Priming the replacement player failed: {error.Message}");
            AbandonSwap(candidate);
        }
    }

    /// <summary>
    /// Promotes a prepared candidate to the live player. The outgoing player
    /// is silenced before the incoming one is made audible, so the two are
    /// never both heard at once, and is only destroyed once the surface has
    /// already been told to rebind (see <see cref="PlayerReplaced"/>).
    /// </summary>
    private void CompleteSwap(MediaPlayer candidate)
    {
        bool superseded;
        MediaPlayer? old = null;

        lock (_gate)
        {
            superseded = !ReferenceEquals(_pendingPlayer, candidate);
            if (!superseded)
            {
                old = _player;
                _player = candidate;
                _pendingPlayer = null;
            }
        }

        if (superseded)
        {
            DestroyPlayer(candidate);
            return;
        }

        if (old is not null)
        {
            try
            {
                old.IsMuted = true;
            }
            catch (Exception)
            {
                // Being torn down regardless; a failed mute is not worth logging.
            }
        }

        candidate.IsMuted = _isMuted;
        candidate.PlaybackSession.PlaybackRate = _rate;

        PlayerReplaced?.Invoke(candidate);

        DestroyPlayer(old);
    }

    /// <summary>Discards a candidate that failed before it could take over.</summary>
    private void AbandonSwap(MediaPlayer candidate)
    {
        lock (_gate)
        {
            if (ReferenceEquals(_pendingPlayer, candidate))
            {
                _pendingPlayer = null;
            }
        }

        DestroyPlayer(candidate);
    }

    /// <summary>
    /// Reaching <see cref="MediaPlaybackState.Playing"/> proves the current build
    /// works, so the rebuild budget resets and a long session cannot exhaust it.
    /// The same signal, on a pending candidate's session, means it now has a
    /// real frame ready and can take over from the current player.
    /// </summary>
    private void OnPlaybackStateChanged(MediaPlaybackSession sender, object args)
    {
        try
        {
            Log.Playback.Info($"PlaybackState -> {sender.PlaybackState} for {Path.GetFileName(FilePath)}");

            if (sender.PlaybackState != MediaPlaybackState.Playing)
            {
                return;
            }

            _rebuildAttempts = 0;

            var pending = _pendingPlayer;
            if (pending is not null && ReferenceEquals(sender, pending.PlaybackSession))
            {
                Dispatcher.UIThread.Post(() => CompleteSwap(pending));
            }
        }
        catch (Exception)
        {
            // The session can be torn down between the event and this read.
        }
    }

    /// <summary>
    /// Media failures arrive on a background thread; every repair runs on the UI
    /// thread so the surface rebind and the player swap stay ordered. A
    /// failure on a pending candidate only abandons that candidate - the
    /// still-healthy active player must not be rebuilt because of it.
    /// </summary>
    private void OnMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
    {
        var error = args.ExtendedErrorCode;
        Dispatcher.UIThread.Post(() =>
        {
            if (ReferenceEquals(sender, _pendingPlayer))
            {
                Log.Playback.Error($"Replacement player failed to open {Path.GetFileName(FilePath)}: {error?.Message}");
                AbandonSwap(sender);
                return;
            }

            HandleFailure(error);
        });
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
