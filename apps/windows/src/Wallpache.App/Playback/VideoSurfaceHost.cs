using System;
using System.Numerics;
using Wallpache.App.Interop;
using Wallpache.App.Support;
using Windows.Media.Playback;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Composition.Desktop;

namespace Wallpache.App.Playback;

/// <summary>
/// Draws a <see cref="VideoLoopPlayer"/> into a native window handle.
///
/// The Windows counterpart of the macOS <c>WallpaperPlayerView</c>: the media
/// player renders into a composition surface at the clip's own pixel size, and
/// a surface brush resolves the scaling mode against the window bounds. Doing
/// the fit in the brush rather than in the decoder means changing mode never
/// reloads the video.
/// </summary>
public sealed class VideoSurfaceHost : IDisposable
{
    private readonly IntPtr _hwnd;
    private readonly Compositor _compositor;
    private readonly DesktopWindowTarget? _target;
    private readonly ContainerVisual? _root;
    private readonly SpriteVisual? _background;
    private readonly SpriteVisual? _video;
    private readonly CompositionSurfaceBrush? _brush;

    private VideoLoopPlayer? _player;
    private Vector2 _size;
    private Vector2 _videoPixelSize;
    private ScalingMode _scalingMode = ScalingMode.Fill;
    private bool _disposed;

    public VideoSurfaceHost(IntPtr hwnd, int width, int height)
    {
        _hwnd = hwnd;
        _size = new Vector2(Math.Max(width, 1), Math.Max(height, 1));
        _compositor = CompositionInterop.Compositor;
        _target = CompositionInterop.TryCreateTarget(hwnd);

        if (_target is null)
        {
            return;
        }

        _root = _compositor.CreateContainerVisual();
        _root.Size = _size;

        // Black underneath, so Fit and Center letterbox onto black exactly as
        // they do on macOS instead of showing whatever Explorer painted.
        _background = _compositor.CreateSpriteVisual();
        _background.Size = _size;
        _background.Brush = _compositor.CreateColorBrush(Color.FromArgb(255, 0, 0, 0));

        _brush = _compositor.CreateSurfaceBrush();
        _brush.Stretch = _scalingMode.ToCompositionStretch();
        _brush.HorizontalAlignmentRatio = 0.5f;
        _brush.VerticalAlignmentRatio = 0.5f;

        _video = _compositor.CreateSpriteVisual();
        _video.Size = _size;
        _video.Brush = _brush;

        _root.Children.InsertAtTop(_background);
        _root.Children.InsertAtTop(_video);
        _target.Root = _root;
    }

    /// <summary>Whether composition is actually driving this window.</summary>
    public bool IsAvailable => _target is not null;

    public IntPtr WindowHandle => _hwnd;

    /// <summary>Binds a player's video output to this window.</summary>
    public void Attach(VideoLoopPlayer? player)
    {
        if (_player is not null)
        {
            _player.PlayerReplaced -= OnPlayerReplaced;
        }

        _player = player;

        if (player is null)
        {
            if (_brush is not null)
            {
                _brush.Surface = null;
            }

            return;
        }

        player.PlayerReplaced += OnPlayerReplaced;
        BindSurface(player.Player);
    }

    /// <summary>
    /// The clip's natural pixel size. The composition surface is created at this
    /// size so the brush stretch, not the decoder, decides the framing.
    /// </summary>
    public void SetVideoPixelSize(double? width, double? height)
    {
        _videoPixelSize = width is > 0 && height is > 0
            ? new Vector2((float)width.Value, (float)height.Value)
            : Vector2.Zero;

        ApplySurfaceSize();
    }

    public void SetScalingMode(ScalingMode mode)
    {
        _scalingMode = mode;
        if (_brush is not null)
        {
            _brush.Stretch = mode.ToCompositionStretch();
        }
    }

    /// <summary>Follows a window that changed resolution, arrangement, or rotation.</summary>
    public void Resize(int width, int height)
    {
        var updated = new Vector2(Math.Max(width, 1), Math.Max(height, 1));
        if (updated == _size)
        {
            return;
        }

        _size = updated;
        if (_root is not null)
        {
            _root.Size = _size;
        }

        if (_background is not null)
        {
            _background.Size = _size;
        }

        if (_video is not null)
        {
            _video.Size = _size;
        }

        ApplySurfaceSize();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_player is not null)
        {
            _player.PlayerReplaced -= OnPlayerReplaced;
            _player = null;
        }

        try
        {
            if (_brush is not null)
            {
                _brush.Surface = null;
            }

            if (_target is not null)
            {
                _target.Root = null;
            }

            _root?.Dispose();
            _background?.Dispose();
            _video?.Dispose();
            _brush?.Dispose();
        }
        catch (Exception error)
        {
            Log.Playback.Error($"Surface teardown failed: {error.Message}");
        }
    }

    // MARK: - Private

    private void OnPlayerReplaced(MediaPlayer player) => BindSurface(player);

    private void BindSurface(MediaPlayer? player)
    {
        if (_brush is null || player is null || _disposed)
        {
            return;
        }

        try
        {
            ApplySurfaceSize(player);
            var surface = player.GetSurface(_compositor);
            _brush.Surface = surface.CompositionSurface;
            Log.Playback.Info(
                $"Surface bound: hwnd=0x{_hwnd:X} target={(_target is not null)} " +
                $"compSurface={(surface.CompositionSurface is not null)} size={_size} videoPx={_videoPixelSize}");
        }
        catch (Exception error)
        {
            Log.Playback.Error($"Surface binding failed: {error.GetType().FullName} {error.Message}\n{error.StackTrace}");
        }
    }

    private void ApplySurfaceSize() => ApplySurfaceSize(_player?.Player);

    /// <summary>
    /// Decodes at the clip's own resolution when it is known. Without it the
    /// player would letterbox into the window's aspect ratio itself, and Fill
    /// would show bars instead of cropping.
    /// </summary>
    private void ApplySurfaceSize(MediaPlayer? player)
    {
        if (player is null)
        {
            return;
        }

        var size = _videoPixelSize == Vector2.Zero ? _size : _videoPixelSize;

        try
        {
            player.SetSurfaceSize(new Windows.Foundation.Size(size.X, size.Y));
        }
        catch (Exception error)
        {
            Log.Playback.Error($"Surface sizing failed: {error.Message}");
        }
    }
}
