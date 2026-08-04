using System;
using System.Runtime.InteropServices;

namespace Wallpache.App.Interop;

/// <summary>
/// Every Win32 entry point the app uses. Kept in one file so the P/Invoke
/// surface stays reviewable, and so a Windows behaviour change has a single
/// place to be adjusted.
/// </summary>
internal static class NativeMethods
{
    // MARK: - Window messages

    internal const int WM_DESTROY = 0x0002;
    internal const int WM_ERASEBKGND = 0x0014;
    internal const int WM_SETTINGCHANGE = 0x001A;
    internal const int WM_NCHITTEST = 0x0084;
    internal const int WM_DISPLAYCHANGE = 0x007E;
    internal const int WM_MOUSEACTIVATE = 0x0021;
    internal const int WM_POWERBROADCAST = 0x0218;
    internal const int WM_DEVICECHANGE = 0x0219;
    internal const int WM_WTSSESSION_CHANGE = 0x02B1;
    internal const int WM_DPICHANGED = 0x02E0;

    /// <summary>The <c>WM_SETTINGCHANGE</c> action that means the desktop layout moved.</summary>
    internal const int SPI_SETWORKAREA = 0x002F;

    /// <summary>Undocumented Progman message that asks Explorer to spawn the wallpaper WorkerW.</summary>
    internal const int WM_SPAWN_WORKER = 0x052C;

    internal const int HTTRANSPARENT = -1;
    internal const int MA_NOACTIVATE = 3;

    // Power notifications
    internal const int PBT_APMSUSPEND = 0x0004;
    internal const int PBT_APMRESUMESUSPEND = 0x0007;
    internal const int PBT_APMRESUMEAUTOMATIC = 0x0012;
    internal const int PBT_POWERSETTINGCHANGE = 0x8013;

    // Session notifications
    internal const int NOTIFY_FOR_THIS_SESSION = 0;
    internal const int WTS_SESSION_LOCK = 0x7;
    internal const int WTS_SESSION_UNLOCK = 0x8;
    internal const int WTS_CONSOLE_DISCONNECT = 0x3;
    internal const int WTS_CONSOLE_CONNECT = 0x1;

    // MARK: - Window styles

    internal const int GWL_STYLE = -16;
    internal const int GWL_EXSTYLE = -20;

    internal const uint WS_CHILD = 0x40000000;
    internal const uint WS_VISIBLE = 0x10000000;
    internal const uint WS_POPUP = 0x80000000;
    internal const uint WS_CLIPSIBLINGS = 0x04000000;
    internal const uint WS_CLIPCHILDREN = 0x02000000;
    internal const uint WS_DISABLED = 0x08000000;

    internal const uint WS_EX_TOOLWINDOW = 0x00000080;
    internal const uint WS_EX_NOACTIVATE = 0x08000000;
    internal const uint WS_EX_TRANSPARENT = 0x00000020;
    internal const uint WS_EX_APPWINDOW = 0x00040000;
    internal const uint WS_EX_NOREDIRECTIONBITMAP = 0x00200000;

    internal const uint SWP_NOSIZE = 0x0001;
    internal const uint SWP_NOMOVE = 0x0002;
    internal const uint SWP_NOZORDER = 0x0004;
    internal const uint SWP_NOACTIVATE = 0x0010;
    internal const uint SWP_SHOWWINDOW = 0x0040;
    internal const uint SWP_FRAMECHANGED = 0x0020;

    internal static readonly IntPtr HWND_BOTTOM = new(1);
    internal static readonly IntPtr HWND_MESSAGE = new(-3);

    internal const uint SMTO_ABORTIFHUNG = 0x0002;
    internal const uint SMTO_NORMAL = 0x0000;

    internal const int SW_HIDE = 0;
    internal const int SW_SHOWNA = 8;

    // MARK: - Delegates

