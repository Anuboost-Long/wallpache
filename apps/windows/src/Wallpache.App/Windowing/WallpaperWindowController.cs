using System;
using Wallpache.App.Desktop;
using Wallpache.App.Displays;
using Wallpache.App.Interop;
using Wallpache.App.Playback;
using Wallpache.App.Support;

namespace Wallpache.App.Windowing;

/// <summary>
/// Owns the wallpaper window for exactly one display: its native handle, its
/// attachment to the Explorer desktop host, and the composition surface that
/// draws into it.
/// </summary>
public sealed class WallpaperWindowController : IDisposable
{
    private readonly DesktopHostService _desktopHost;
    private WallpaperWindow? _window;
    private VideoSurfaceHost? _surface;
    private bool _isAttached;

    public WallpaperWindowController(DesktopHostService desktopHost, DisplayDescriptor display)
    {
        _desktopHost = desktopHost;

        var bounds = ToRect(display);
        _window = new WallpaperWindow(bounds);

        if (!_window.IsValid)
        {
            return;
        }

        _surface = new VideoSurfaceHost(_window.Handle, bounds.Width, bounds.Height);
        if (!_surface.IsAvailable)
        {
            Log.Playback.Error($"Composition unavailable on {display.Name}; the wallpaper cannot be drawn");
        }
    }

    /// <summary>Whether the window and its drawing surface both came up.</summary>
    public bool IsUsable => _window is { IsValid: true } && _surface is { IsAvailable: true };

    public void Attach(VideoLoopPlayer? player) => _surface?.Attach(player);

    public void Apply(ScalingMode scalingMode, int? videoWidth, int? videoHeight)
    {
        _surface?.SetVideoPixelSize(videoWidth, videoHeight);
        _surface?.SetScalingMode(scalingMode);
    }

    /// <summary>Parents the window into the desktop and makes it visible.</summary>
    public void Show()
    {
        if (_window is null || !_window.IsValid)
        {
            return;
        }

        if (!_desktopHost.Attach(_window.Handle, _window.Bounds))
        {
            Log.Desktop.Error("Could not attach the wallpaper window to the desktop host");
            return;
        }

        _window.ApplyChildStyles();
        _desktopHost.Position(_window.Handle, _window.Bounds);
        _isAttached = true;
    }

    /// <summary>Follows a display that changed resolution, arrangement, or rotation.</summary>
    public void MoveTo(DisplayDescriptor display)
    {
        if (_window is null || !_window.IsValid)
        {
            return;
        }

        var bounds = ToRect(display);
        if (bounds.Left == _window.Bounds.Left
            && bounds.Top == _window.Bounds.Top
            && bounds.Width == _window.Bounds.Width
            && bounds.Height == _window.Bounds.Height)
        {
            return;
        }

        _window.SetBounds(bounds);
        _desktopHost.Position(_window.Handle, bounds);
        _surface?.Resize(bounds.Width, bounds.Height);
    }

    /// <summary>
    /// Re-asserts desktop placement. Explorer restarts and some display changes
    /// leave the window orphaned or mis-ordered; this is cheap and idempotent,
    /// so it can run after every wake and reconfiguration.
    /// </summary>
    public void ReassertDesktopPlacement()
    {
        if (_window is null || !_window.IsValid)
        {
            return;
        }

        if (!_isAttached)
        {
            Show();
            return;
        }

        _desktopHost.ReassertPlacement(_window.Handle, _window.Bounds);
        _window.ApplyChildStyles();
    }

    public void Close() => Dispose();

    public void Dispose()
    {
        if (_window is not null && _window.IsValid)
        {
            DesktopHostService.Detach(_window.Handle);
        }

        _surface?.Attach(null);
        _surface?.Dispose();
        _surface = null;

        _window?.Dispose();
        _window = null;
        _isAttached = false;
    }

    private static NativeMethods.RECT ToRect(DisplayDescriptor display) => new()
    {
        Left = display.X,
        Top = display.Y,
        Right = display.X + display.Width,
        Bottom = display.Y + display.Height
    };
}
