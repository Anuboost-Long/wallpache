using System;
using System.Runtime.InteropServices;
using Wallpache.App.Interop;
using Wallpache.App.Support;

namespace Wallpache.App.Desktop;

/// <summary>Where a wallpaper window was attached, and how it got there.</summary>
public readonly record struct DesktopHost(IntPtr Handle, bool IsProgmanFallback)
{
    public bool IsValid => Handle != IntPtr.Zero && NativeMethods.IsWindow(Handle);
}

/// <summary>
/// The Explorer compatibility layer.
///
/// Windows has no public API that accepts a video as the desktop wallpaper, so
/// Wallpache parents its own window into Explorer's desktop hierarchy, behind
/// the icon view. That hierarchy — <c>Progman</c>, <c>SHELLDLL_DefView</c>, and
/// the <c>WorkerW</c> that hosts the wallpaper — is an implementation detail of
/// Explorer, not a contract.
///
/// Everything that depends on it lives in this one class, so a Windows update
/// that changes the arrangement has exactly one place to be repaired. Callers
/// only ever see "attach this window" and "it is no longer valid".
/// </summary>
public sealed class DesktopHostService
{
    private const uint SendMessageTimeoutMs = 1000;

    private DesktopHost _host;

    /// <summary>The host discovered by the last successful <see cref="Discover"/>.</summary>
    public DesktopHost Host => _host;

    public bool IsHostValid => _host.IsValid;

    /// <summary>
    /// Finds the window that wallpaper content should be parented to, asking
    /// Explorer to create it first if necessary. Returns an invalid host when
    /// Explorer is not currently in a usable state, which is a normal transient
    /// condition during an Explorer restart.
    /// </summary>
    public DesktopHost Discover()
    {
        var progman = FindProgman();
        if (progman == IntPtr.Zero)
        {
            Log.Desktop.Error("Progman not found; Explorer may be restarting");
            _host = default;
            return _host;
        }

        RequestWorkerWindow(progman);

        var workerW = FindWallpaperHost();
        if (workerW != IntPtr.Zero)
        {
            _host = new DesktopHost(workerW, IsProgmanFallback: false);
            return _host;
        }

        // Some configurations never spawn a separate WorkerW. Progman itself
        // hosts the icon view there, so parenting to it and sinking to the
        // bottom of the z-order produces the same result.
        Log.Desktop.Info("No WorkerW found; falling back to Progman");
        _host = new DesktopHost(progman, IsProgmanFallback: true);
        return _host;
    }

    /// <summary>Re-runs discovery, e.g. after Explorer restarted or the machine woke.</summary>
    public DesktopHost Rediscover()
    {
        _host = default;
        return Discover();
    }

    public static IntPtr FindProgman() => NativeMethods.FindWindow("Progman", null);

    /// <summary>
    /// Asks Explorer to split the desktop into a wallpaper <c>WorkerW</c> and an
    /// icon view. The message is undocumented and returns nothing useful, so the
    /// result is checked by looking for the window afterwards.
    /// </summary>
    public static void RequestWorkerWindow(IntPtr progman)
    {
        // The 0x052C payloads differ between Windows 10 and Windows 11 builds;
        // sending each in turn costs a few milliseconds and covers both.
        ReadOnlySpan<(int WParam, int LParam)> payloads = [(0, 0), (0x0000000D, 0x00000000), (0x0000000D, 0x00000001)];

        foreach (var (wParam, lParam) in payloads)
        {
            NativeMethods.SendMessageTimeout(
                progman,
                NativeMethods.WM_SPAWN_WORKER,
                new IntPtr(wParam),
                new IntPtr(lParam),
                NativeMethods.SMTO_NORMAL | NativeMethods.SMTO_ABORTIFHUNG,
                SendMessageTimeoutMs,
                out _);
        }
    }