    internal delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    internal delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    internal delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, IntPtr lprcMonitor, IntPtr dwData);

    // MARK: - Structures

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public int Width => Right - Left;

        public int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct DISPLAY_DEVICE
    {
        public int cb;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceString;

        public uint StateFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceID;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceKey;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct WNDCLASSEX
    {
        public int cbSize;
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string? lpszMenuName;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string lpszClassName;

        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SYSTEM_POWER_STATUS
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }

    internal const uint MONITORINFOF_PRIMARY = 0x00000001;
    internal const uint DISPLAY_DEVICE_ATTACHED_TO_DESKTOP = 0x00000001;

    // MARK: - Window management

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern IntPtr CreateWindowEx(
        uint dwExStyle,
        string lpClassName,
        string? lpWindowName,
        uint dwStyle,
        int x,
        int y,
        int nWidth,
        int nHeight,
        IntPtr hWndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetParent(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int X,
        int Y,
        int cx,
        int cy,
        uint uFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    /// <summary>32-bit Windows has no <c>GetWindowLongPtr</c>; the 32-bit entry point is the same call.</summary>
    internal static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex) =>
        IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : new IntPtr(GetWindowLong32(hWnd, nIndex));

    internal static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong) =>
        IntPtr.Size == 8
            ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong)
            : new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern IntPtr FindWindowEx(
        IntPtr hwndParent,
        IntPtr hwndChildAfter,
        string? lpszClass,
        string? lpszWindow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern int GetClassName(IntPtr hWnd, [Out] char[] lpClassName, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern IntPtr SendMessageTimeout(
        IntPtr hWnd,
        uint Msg,
        IntPtr wParam,
        IntPtr lParam,
        uint fuFlags,
        uint uTimeout,
        out IntPtr lpdwResult);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern uint RegisterWindowMessage(string lpString);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, [MarshalAs(UnmanagedType.Bool)] bool bErase);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern IntPtr GetModuleHandle(string? lpModuleName);

    // MARK: - Monitors

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumDisplayDevices(
        string? lpDevice,
        uint iDevNum,
        ref DISPLAY_DEVICE lpDisplayDevice,
        uint dwFlags);

    [DllImport("shcore.dll")]
    internal static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    internal const int MDT_EFFECTIVE_DPI = 0;

    // MARK: - Power and session

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS lpSystemPowerStatus);

    [DllImport("wtsapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool WTSRegisterSessionNotification(IntPtr hWnd, uint dwFlags);

    [DllImport("wtsapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool WTSUnRegisterSessionNotification(IntPtr hWnd);

    [DllImport("user32.dll")]
    internal static extern IntPtr RegisterPowerSettingNotification(IntPtr hRecipient, ref Guid PowerSettingGuid, uint Flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnregisterPowerSettingNotification(IntPtr Handle);

    internal const uint DEVICE_NOTIFY_WINDOW_HANDLE = 0x00000000;

    /// <summary>GUID_MONITOR_POWER_ON: fires when the displays are switched off or on.</summary>
    internal static Guid GuidMonitorPowerOn = new("02731015-4510-4526-99E6-E5A17EBD1AEA");

    /// <summary>GUID_POWERSCHEME_PERSONALITY: also fires when Battery Saver toggles.</summary>
    internal static Guid GuidPowerSchemePersonality = new("245D8541-3943-4422-B025-13A784F679B7");

    [StructLayout(LayoutKind.Sequential)]
    internal struct POWERBROADCAST_SETTING
    {
        public Guid PowerSetting;
        public uint DataLength;
        public byte Data;
    }

    // MARK: - Desktop wallpaper (still image)

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SystemParametersInfo(uint uiAction, uint uiParam, string pvParam, uint fWinIni);

    internal const uint SPI_SETDESKWALLPAPER = 0x0014;
    internal const uint SPIF_UPDATEINIFILE = 0x01;
    internal const uint SPIF_SENDCHANGE = 0x02;

    // MARK: - Accessibility

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfoBool(
        uint uiAction,
        uint uiParam,
        [MarshalAs(UnmanagedType.Bool)] out bool pvParam,
        uint fWinIni);

    private const uint SPI_GETCLIENTAREAANIMATION = 0x1042;

    /// <summary>
    /// Whether the user allows animations. "Show animations in Windows" being off
    /// is the Windows equivalent of Reduce Motion, so anything that would start
    /// moving on its own waits for an explicit action instead.
    /// </summary>
    internal static bool AreClientAreaAnimationsEnabled()
    {
        try
        {
            return !SystemParametersInfoBool(SPI_GETCLIENTAREAANIMATION, 0, out var enabled, 0) || enabled;
        }
        catch (Exception)
        {
            return true;
        }
    }

    // MARK: - Composition

    [DllImport("CoreMessaging.dll")]
    internal static extern int CreateDispatcherQueueController(
        DispatcherQueueOptions options,
        out IntPtr dispatcherQueueController);

    [StructLayout(LayoutKind.Sequential)]
    internal struct DispatcherQueueOptions
    {
        public int dwSize;
        public int threadType;
        public int apartmentType;
    }

    internal const int DQTYPE_THREAD_CURRENT = 2;

    /// <summary>
    /// The UI thread is already <c>[STAThread]</c>, so the dispatcher queue must
    /// not try to initialise the apartment itself: doing so fails with
    /// <c>RPC_E_CHANGED_MODE</c> and the compositor never comes up.
    /// </summary>
    internal const int DQTAT_COM_NONE = 0;
}
