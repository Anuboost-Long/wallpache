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
    // Explorer recreates its dedicated wallpaper WorkerW - the window the app's
    // own content is parented into - as a side effect of the desktop picture
    // changing, not just of Explorer restarting. That happens roughly one to
    // two seconds after the picture is set, with no broadcast announcing it, so
    // a slow poll left the wallpaper looking frozen for up to its own interval
    // every time "Match desktop picture" fired. This has to be short enough
    // that the gap is not the dominant part of the pause; the check itself is a
    // single IsWindow call, cheap enough that polling it this often costs
    // nothing worth trading away for.
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);
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
        _poll.Tick += (_, _) => CheckNow();
    }

    /// <summary>
    /// Checks immediately rather than waiting for the next poll tick. Callers
    /// that just did something known to make Explorer rebuild the wallpaper
    /// host - such as changing the desktop picture - can use this to catch the
    /// resulting rebuild sooner than even a short poll interval would.
    /// </summary>
    public void CheckNow()
    {
        // With nothing attached there is nothing to repair, and asking
        // Explorer to rebuild its desktop windows for an idle app is exactly
        // the kind of cost an all-day tray app must not carry.
        if (!HasActiveWallpapers || _isHostValid())
        {
            return;
        }

        Log.Desktop.Info("Desktop host handle went stale");
        DesktopHostInvalidated?.Invoke();
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
