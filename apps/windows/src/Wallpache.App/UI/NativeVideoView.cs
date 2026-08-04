using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Wallpache.App.Interop;
using Wallpache.App.Playback;

namespace Wallpache.App.UI;

/// <summary>
/// Hosts a <see cref="VideoSurfaceHost"/> inside the app's own UI.
///
/// The preview deliberately reuses the wallpaper's media pipeline rather than a
/// separate one, so what the user sees before applying is what they get.
/// </summary>
public sealed class NativeVideoView : NativeControlHost
{
    private ChildSurfaceWindow? _window;
    private VideoSurfaceHost? _surface;
    private VideoLoopPlayer? _pendingPlayer;
    private ScalingMode _scalingMode = ScalingMode.Fill;
    private int? _videoWidth;
    private int? _videoHeight;

    /// <summary>
    /// Binds a player. Safe to call before the control is attached to a window:
    /// the surface picks the player up once its handle exists.
    /// </summary>
    public void Attach(VideoLoopPlayer? player, int? videoWidth, int? videoHeight, ScalingMode scalingMode)
    {
        _pendingPlayer = player;
        _videoWidth = videoWidth;
        _videoHeight = videoHeight;
        _scalingMode = scalingMode;

        if (_surface is null)
        {
            return;
        }

        _surface.SetVideoPixelSize(videoWidth, videoHeight);
        _surface.SetScalingMode(scalingMode);
        _surface.Attach(player);
    }

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        var (width, height) = PixelSize();

        _window = new ChildSurfaceWindow(parent.Handle, width, height);
        _surface = new VideoSurfaceHost(_window.Handle, width, height);

        _surface.SetVideoPixelSize(_videoWidth, _videoHeight);
        _surface.SetScalingMode(_scalingMode);
        _surface.Attach(_pendingPlayer);

        return new PlatformHandle(_window.Handle, "HWND");
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        _surface?.Attach(null);
        _surface?.Dispose();
        _surface = null;

        _window?.Dispose();
        _window = null;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var arranged = base.ArrangeOverride(finalSize);

        // The host resizes the child window itself; the composition visuals have
        // to be told separately or the video keeps its first size.
        var scaling = RenderScaling;
        _surface?.Resize(
            (int)Math.Round(arranged.Width * scaling),
            (int)Math.Round(arranged.Height * scaling));

        return arranged;
    }

    /// <summary>
    /// Composition visuals are sized in physical pixels, so every measurement
    /// taken from the layout has to be scaled by the window's DPI factor.
    /// </summary>
    private double RenderScaling => TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;

    private (int Width, int Height) PixelSize()
    {
        var scaling = RenderScaling;
        var width = Bounds.Width > 0 ? Bounds.Width : 640;
        var height = Bounds.Height > 0 ? Bounds.Height : 360;

        return ((int)Math.Round(width * scaling), (int)Math.Round(height * scaling));
    }
}
