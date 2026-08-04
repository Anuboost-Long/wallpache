using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Wallpache.App.Support;

namespace Wallpache.App.Interop;

/// <summary>
/// A hidden message-only window. Power, session, display, and Explorer-restart
/// notifications are all delivered as window messages, so one of these is the
/// app's single entry point for system events.
/// </summary>
internal sealed class MessageWindow : IDisposable
{
    private readonly Func<uint, IntPtr, IntPtr, bool> _handler;
    private IntPtr _handle;
    private bool _disposed;

    private const string ClassName = "WallpacheMessageWindow";

    // The window class carries one procedure for every instance, so messages are
    // routed back to the owning object by handle.
    private static readonly ConcurrentDictionary<IntPtr, MessageWindow> Instances = new();
    private static readonly NativeMethods.WndProc ClassProc = Dispatch;
    private static readonly object ClassGate = new();
    private static ushort _classAtom;

    /// <param name="handler">
    /// Returns <see langword="true"/> when the message was handled; anything
    /// else falls through to <c>DefWindowProc</c>.
    /// </param>
    internal MessageWindow(Func<uint, IntPtr, IntPtr, bool> handler)
    {
        _handler = handler;

        EnsureClassRegistered();

        _handle = NativeMethods.CreateWindowEx(
            0,
            ClassName,
            "Wallpache",
            0,
            0,
            0,
            0,
            0,
            NativeMethods.HWND_MESSAGE,
            IntPtr.Zero,
            NativeMethods.GetModuleHandle(null),
            IntPtr.Zero);

        if (_handle == IntPtr.Zero)
        {
            Log.Lifecycle.Error($"Message window creation failed: {Marshal.GetLastWin32Error()}");
            return;
        }

        Instances[_handle] = this;
    }

    internal IntPtr Handle => _handle;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_handle != IntPtr.Zero)
        {
            Instances.TryRemove(_handle, out _);
            NativeMethods.DestroyWindow(_handle);
            _handle = IntPtr.Zero;
        }
    }

    private static IntPtr Dispatch(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (Instances.TryGetValue(hWnd, out var window))
        {
            try
            {
                if (window._handler(msg, wParam, lParam))
                {
                    return IntPtr.Zero;
                }
            }
            catch (Exception error)
            {
                // A throwing handler must not tear down the message pump.
                Log.Lifecycle.Error($"Message handler failed for 0x{msg:X4}: {error.Message}");
            }
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
                Log.Lifecycle.Error($"Message window class registration failed: {Marshal.GetLastWin32Error()}");
            }
        }
    }
}
