using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia.Threading;
using Wallpache.App.Interop;
using Wallpache.App.Support;

namespace Wallpache.App.Displays;

/// <summary>
/// Enumerates displays and reports changes.
///
/// Docking, undocking, and waking emit several <c>WM_DISPLAYCHANGE</c> and
/// <c>WM_SETTINGCHANGE</c> messages in quick succession, so changes are
/// debounced into one reconcile pass.
/// </summary>
public sealed class DisplayManager : IDisposable
{
    private static readonly TimeSpan DebounceInterval = TimeSpan.FromMilliseconds(500);

    public event Action<IReadOnlyList<DisplayDescriptor>>? DisplaysChanged;

    private readonly DispatcherTimer _debounce;
    private bool _started;

    public DisplayManager()
    {
        _debounce = new DispatcherTimer { Interval = DebounceInterval };
        _debounce.Tick += (_, _) =>
        {
            _debounce.Stop();
            Refresh();
        };
    }

    public IReadOnlyList<DisplayDescriptor> Displays { get; private set; } = [];

    public void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        Displays = CurrentDisplays();
    }

    public void Stop()
    {
        _debounce.Stop();
        _started = false;
    }

    /// <summary>
    /// Called by the message window for every display-related message. Several
    /// arrive per change, which is exactly what the debounce is for.
    /// </summary>
    public void ScheduleReconcile()
    {
        _debounce.Stop();
        _debounce.Start();
    }

    /// <summary>
    /// Refreshes the cached list immediately and notifies if it changed. Used
    /// after wake, where the messages may already have been coalesced.
    /// </summary>
    public void Refresh()
    {
        var updated = CurrentDisplays();
        if (AreEquivalent(Displays, updated))
        {
            // Handles still need refreshing even when the arrangement is
            // unchanged: they are invalidated by every reconfiguration.
            Displays = updated;
            return;
        }

        Displays = updated;
        Log.Displays.Info($"Displays changed: {string.Join(", ", updated.Select(display => display.Name))}");
        DisplaysChanged?.Invoke(updated);
    }

    public void Dispose() => Stop();

    /// <summary>
    /// Enumerates monitors through the Win32 API rather than an array index, so
    /// identifiers stay attached to physical hardware.
    /// </summary>
    public static IReadOnlyList<DisplayDescriptor> CurrentDisplays()
    {
        var identities = DisplayNameResolver.ResolveAll();
        var monitors = new List<DisplayDescriptor>();

        NativeMethods.MonitorEnumProc callback = (monitor, _, _, _) =>
        {
            var info = new NativeMethods.MONITORINFOEX
            {
                cbSize = Marshal.SizeOf<NativeMethods.MONITORINFOEX>(),
                szDevice = string.Empty
            };

            if (!NativeMethods.GetMonitorInfo(monitor, ref info))
            {
                return true;
            }

            var deviceName = info.szDevice ?? string.Empty;
            identities.TryGetValue(deviceName, out var identity);

            var name = !string.IsNullOrWhiteSpace(identity.FriendlyName)
                ? identity.FriendlyName
                : FallbackName(deviceName);

            // Prefer the monitor's device interface path: it follows the panel
            // across reordering, where \\.\DISPLAYn follows the slot.
            var id = !string.IsNullOrWhiteSpace(identity.DevicePath) ? identity.DevicePath : deviceName;

            monitors.Add(new DisplayDescriptor(
                Id: id,
                MonitorHandle: monitor,
                DeviceName: deviceName,
                Name: name,
                X: info.rcMonitor.Left,
                Y: info.rcMonitor.Top,
                Width: info.rcMonitor.Width,
                Height: info.rcMonitor.Height,
                IsPrimary: (info.dwFlags & NativeMethods.MONITORINFOF_PRIMARY) != 0,
                ScaleFactor: ScaleFactor(monitor)));

            return true;
        };

        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
        GC.KeepAlive(callback);

        // A stable order keeps the Displays tab from reshuffling on every change.
        return monitors
            .OrderByDescending(display => display.IsPrimary)
            .ThenBy(display => display.X)
            .ThenBy(display => display.Y)
            .ToList();
    }

    // MARK: - Private

    private static bool AreEquivalent(IReadOnlyList<DisplayDescriptor> left, IReadOnlyList<DisplayDescriptor> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Count; index++)
        {
            if (!left[index].IsEquivalentTo(right[index]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Turns <c>\\.\DISPLAY2</c> into "Display 2" when no marketing name exists.</summary>
    private static string FallbackName(string deviceName)
    {
        var trimmed = deviceName.TrimStart('\\', '.');
        if (trimmed.StartsWith("DISPLAY", StringComparison.OrdinalIgnoreCase))
        {
            return $"Display {trimmed[7..]}";
        }

        return string.IsNullOrWhiteSpace(trimmed) ? "Display" : trimmed;
    }

    private static double ScaleFactor(IntPtr monitor)
    {
        try
        {
            if (NativeMethods.GetDpiForMonitor(monitor, NativeMethods.MDT_EFFECTIVE_DPI, out var dpiX, out _) == 0
                && dpiX > 0)
            {
                return dpiX / 96.0;
            }
        }
        catch (DllNotFoundException)
        {
            // shcore.dll is present on every supported release; the guard is for
            // the rare stripped image.
        }

        return 1.0;
    }
}
