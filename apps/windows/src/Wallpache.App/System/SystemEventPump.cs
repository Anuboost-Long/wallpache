using System;
using Wallpache.App.Interop;

namespace Wallpache.App.System;

/// <summary>
/// The app's single window-message source.
///
/// Power, session, display, and Explorer notifications are all delivered as
/// window messages, and several of them have to be registered against a real
/// HWND. One hidden message-only window serves every subscriber, so there is
/// exactly one place where the app talks to the Windows message loop.
/// </summary>
public sealed class SystemEventPump : IDisposable
{
    private readonly MessageWindow _window;

    public SystemEventPump()
    {
        _window = new MessageWindow(OnMessage);
    }

    /// <summary>Raised on the UI thread for every message the window receives.</summary>
    public event Action<uint, IntPtr, IntPtr>? Message;

    public IntPtr Handle => _window.Handle;

    public void Dispose() => _window.Dispose();

    /// <summary>
    /// Always falls through to <c>DefWindowProc</c>: these are notifications, and
    /// swallowing them would change how Windows treats the window.
    /// </summary>
    private bool OnMessage(uint message, IntPtr wParam, IntPtr lParam)
    {
        Message?.Invoke(message, wParam, lParam);
        return false;
    }
}
