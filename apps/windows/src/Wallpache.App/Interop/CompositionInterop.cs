using System;
using System.Runtime.InteropServices;
using Wallpache.App.Support;
using Windows.UI.Composition;
using Windows.UI.Composition.Desktop;
using WinRT;

namespace Wallpache.App.Interop;

[ComImport]
[Guid("29E691FA-4567-4DCA-B319-D0F207EB6807")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ICompositorDesktopInterop
{
    void CreateDesktopWindowTarget(IntPtr hwndTarget, [MarshalAs(UnmanagedType.Bool)] bool isTopmost, out IntPtr result);

    void EnsureOnThread(int threadId);
}

/// <summary>
/// Bridges Win32 window handles to <c>Windows.UI.Composition</c>.
///
/// The wallpaper is drawn by a composition visual rather than by GDI or a XAML
/// island: it is the only route that lets a <c>MediaPlayer</c> render straight
/// into an arbitrary HWND, which is what the desktop-host attachment needs.
/// </summary>
internal static class CompositionInterop
{
    private static Compositor? _compositor;
    private static IntPtr _dispatcherQueueController;

    /// <summary>
    /// The process-wide compositor, created on first use.
    ///
    /// A compositor requires a <c>DispatcherQueue</c> on the calling thread, so
    /// this must be called from the UI thread, which already pumps messages.
    /// </summary>
    internal static Compositor Compositor
    {
        get
        {
            if (_compositor is not null)
            {
                return _compositor;
            }

            EnsureDispatcherQueue();
            _compositor = new Compositor();
            return _compositor;
        }
    }

    /// <summary>
    /// Creates the composition target for <paramref name="hwnd"/>. Returns
    /// <see langword="null"/> when composition is unavailable, so callers can
    /// fall back to leaving the desktop untouched instead of crashing.
    /// </summary>
    internal static DesktopWindowTarget? TryCreateTarget(IntPtr hwnd)
    {
        try
        {
            // Compositor.As<ICompositorDesktopInterop>() throws E_NOINTERFACE:
            // CsWinRT's As/TryAs helpers only resolve interfaces that are
            // themselves CsWinRT-projected (IInspectable-based), and this interop
            // interface is a classic IUnknown-derived COM interface with no
            // projection (see microsoft/CsWinRT#959 - unsupported by design).
            // Querying the compositor's underlying native pointer directly and
            // wrapping the result as a plain COM RCW sidesteps CsWinRT entirely.
            var nativeObject = ((IWinRTObject)Compositor).NativeObject;
            var interopGuid = typeof(ICompositorDesktopInterop).GUID;

            if (nativeObject.TryAs(interopGuid, out IntPtr interopPtr) < 0 || interopPtr == IntPtr.Zero)
            {
                Log.Playback.Error("Compositor does not support ICompositorDesktopInterop");
                return null;
            }

            try
            {
                var interop = (ICompositorDesktopInterop)Marshal.GetTypedObjectForIUnknown(
                    interopPtr, typeof(ICompositorDesktopInterop));

                interop.CreateDesktopWindowTarget(hwnd, false, out var raw);
                if (raw == IntPtr.Zero)
                {
                    return null;
                }

                // The reference handed back is owned by the returned projection for
                // the lifetime of the window; targets are created once per wallpaper
                // window and released when the process exits.
                return MarshalInspectable<DesktopWindowTarget>.FromAbi(raw);
            }
            finally
            {
                Marshal.Release(interopPtr);
            }
        }
        catch (Exception error)
        {
            Log.Playback.Error($"Composition target creation failed: {error.GetType().FullName} 0x{error.HResult:X8} {error.Message}\n{error.StackTrace}");
            return null;
        }
    }

    private static void EnsureDispatcherQueue()
    {
        if (_dispatcherQueueController != IntPtr.Zero)
        {
            return;
        }

        var options = new NativeMethods.DispatcherQueueOptions
        {
            dwSize = Marshal.SizeOf<NativeMethods.DispatcherQueueOptions>(),
            threadType = NativeMethods.DQTYPE_THREAD_CURRENT,
            apartmentType = NativeMethods.DQTAT_COM_NONE
        };

        var hr = NativeMethods.CreateDispatcherQueueController(options, out _dispatcherQueueController);
        if (hr < 0)
        {
            // A queue may already exist on this thread, which is not an error.
            Log.Lifecycle.Info($"CreateDispatcherQueueController returned 0x{hr:X8}");
        }
    }
}
