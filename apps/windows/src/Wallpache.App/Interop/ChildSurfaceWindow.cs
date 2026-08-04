using System;
using System.Runtime.InteropServices;
using Wallpache.App.Support;

namespace Wallpache.App.Interop;

/// <summary>
/// A plain child window used as a composition target inside the app's own UI.
///
/// The preview needs the same media pipeline as the wallpaper, and that pipeline
/// draws into an HWND. This is the smallest window that can host it.
/// </summary>
internal sealed class ChildSurfaceWindow : IDisposable
{
    private const string ClassName = "WallpacheVideoSurface";

    private static readonly NativeMethods.WndProc ClassProc = Dispatch;
    private static readonly object ClassGate = new();
    private static ushort _classAtom;

    private IntPtr _handle;

    internal ChildSurfaceWindow(IntPtr parent, int width, int height)
    {
        EnsureClassRegistered();

        _handle = NativeMethods.CreateWindowEx(
            0,
            ClassName,
            null,
            NativeMethods.WS_CHILD | NativeMethods.WS_VISIBLE
                | NativeMethods.WS_CLIPSIBLINGS | NativeMethods.WS_CLIPCHILDREN,
            0,
            0,
            Math.Max(width, 1),
            Math.Max(height, 1),
            parent,
            IntPtr.Zero,
            NativeMethods.GetModuleHandle(null),
            IntPtr.Zero);

        if (_handle == IntPtr.Zero)
        {
            Log.Playback.Error($"Preview surface window creation failed: {Marshal.GetLastWin32Error()}");
        }
    }

    internal IntPtr Handle => _handle;

    internal void Resize(int width, int height)
    {
        if (_handle == IntPtr.Zero)
        {
            return;
        }

        NativeMethods.SetWindowPos(
            _handle,
            IntPtr.Zero,
            0,
            0,
            Math.Max(width, 1),
            Math.Max(height, 1),
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
    }

    public void Dispose()
    {
        if (_handle == IntPtr.Zero)
        {
            return;
        }

        NativeMethods.DestroyWindow(_handle);
        _handle = IntPtr.Zero;
    }

    private static IntPtr Dispatch(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        // Composition paints every pixel, so erasing first only causes flicker.
        if (msg == NativeMethods.WM_ERASEBKGND)
        {
            return new IntPtr(1);
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
                Log.Playback.Error($"Preview surface class registration failed: {Marshal.GetLastWin32Error()}");
            }
        }
    }
}
