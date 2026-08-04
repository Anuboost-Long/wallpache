using System;
using System.Runtime.InteropServices;
using Wallpache.App.Interop;
using Wallpache.App.Support;

namespace Wallpache.App.System;

/// <summary>The power inputs the playback policy reads.</summary>
public interface IPowerConditions
{
    bool IsBatterySaverEnabled { get; }

    bool IsOnBatteryPower { get; }
}

/// <summary>
/// Observes sleep, wake, screen lock, and battery state so playback can follow
/// the machine's state. Callbacks are delivered on the UI thread, because the
/// message window lives there.
/// </summary>
public sealed class PowerAndSessionMonitor : IPowerConditions, IDisposable
{
    /// <summary>Battery Saver is on. Matches <c>SYSTEM_POWER_STATUS.SystemStatusFlag</c>.</summary>
    private const byte BatterySaverOn = 1;

    private const byte AcLineOffline = 0;
    private const byte BatteryFlagNoBattery = 128;

    public event Action? WillSleep;
    public event Action? DidWake;
    public event Action<bool>? SessionLockChanged;
    public event Action? PowerConditionsChanged;
    public event Action? DisplaysMayHaveChanged;

    private readonly SystemEventPump _pump;
    private IntPtr _monitorPowerRegistration;
    private IntPtr _powerSchemeRegistration;
    private bool _started;

    public PowerAndSessionMonitor(SystemEventPump pump)
    {
        _pump = pump;
    }

    public bool IsBatterySaverEnabled => ReadPowerStatus() is { } status && status.SystemStatusFlag == BatterySaverOn;

    public bool IsOnBatteryPower => ReadPowerStatus() is { } status
        && status.ACLineStatus == AcLineOffline
        && (status.BatteryFlag & BatteryFlagNoBattery) == 0;

    public void Start()
    {
        if (_started || _pump.Handle == IntPtr.Zero)
        {
            return;
        }

        _started = true;
        _pump.Message += OnMessage;

        // Fast user switching hides the desktop just as a lock does, so both are
        // routed through the same notification.
        if (!NativeMethods.WTSRegisterSessionNotification(_pump.Handle, NativeMethods.NOTIFY_FOR_THIS_SESSION))
        {
            Log.Lifecycle.Error($"Session notifications unavailable: {Marshal.GetLastWin32Error()}");
        }

        _monitorPowerRegistration = NativeMethods.RegisterPowerSettingNotification(
            _pump.Handle,
            ref NativeMethods.GuidMonitorPowerOn,
            NativeMethods.DEVICE_NOTIFY_WINDOW_HANDLE);

        _powerSchemeRegistration = NativeMethods.RegisterPowerSettingNotification(
            _pump.Handle,
            ref NativeMethods.GuidPowerSchemePersonality,
            NativeMethods.DEVICE_NOTIFY_WINDOW_HANDLE);
    }

    public void Stop()
    {
        if (!_started)
        {
            return;
        }

        _started = false;
        _pump.Message -= OnMessage;

        if (_pump.Handle != IntPtr.Zero)
        {
            NativeMethods.WTSUnRegisterSessionNotification(_pump.Handle);
        }

        if (_monitorPowerRegistration != IntPtr.Zero)
        {
            NativeMethods.UnregisterPowerSettingNotification(_monitorPowerRegistration);
            _monitorPowerRegistration = IntPtr.Zero;
        }

        if (_powerSchemeRegistration != IntPtr.Zero)
        {
            NativeMethods.UnregisterPowerSettingNotification(_powerSchemeRegistration);
            _powerSchemeRegistration = IntPtr.Zero;
        }
    }

    public void Dispose() => Stop();

    // MARK: - Private

    private void OnMessage(uint message, IntPtr wParam, IntPtr lParam)
    {
        switch (message)
        {
            case NativeMethods.WM_POWERBROADCAST:
                HandlePowerBroadcast(wParam, lParam);
                break;

            case NativeMethods.WM_WTSSESSION_CHANGE:
                HandleSessionChange(wParam.ToInt32());
                break;

            case NativeMethods.WM_DISPLAYCHANGE:
            case NativeMethods.WM_DEVICECHANGE:
                DisplaysMayHaveChanged?.Invoke();
                break;

            // WM_SETTINGCHANGE is broadcast for everything from theme to policy
            // changes; only the work-area notification means the desktop layout
            // moved, and re-enumerating displays on the rest would be constant.
            case NativeMethods.WM_SETTINGCHANGE when wParam.ToInt32() == NativeMethods.SPI_SETWORKAREA:
                DisplaysMayHaveChanged?.Invoke();
                break;
        }
    }

    private void HandlePowerBroadcast(IntPtr wParam, IntPtr lParam)
    {
        switch (wParam.ToInt32())
        {
            case NativeMethods.PBT_APMSUSPEND:
                Log.Lifecycle.Info("System will sleep");
                WillSleep?.Invoke();
                break;

            case NativeMethods.PBT_APMRESUMESUSPEND:
            case NativeMethods.PBT_APMRESUMEAUTOMATIC:
                Log.Lifecycle.Info("System did wake");
                DidWake?.Invoke();
                break;

            case NativeMethods.PBT_POWERSETTINGCHANGE:
                HandlePowerSettingChange(lParam);
                break;
        }
    }

    /// <summary>
    /// Display power-off is treated as a sleep for playback purposes: decoding
    /// frames nobody can see is the clearest waste a wallpaper can commit.
    /// </summary>
    private void HandlePowerSettingChange(IntPtr lParam)
    {
        if (lParam == IntPtr.Zero)
        {
            PowerConditionsChanged?.Invoke();
            return;
        }

        var setting = Marshal.PtrToStructure<NativeMethods.POWERBROADCAST_SETTING>(lParam);

        if (setting.PowerSetting == NativeMethods.GuidMonitorPowerOn)
        {
            var displaysOn = setting.Data != 0;
            Log.Lifecycle.Info($"Displays powered {(displaysOn ? "on" : "off")}");

            if (displaysOn)
            {
                DidWake?.Invoke();
            }
            else
            {
                WillSleep?.Invoke();
            }

            return;
        }

        PowerConditionsChanged?.Invoke();
    }

    private void HandleSessionChange(int reason)
    {
        switch (reason)
        {
            case NativeMethods.WTS_SESSION_LOCK:
            case NativeMethods.WTS_CONSOLE_DISCONNECT:
                Log.Lifecycle.Info("Session locked");
                SessionLockChanged?.Invoke(true);
                break;

            case NativeMethods.WTS_SESSION_UNLOCK:
            case NativeMethods.WTS_CONSOLE_CONNECT:
                Log.Lifecycle.Info("Session unlocked");
                SessionLockChanged?.Invoke(false);
                break;
        }
    }

    private static NativeMethods.SYSTEM_POWER_STATUS? ReadPowerStatus() =>
        NativeMethods.GetSystemPowerStatus(out var status) ? status : null;
}
