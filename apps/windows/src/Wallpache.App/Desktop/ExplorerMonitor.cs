using System;
using Avalonia.Threading;
using Wallpache.App.Interop;
using Wallpache.App.Support;
using Wallpache.App.System;

namespace Wallpache.App.Desktop;

/// <summary>
/// Detects Explorer restarts.
///
/// Explorer can restart independently of Wallpache — a crash, an update, or the
/// user ending the task. Every desktop-host handle is invalid afterwards, so
/// the app has to rediscover the hierarchy and reattach rather than requiring a
/// restart of its own.
///
/// Two signals are used together: the <c>TaskbarCreated</c> broadcast, which is
/// how a shell client is told Explorer came back, and a slow poll that catches
/// the cases where the broadcast is missed or the host is torn down without one.
/// </summary>
public sealed class ExplorerMonitor : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RestartSettleDelay = TimeSpan.FromMilliseconds(750);

    /// <summary>Raised on the UI thread when the desktop hierarchy needs rebuilding.</summary>
    public event Action? DesktopHostInvalidated;

    private readonly SystemEventPump _pump;
    private readonly Func<bool> _isHostValid;
    private readonly DispatcherTimer _poll;
    private readonly uint _taskbarCreatedMessage;
    private bool _started;

    /// <param name="isHostValid">
    /// Reports whether the current desktop host handle still refers to a live
    /// window. Supplied by the coordinator so this class holds no state of its own.
    /// </param>
    public ExplorerMonitor(SystemEventPump pump, Func<bool> isHostValid)
    {
        _pump = pump;
        _isHostValid = isHostValid;
        _taskbarCreatedMessage = NativeMethods.RegisterWindowMessage("TaskbarCreated");

        _poll = new DispatcherTimer { Interval = PollInterval };
        _poll.Tick += (_, _) =>
        {
            // With nothing attached there is nothing to repair, and asking
            // Explorer to rebuild its desktop windows every few seconds for an
            // idle app is exactly the kind of cost an all-day tray app must not
            // carry.
            if (!HasActiveWallpapers || _isHostValid())
            {
                return;
            }

            Log.Desktop.Info("Desktop host handle went stale");
            DesktopHostInvalidated?.Invoke();
        };
    }

    /// <summary>
    /// Set by the coordinator whenever the number of live sessions changes. The
    /// poll only runs while something is actually attached to the desktop.
    /// </summary>
    public bool HasActiveWallpapers { get; set; }

    public void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        _pump.Message += OnMessage;
        _poll.Start();
    }

    public void Stop()
    {
        if (!_started)
        {
            return;
        }

        _pump.Message -= OnMessage;
        _poll.Stop();
        _started = false;
    }

    private void OnMessage(uint message, IntPtr wParam, IntPtr lParam)
    {
        if (_taskbarCreatedMessage == 0 || message != _taskbarCreatedMessage)
        {
            return;
        }

        Log.Desktop.Info("Explorer restarted");

        // Explorer publishes TaskbarCreated before the desktop hierarchy is
        // fully rebuilt, so the reattach waits a beat rather than racing it.
        DispatcherTimer.RunOnce(() => DesktopHostInvalidated?.Invoke(), RestartSettleDelay);
    }

    public void Dispose() => Stop();
}
