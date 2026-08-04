using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Wallpache.App.Interop;
using Wallpache.App.Support;

namespace Wallpache.App.Windowing;

/// <summary>
/// Borderless, non-interactive native window that hosts the video for one
/// display.
///
/// It must never take focus and must never swallow a click: the desktop has to
/// keep behaving like the desktop. The window is a plain Win32 window rather
/// than a framework window because it is re-parented into Explorer's tree,
/// which no UI framework's window lifecycle expects.
/// </summary>
internal sealed class WallpaperWindow : IDisposable
{
    private const string ClassName = "WallpacheWallpaperWindow";

    private static readonly ConcurrentDictionary<IntPtr, WallpaperWindow> Instances = new();
    private static readonly NativeMethods.WndProc ClassProc = Dispatch;
    private static readonly object ClassGate = new();
    private static ushort _classAtom;

    private IntPtr _handle;
    private bool _disposed;

    internal WallpaperWindow(NativeMethods.RECT bounds)
    {
        EnsureClassRegistered();

        // WS_POPUP to begin with: the window becomes a child only once the
        // desktop host has been found, and a WS_CHILD window with no parent is
        // not a valid state to sit in.
        _handle = NativeMethods.CreateWindowEx(
            NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TRANSPARENT,
            ClassName,
            "Wallpache Wallpaper",
            NativeMethods.WS_POPUP | NativeMethods.WS_CLIPSIBLINGS | NativeMethods.WS_CLIPCHILDREN
                | NativeMethods.WS_DISABLED,
            bounds.Left,
            bounds.Top,
            bounds.Width,
            bounds.Height,
            IntPtr.Zero,
            IntPtr.Zero,
            NativeMethods.GetModuleHandle(null),
            IntPtr.Zero);

        if (_handle == IntPtr.Zero)
        {
            Log.Playback.Error($"Wallpaper window creation failed: {Marshal.GetLastWin32Error()}");
            return;
        }

        Instances[_handle] = this;
        Bounds = bounds;
    }

    internal IntPtr Handle => _handle;

    internal NativeMethods.RECT Bounds { get; private set; }

    internal bool IsValid => _handle != IntPtr.Zero && NativeMethods.IsWindow(_handle);

    /// <summary>
    /// Switches the window to the child styles the desktop host requires. Called
    /// once the window has been parented, so the two always agree.
    /// </summary>
    internal void ApplyChildStyles()
    {
        if (!IsValid)
        {
            return;
        }

        var style = (uint)NativeMethods.GetWindowLongPtr(_handle, NativeMethods.GWL_STYLE).ToInt64();
        style &= ~NativeMethods.WS_POPUP;
        style |= NativeMethods.WS_CHILD | NativeMethods.WS_VISIBLE
            | NativeMethods.WS_CLIPSIBLINGS | NativeMethods.WS_CLIPCHILDREN;
        NativeMethods.SetWindowLongPtr(_handle, NativeMethods.GWL_STYLE, new IntPtr((long)style));

        // WS_EX_APPWINDOW would put the window back into the taskbar and Alt+Tab,
        // which is exactly what a wallpaper must not do.
        var exStyle = (uint)NativeMethods.GetWindowLongPtr(_handle, NativeMethods.GWL_EXSTYLE).ToInt64();
        exStyle &= ~NativeMethods.WS_EX_APPWINDOW;
        exStyle |= NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TRANSPARENT;
        NativeMethods.SetWindowLongPtr(_handle, NativeMethods.GWL_EXSTYLE, new IntPtr((long)exStyle));

        NativeMethods.SetWindowPos(
            _handle,
            IntPtr.Zero,
            0,
            0,
            0,
            0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER
                | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_FRAMECHANGED);
    }

    internal void SetBounds(NativeMethods.RECT bounds) => Bounds = bounds;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_handle == IntPtr.Zero)
        {
            return;
        }

        Instances.TryRemove(_handle, out _);
        NativeMethods.DestroyWindow(_handle);
        _handle = IntPtr.Zero;
    }

    // MARK: - Private

    private static IntPtr Dispatch(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            // Composition paints every pixel, so erasing first only causes a
            // black flash on resize.
            case NativeMethods.WM_ERASEBKGND:
                return new IntPtr(1);

            // Both of these keep the desktop interactive: clicks, rubber-band
            // selection, and right-click menus have to reach the icon view.
            case NativeMethods.WM_NCHITTEST:
                return new IntPtr(NativeMethods.HTTRANSPARENT);

            case NativeMethods.WM_MOUSEACTIVATE:
                return new IntPtr(NativeMethods.MA_NOACTIVATE);

            case NativeMethods.WM_DESTROY:
                Instances.TryRemove(hWnd, out _);
                break;
        }

        return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
    }

    private static void EnsureClassRegistered()
    {
        lock (ClassGate)
        {
            if (_classAtom != 0)
            {
                return;
            }

            var wndClass = new NativeMethods.WNDCLASSEX
            {
                cbSize = Marshal.SizeOf<NativeMethods.WNDCLASSEX>(),
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(ClassProc),
                hInstance = NativeMethods.GetModuleHandle(null),
                lpszClassName = ClassName
            };

            _classAtom = NativeMethods.RegisterClassEx(ref wndClass);
            if (_classAtom == 0)
            {
                Log.Playback.Error($"Wallpaper window class registration failed: {Marshal.GetLastWin32Error()}");
            }
        }
    }
}
