using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Wallpache.App.Interop;
using Wallpache.App.Playback;
using Wallpache.App.Support;

namespace Wallpache.App.Desktop;

[ComImport]
[Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDesktopWallpaper
{
    void SetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string? monitorId, [MarshalAs(UnmanagedType.LPWStr)] string wallpaper);

    [return: MarshalAs(UnmanagedType.LPWStr)]
    string GetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string? monitorId);

    [return: MarshalAs(UnmanagedType.LPWStr)]
    string GetMonitorDevicePathAt(uint monitorIndex);

    uint GetMonitorDevicePathCount();

    NativeMethods.RECT GetMonitorRECT([MarshalAs(UnmanagedType.LPWStr)] string monitorId);

    void SetBackgroundColor(uint color);

    uint GetBackgroundColor();

    void SetPosition(int position);

    int GetPosition();

    // The slideshow half of the interface is unused, but the vtable order has to
    // be preserved for the methods above it to be callable.
    void SetSlideshow(IntPtr items);

    IntPtr GetSlideshow();

    void SetSlideshowOptions(int options, uint slideshowTick);

    void GetSlideshowOptions(out int options, out uint slideshowTick);

    void AdvanceSlideshow([MarshalAs(UnmanagedType.LPWStr)] string? monitorId, int direction);

    int GetStatus();

    void Enable([MarshalAs(UnmanagedType.Bool)] bool enable);
}

/// <summary>
/// Sets the real Windows desktop picture so a still image sits underneath the
/// video window.
///
/// The wallpaper window only exists while the app runs. Without this, quitting
/// or pausing reveals whatever unrelated picture Windows was already showing.
/// Pointing the system wallpaper at the video's own still makes that fallback
/// match the live wallpaper instead.
///
/// <c>IDesktopWallpaper</c> is used because it is the only API that addresses a
/// single monitor; <c>SystemParametersInfo</c> is kept as a fallback for the
/// case where the COM object is unavailable, at the cost of applying to every
/// display.
///
/// Every call here runs on a thread-pool thread with its own, uncached COM
/// instance rather than the caller's thread. <c>IDesktopWallpaper</c> is slow
/// enough (Explorer decodes the image and redraws the desktop synchronously
/// inside the call) that running it on the UI thread stalls whatever else that
/// thread is pumping - including the composition dispatcher queue the video
/// surface depends on for its next frame, which is what made the wallpaper
/// visibly pause for the call's duration.
/// </summary>
public sealed class DesktopPictureService
{
    private static readonly Guid DesktopWallpaperClsid = new("C2CF3110-460E-4FC1-B9D0-8A1C0C9CC4BD");

    /// <summary>The picture Windows is showing on <paramref name="displayId"/> right now.</summary>
    public Task<string?> CurrentPictureAsync(string displayId) => Task.Run(() =>
    {
        var wallpaper = CreateInstance();
        if (wallpaper is null)
        {
            return null;
        }

        try
        {
            var path = wallpaper.GetWallpaper(MonitorIdFor(displayId));
            return string.IsNullOrWhiteSpace(path) ? null : path;
        }
        catch (Exception error)
        {
            Log.Desktop.Error($"Reading the desktop picture failed: {error.Message}");
            return null;
        }
    });

    /// <summary>
    /// Points the system wallpaper at <paramref name="imagePath"/>. Returns
    /// <see langword="false"/> when Windows rejects it; a failed desktop picture
    /// is cosmetic, so callers log rather than surface an error.
    /// </summary>
    public Task<bool> SetPictureAsync(string displayId, string imagePath, ScalingMode scalingMode) => Task.Run(() =>
    {
        var wallpaper = CreateInstance();
        if (wallpaper is not null)
        {
            try
            {
                wallpaper.SetPosition(scalingMode.ToDesktopWallpaperPosition());
                wallpaper.SetWallpaper(MonitorIdFor(displayId), imagePath);
                return true;
            }
            catch (Exception error)
            {
                Log.Desktop.Error($"Desktop picture update failed: {error.Message}");
            }
        }

        return SetPictureForAllDisplays(imagePath);
    });

    /// <summary>
    /// The single-display fallback. It cannot target one monitor, so it is only
    /// reached when <c>IDesktopWallpaper</c> could not be created at all.
    /// </summary>
    public static bool SetPictureForAllDisplays(string imagePath)
    {
        try
        {
            return NativeMethods.SystemParametersInfo(
                NativeMethods.SPI_SETDESKWALLPAPER,
                0,
                imagePath,
                NativeMethods.SPIF_UPDATEINIFILE | NativeMethods.SPIF_SENDCHANGE);
        }
        catch (Exception error)
        {
            Log.Desktop.Error($"Desktop picture fallback failed: {error.Message}");
            return false;
        }
    }

    /// <summary>
    /// The monitor device path <c>IDesktopWallpaper</c> expects. Display ids
    /// already are device paths when the display configuration API supplied one.
    /// Anything else cannot be addressed per monitor, and a null id means "every
    /// display", which is the only honest fallback.
    /// </summary>
    private static string? MonitorIdFor(string displayId) =>
        displayId.StartsWith(@"\\?\", StringComparison.Ordinal) ? displayId : null;

    /// <summary>
    /// A fresh instance per call rather than one cached field: this runs on
    /// whichever thread-pool thread <see cref="Task.Run(Func{Task})"/> happens to
    /// use, and a COM object created on the app's own STA UI thread would just
    /// marshal every call back there - defeating the point of running off it.
    /// </summary>
    private static IDesktopWallpaper? CreateInstance()
    {
        try
        {
            var type = Type.GetTypeFromCLSID(DesktopWallpaperClsid);
            return type is null ? null : Activator.CreateInstance(type) as IDesktopWallpaper;
        }
        catch (Exception error)
        {
            Log.Desktop.Error($"IDesktopWallpaper unavailable: {error.Message}");
            return null;
        }
    }
}
