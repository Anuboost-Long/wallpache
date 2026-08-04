using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Wallpache.App.Support;

namespace Wallpache.App.Displays;

/// <summary>
/// Maps a GDI device name such as <c>\\.\DISPLAY1</c> onto the monitor's
/// marketing name and its device interface path, using the Connecting and
/// Configuring Displays API.
///
/// <c>EnumDisplayDevices</c> alone reports "Generic PnP Monitor" for most
/// hardware, which is useless in a list of three displays, so the friendly name
/// is read from the display configuration instead.
/// </summary>
internal static class DisplayNameResolver
{
    internal readonly record struct MonitorIdentity(string FriendlyName, string DevicePath);

    private const uint QDC_ONLY_ACTIVE_PATHS = 0x00000002;
    private const uint DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME = 1;
    private const uint DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME = 2;
    private const int ERROR_SUCCESS = 0;

    /// <summary>
    /// Reads the whole active display configuration once. Callers look up by GDI
    /// device name; a missing entry simply means no better name was available.
    /// </summary>
    internal static Dictionary<string, MonitorIdentity> ResolveAll()
    {
        var result = new Dictionary<string, MonitorIdentity>(StringComparer.OrdinalIgnoreCase);

        try
        {
            if (GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, out var pathCount, out var modeCount) != ERROR_SUCCESS)
            {
                return result;
            }

            var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
            var modes = new byte[modeCount * ModeInfoSize];

            if (QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero)
                != ERROR_SUCCESS)
            {
                return result;
            }

            for (var index = 0; index < pathCount; index++)
            {
                var path = paths[index];

                var sourceName = QuerySourceName(path);
                if (string.IsNullOrEmpty(sourceName))
                {
                    continue;
                }

                var target = QueryTargetName(path);
                result[sourceName] = target;
            }
        }
        catch (Exception error)
        {
            Log.Displays.Error($"Display configuration query failed: {error.Message}");
        }

        return result;
    }

    private static string? QuerySourceName(DISPLAYCONFIG_PATH_INFO path)
    {
        var request = new DISPLAYCONFIG_SOURCE_DEVICE_NAME
        {
            header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
            {
                type = DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME,
                size = Marshal.SizeOf<DISPLAYCONFIG_SOURCE_DEVICE_NAME>(),
                adapterId = path.sourceInfo.adapterId,
                id = path.sourceInfo.id
            }
        };

        return DisplayConfigGetDeviceInfo(ref request) == ERROR_SUCCESS ? request.viewGdiDeviceName : null;
    }

    private static MonitorIdentity QueryTargetName(DISPLAYCONFIG_PATH_INFO path)
    {
        var request = new DISPLAYCONFIG_TARGET_DEVICE_NAME
        {
            header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
            {
                type = DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME,
                size = Marshal.SizeOf<DISPLAYCONFIG_TARGET_DEVICE_NAME>(),
                adapterId = path.targetInfo.adapterId,
                id = path.targetInfo.id
            }
        };

        if (DisplayConfigGetDeviceInfo(ref request) != ERROR_SUCCESS)
        {
            return new MonitorIdentity(string.Empty, string.Empty);
        }

        return new MonitorIdentity(
            request.monitorFriendlyDeviceName ?? string.Empty,
            request.monitorDevicePath ?? string.Empty);
    }

    // MARK: - Interop

    [StructLayout(LayoutKind.Sequential)]
    private struct LUID
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_PATH_SOURCE_INFO
    {
        public LUID adapterId;
        public uint id;
        public uint modeInfoIdx;
        public uint statusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_RATIONAL
    {
        public uint Numerator;
        public uint Denominator;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_PATH_TARGET_INFO
    {
        public LUID adapterId;
        public uint id;
        public uint modeInfoIdx;
        public uint outputTechnology;
        public uint rotation;
        public uint scaling;
        public DISPLAYCONFIG_RATIONAL refreshRate;
        public uint scanLineOrdering;

        /// <summary>A Win32 <c>BOOL</c>; kept as an integer so the path array stays blittable.</summary>
        public int targetAvailable;

        public uint statusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_PATH_INFO
    {
        public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo;
        public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo;
        public uint flags;
    }

    /// <summary>
    /// Nothing here reads the mode table, so it is passed as an opaque buffer of
    /// <c>DISPLAYCONFIG_MODE_INFO</c>-sized elements rather than a marshalled
    /// structure with an unread union.
    /// </summary>
    private const int ModeInfoSize = 64;

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_DEVICE_INFO_HEADER
    {
        public uint type;
        public int size;
        public LUID adapterId;
        public uint id;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DISPLAYCONFIG_SOURCE_DEVICE_NAME
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string viewGdiDeviceName;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DISPLAYCONFIG_TARGET_DEVICE_NAME
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
        public uint flags;
        public uint outputTechnology;
        public ushort edidManufactureId;
        public ushort edidProductCodeId;
        public uint connectorInstance;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string monitorFriendlyDeviceName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string monitorDevicePath;
    }

    [DllImport("user32.dll")]
    private static extern int GetDisplayConfigBufferSizes(uint flags, out uint numPathArrayElements, out uint numModeInfoArrayElements);

    [DllImport("user32.dll")]
    private static extern int QueryDisplayConfig(
        uint flags,
        ref uint numPathArrayElements,
        [Out] DISPLAYCONFIG_PATH_INFO[] pathArray,
        ref uint numModeInfoArrayElements,
        [Out] byte[] modeInfoArray,
        IntPtr currentTopologyId);

    [DllImport("user32.dll")]
    private static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_SOURCE_DEVICE_NAME requestPacket);

    [DllImport("user32.dll")]
    private static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_TARGET_DEVICE_NAME requestPacket);
}