    /// <summary>The top-level window that owns the desktop icon view.</summary>
    public static IntPtr FindShellDefViewOwner()
    {
        var owner = IntPtr.Zero;

        NativeMethods.EnumWindowsProc callback = (window, _) =>
        {
            if (NativeMethods.FindWindowEx(window, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero)
            {
                owner = window;
                return false;
            }

            return true;
        };

        NativeMethods.EnumWindows(callback, IntPtr.Zero);
        GC.KeepAlive(callback);
        return owner;
    }

    /// <summary>
    /// The <c>WorkerW</c> that sits immediately behind the icon view. Content
    /// parented here is drawn above the static wallpaper and below the icons.
    /// </summary>
    public static IntPtr FindWallpaperHost()
    {
        var defViewOwner = FindShellDefViewOwner();
        if (defViewOwner == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        // The sibling that follows the icon view in z-order is the one Explorer
        // paints the wallpaper into.
        return NativeMethods.FindWindowEx(IntPtr.Zero, defViewOwner, "WorkerW", null);
    }

    /// <summary>
    /// Parents <paramref name="window"/> into the desktop host and places it at
    /// <paramref name="bounds"/> in virtual-screen coordinates.
    /// </summary>
    internal bool Attach(IntPtr window, NativeMethods.RECT bounds)
    {
        if (!_host.IsValid)
        {
            Discover();
        }

        if (!_host.IsValid)
        {
            return false;
        }

        if (NativeMethods.SetParent(window, _host.Handle) == IntPtr.Zero
            && Marshal.GetLastWin32Error() != 0)
        {
            Log.Desktop.Error($"SetParent failed: {Marshal.GetLastWin32Error()}");
            return false;
        }

        Position(window, bounds);
        return true;
    }

    /// <summary>
    /// Places the window over one monitor. Child coordinates are relative to the
    /// host, which spans the whole virtual desktop and shares its origin, so the
    /// host's own rectangle is what the monitor bounds are offset against.
    /// </summary>
    internal void Position(IntPtr window, NativeMethods.RECT bounds)
    {
        var originX = 0;
        var originY = 0;

        if (_host.IsValid && NativeMethods.GetWindowRect(_host.Handle, out var hostRect))
        {
            originX = hostRect.Left;
            originY = hostRect.Top;
        }

        NativeMethods.SetWindowPos(
            window,
            NativeMethods.HWND_BOTTOM,
            bounds.Left - originX,
            bounds.Top - originY,
            bounds.Width,
            bounds.Height,
            NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
    }

    /// <summary>
    /// Re-asserts placement without re-parenting. Cheap and idempotent, so it
    /// can run after every wake, display change, and policy transition.
    /// </summary>
    internal void ReassertPlacement(IntPtr window, NativeMethods.RECT bounds)
    {
        if (!NativeMethods.IsWindow(window))
        {
            return;
        }

        var parent = NativeMethods.GetParent(window);
        if (!_host.IsValid || parent != _host.Handle)
        {
            Attach(window, bounds);
            return;
        }

        Position(window, bounds);
    }

    /// <summary>
    /// Returns the window to the desktop so it can be destroyed without leaving
    /// a child behind in Explorer's tree.
    /// </summary>
    public static void Detach(IntPtr window)
    {
        if (!NativeMethods.IsWindow(window))
        {
            return;
        }

        NativeMethods.ShowWindow(window, NativeMethods.SW_HIDE);
        NativeMethods.SetParent(window, IntPtr.Zero);
    }

    /// <summary>
    /// Asks Explorer to repaint the desktop. Used when the last wallpaper window
    /// goes away, so the user's own wallpaper is visible again immediately.
    /// </summary>
    public static void RefreshDesktop()
    {
        var progman = FindProgman();
        if (progman != IntPtr.Zero)
        {
            NativeMethods.InvalidateRect(progman, IntPtr.Zero, true);
        }

        var worker = FindWallpaperHost();
        if (worker != IntPtr.Zero)
        {
            NativeMethods.InvalidateRect(worker, IntPtr.Zero, true);
        }
    }
}
