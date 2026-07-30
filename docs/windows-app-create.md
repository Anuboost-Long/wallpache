# Wallpache for Windows
## Porting Brief and Coding-Agent Prompt

**Existing product:** Wallpache for macOS
**Windows target:** Windows 11
**Recommended stack:** C#, .NET 8, WPF, Win32 interop
**Backend:** None

This document translates the existing macOS product requirements into a Windows-native implementation plan. The Windows version should reproduce the same behavior and user experience; it should not attempt to translate Swift code line by line.


# 1. Copy-Paste Prompt for the Coding Agent

```text
Build a native Windows version of my existing macOS application, Wallpache.

The macOS application is already implemented in Swift using SwiftUI, AppKit, and AVFoundation. Treat the macOS source, the attached requirements document, screenshots, and recordings as the behavioral reference. Do not attempt to compile or directly translate Swift code on Windows.

Create a separate solution named Wallpache.Windows.

Use:
- C#
- .NET 8
- WPF for the app UI and wallpaper windows
- Win32 APIs through P/Invoke for desktop integration, monitor detection, window styles, power events, session lock/unlock, and Explorer recovery
- Windows.Media.Playback.MediaPlayer where practical for video playback
- A Windows system-tray application model
- Local JSON settings and local wallpaper storage
- No Electron
- No backend
- No user accounts
- No video uploads

The Windows version must support:
1. Import local MP4, MOV, and M4V files.
2. Preview an imported video.
3. Apply a muted looping video behind Windows desktop icons.
4. Keep desktop icons clickable.
5. Keep the wallpaper window out of Alt+Tab and the taskbar.
6. Fill, Fit, Stretch, and Center modes.
7. Pause, Resume, Stop, Mute, and playback-rate controls.
8. System-tray controls.
9. Restore the last valid wallpaper configuration after relaunch.
10. Optional launch at sign-in.
11. Pause on sleep and session lock.
12. Resume safely after wake and unlock.
13. Detect display changes.
14. Create one wallpaper session per monitor.
15. Apply one wallpaper to all monitors or separate wallpapers per monitor.
16. Copy imported wallpapers into local application storage.
17. Handle missing, corrupt, unsupported, and deleted files without crashing.
18. Avoid duplicate wallpaper windows and player sessions.
19. Remain lightweight enough to run all day.
20. Never upload user videos.

The Windows wallpaper implementation commonly relies on Explorer windows such as Progman, SHELLDLL_DefView, and WorkerW. Treat this as an implementation-specific compatibility layer. Put every related operation inside DesktopHostService so it can be replaced if a Windows update changes Explorer behavior.

Main components:
- WallpaperCoordinator
- WallpaperSession
- DesktopHostService
- DisplayManager
- VideoPlaybackService
- WallpaperLibraryService
- PowerAndSessionMonitor
- ExplorerMonitor
- StartupService
- SettingsStore
- TrayIconService

Build in phases. The first milestone is only:
- one bundled H.264 MP4
- one monitor
- one borderless WPF window
- attach it behind desktop icons
- muted looping playback
- icons remain clickable
- normal windows remain above it
- wallpaper window is absent from Alt+Tab and taskbar
- exiting Wallpache restores the normal desktop

Before writing the full product, provide:
1. Proposed solution structure.
2. Desktop attachment strategy.
3. Risks of the WorkerW/Explorer technique.
4. Proof-of-concept code.
5. Manual test checklist.

Do not modify the existing macOS project. Create a sibling Windows project or repository and preserve the Wallpache branding, terminology, settings concepts, and user-facing behavior.
```


# 2. What to Give the Agent

Provide the agent with:

- The macOS source repository.
- The original macOS requirements Markdown.
- Screenshots of all screens.
- A short recording of the complete user flow.
- Wallpache icon and brand assets.
- Example wallpaper videos.
- Known bugs and behavior notes.

The DMG is useful only as a visual reference on a Mac. The source project is what allows the agent to understand the existing settings, naming, persistence, and user flow.


# 3. Recommended Windows Stack

| Area | Recommendation |
|---|---|
| Language | C# |
| Runtime | .NET 8 |
| Main UI | WPF |
| Wallpaper window | WPF `Window` with native `HWND` |
| Native integration | Win32 through P/Invoke |
| Video playback | `Windows.Media.Playback.MediaPlayer` |
| Monitor discovery | `EnumDisplayMonitors`, `GetMonitorInfo` |
| Window parenting | `SetParent` |
| Window styles | `GetWindowLongPtr`, `SetWindowLongPtr` |
| Window placement | `SetWindowPos` |
| Tray | `NotifyIcon` or equivalent |
| Power events | `WM_POWERBROADCAST` |
| Lock/unlock | `WTSRegisterSessionNotification` |
| Startup | `StartupTask` when packaged, or per-user startup registration |
| Settings | JSON under Local Application Data |
| Installer | Inno Setup, WiX, MSIX, or portable ZIP |
| Backend | None |

## Why WPF

WinUI 3 is Microsoft's modern recommended UI framework for normal new Windows apps. Wallpache is unusual because the critical work is direct HWND control, Windows message hooks, Explorer desktop parenting, and a tray-first lifecycle.

WPF is the practical first-port choice because it makes these tasks straightforward through `WindowInteropHelper` and `HwndSource`. A later release can use a WinUI 3 settings app with a separate native wallpaper engine, but that adds complexity and is unnecessary for the first Windows version.


# 4. Theory of Operation

Windows does not provide a normal public API that accepts an arbitrary video as the system wallpaper. Wallpache creates a native video window and places it inside or behind the Explorer desktop-window hierarchy.

```text
Normal Windows wallpaper
        ↓
Wallpache video window
        ↓
Desktop icons
        ↓
Normal application windows
```

Typical implementation:

1. Find `Progman`.
2. Request or expose Explorer's worker desktop window.
3. Enumerate top-level windows.
4. Find the window containing `SHELLDLL_DefView`.
5. Identify the associated `WorkerW` host behind desktop icons.
6. Create a borderless WPF window.
7. Obtain its native `HWND`.
8. Attach it to the selected desktop host.
9. Resize it to the target monitor.
10. Apply no-focus and no-task-switch window styles.
11. Start muted looping playback.

Isolate this inside:

```text
DesktopHostService
├── FindProgman()
├── RequestWorkerWindow()
├── FindShellDefView()
├── FindWallpaperHost()
├── AttachWallpaperWindow()
├── ReattachAfterExplorerRestart()
└── DetachWallpaperWindow()
```

The WorkerW arrangement is an Explorer implementation detail, so the app must be prepared to rediscover and reattach after Explorer restarts.


# 5. Core Product Requirements

Wallpache for Windows must:

- Import local videos.
- Preview imported videos.
- Play a selected video behind desktop icons.
- Keep desktop icons interactive.
- Remain below normal application windows.
- Loop continuously.
- Default to muted.
- Support Fill, Fit, Stretch, and Center.
- Support Play, Pause, Resume, Stop, Mute, and playback rate.
- Run primarily from the system tray.
- Restore the previous valid state after relaunch.
- Support multiple monitors.
- Store all data locally.
- Require no backend or account.
- Stop cleanly and reveal the original Windows wallpaper.

Initial guaranteed media target:

```text
Container: MP4
Video codec: H.264
Audio: optional
```

MOV and M4V can be accepted when the installed Windows codecs can decode them. Validate every file before applying it.


# 6. Wallpaper Window Requirements

Every wallpaper window must:

- Be borderless.
- Match one monitor's full bounds.
- Be hidden from the taskbar.
- Be hidden from Alt+Tab.
- Ignore mouse input.
- Never take focus.
- Remain behind desktop icons.
- Be recreated or reattached after Explorer restarts.
- Close when Wallpache stops.

Likely styles to test:

```text
WS_CHILD
WS_VISIBLE
WS_CLIPSIBLINGS
WS_CLIPCHILDREN
WS_EX_TOOLWINDOW
WS_EX_NOACTIVATE
WS_EX_TRANSPARENT
```

Do not apply a style unless its behavior is understood and tested.

Example WPF handle acquisition:

```csharp
public sealed partial class WallpaperWindow : Window
{
    public IntPtr Handle { get; private set; }

    public WallpaperWindow()
    {
        InitializeComponent();

        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Focusable = false;

        SourceInitialized += (_, _) =>
        {
            Handle = new WindowInteropHelper(this).Handle;
            NativeWindowStyles.ConfigureWallpaperWindow(Handle);
        };
    }
}
```


# 7. Playback Design

Preferred model:

```text
Local file
    ↓
MediaSource
    ↓
Windows.Media.Playback.MediaPlayer
    ↓
Wallpaper rendering surface
```

Required settings:

```text
IsMuted = true
IsLoopingEnabled = true
PlaybackRate = 1.0
```

Interface:

```csharp
public interface IVideoPlaybackService : IDisposable
{
    Task LoadAsync(string filePath, CancellationToken cancellationToken);
    void Play();
    void Pause();
    void Stop();
    void SetMuted(bool muted);
    void SetPlaybackRate(double rate);
    void SetScalingMode(ScalingMode mode);
}
```

Do not add FFmpeg or VLC to the proof of concept. Add third-party playback only after a verified native limitation.


# 8. Multiple Monitors

Use Win32 monitor enumeration, not array indexes.

```csharp
public sealed record DisplayDescriptor(
    string Id,
    IntPtr MonitorHandle,
    string DeviceName,
    int X,
    int Y,
    int Width,
    int Height,
    bool IsPrimary
);
```

Use:

- `EnumDisplayMonitors`
- `GetMonitorInfo`
- Stable device names or display configuration paths

When a display change occurs:

1. Debounce the event.
2. Enumerate active displays.
3. Remove sessions for missing displays.
4. Create sessions for new displays.
5. Reposition sessions whose bounds changed.
6. Restore saved assignments.
7. Reattach to the desktop host if necessary.

Listen for:

```text
WM_DISPLAYCHANGE
WM_DEVICECHANGE
WM_SETTINGCHANGE
```


# 9. Sleep, Wake, Lock, and Explorer Recovery

Use `WM_POWERBROADCAST` for power lifecycle events.

```text
Suspend or display off
→ Pause players

Resume
→ Re-enumerate displays
→ Rediscover desktop host
→ Reattach windows
→ Resume eligible players
```

Register for session changes with `WTSRegisterSessionNotification`.

Handle:

```text
WM_WTSSESSION_CHANGE
WTS_SESSION_LOCK
WTS_SESSION_UNLOCK
```

On lock, pause players. On unlock, validate Explorer and displays before resuming.

Explorer can restart independently. Wallpache must detect invalid desktop-host handles, wait for Explorer to return, rediscover the desktop hierarchy, reattach all active wallpaper windows, and resume without requiring a Wallpache restart.


# 10. Local Storage and Settings

Recommended storage:

```text
%LOCALAPPDATA%\Wallpache\
├── Wallpapers\
├── Thumbnails\
├── Logs\
└── settings.json
```

Copy imported files into Wallpache storage by default.

Example settings:

```json
{
  "version": 1,
  "launchAtSignIn": false,
  "energySaving": true,
  "wasPlayingBeforeExit": true,
  "wallpapers": [],
  "displays": [
    {
      "displayId": "\\\\.\\DISPLAY1",
      "wallpaperId": null,
      "scalingMode": "Fill",
      "playbackRate": 1.0,
      "muted": true
    }
  ]
}
```

Write settings atomically using a temporary file and replacement. A damaged settings file must not prevent the app from launching.


# 11. Suggested Solution Structure

```text
Wallpache.Windows/
├── Wallpache.sln
├── src/
│   ├── Wallpache.App/
│   ├── Wallpache.Core/
│   ├── Wallpache.Desktop/
│   ├── Wallpache.Playback/
│   ├── Wallpache.Displays/
│   ├── Wallpache.Library/
│   ├── Wallpache.System/
│   └── Wallpache.Persistence/
├── tests/
│   ├── Wallpache.Core.Tests/
│   ├── Wallpache.Persistence.Tests/
│   └── Wallpache.Desktop.Tests/
├── assets/
│   ├── icon.ico
│   └── sample-wallpapers/
└── docs/
    ├── Windows-Test-Checklist.md
    └── WorkerW-Compatibility.md
```

Main classes:

```text
WallpaperCoordinator
WallpaperSession
DesktopHostService
WallpaperWindow
VideoPlaybackService
DisplayManager
WallpaperLibraryService
PowerAndSessionMonitor
ExplorerMonitor
StartupService
SettingsStore
TrayIconService
```


# 12. Implementation Phases

## Phase 0 — Proof of Concept

- One bundled H.264 MP4.
- One monitor.
- One borderless wallpaper window.
- Desktop-host attachment.
- Muted loop.
- Icons remain clickable.
- Window absent from Alt+Tab and taskbar.
- Normal apps stay above.
- Exiting removes the wallpaper window.

Do not continue until this is reliable.

## Phase 1 — Single-Display MVP

- File picker.
- Video validation.
- Local import.
- Preview.
- Fill and Fit.
- Pause, Resume, Stop.
- Tray controls.
- Persistence.
- Restore after restart.
- Basic power handling.

## Phase 2 — Reliability

- Lock/unlock handling.
- Explorer restart recovery.
- Playback-failure recovery.
- Missing-file handling.
- Duplicate-session prevention.
- Logging.
- Eight-hour memory test.

## Phase 3 — Multiple Displays

- Stable display IDs.
- One session per monitor.
- Same wallpaper on all displays.
- Different wallpaper per display.
- Connection/removal.
- Resolution, orientation, and DPI changes.

## Phase 4 — Product Polish

- Wallpaper library.
- Thumbnails.
- Drag and drop.
- Stretch and Center.
- Playback speed.
- Launch at sign-in.
- Onboarding.
- Accessibility.
- Installer.

## Phase 5 — Release

- Versioned installer or portable package.
- Release notes.
- SHA-256 checksum.
- Website or GitHub download.
- SmartScreen testing.
- Upgrade and uninstall tests.


# 13. Acceptance Criteria for Windows 1.0

- Users can import an H.264 MP4.
- The video appears behind desktop icons.
- Icons remain clickable.
- Normal windows remain above it.
- Wallpaper windows are absent from Alt+Tab and the taskbar.
- Looping is stable.
- Scaling modes work.
- Tray controls work.
- State restores correctly.
- Sleep, wake, lock, and unlock work repeatedly.
- Explorer restart is recovered automatically.
- Display changes do not require an app restart.
- Separate monitor assignments persist.
- Invalid files show clear errors.
- Videos never leave the PC.
- No duplicate sessions are created.
- Memory remains stable during an eight-hour run.
- Uninstalling does not damage the normal Windows wallpaper.


# 14. Windows Test Checklist

Test:

- Desktop icon clicks, right-click, selection rectangle, and Win+D.
- Virtual desktops.
- Taskbar auto-hide.
- Explorer restart.
- One, two, and three monitors.
- Primary monitor changes.
- Portrait orientation.
- Mixed DPI.
- Monitor connection and removal.
- Sleep, wake, hibernate, lock, and unlock.
- H.264 MP4, supported MOV/M4V, corrupt files, short loops, long files, 1080p, and 4K.
- App restart, forced exit, Windows restart, upgrade, and uninstall.


# 15. Final Recommendation

Do not ask the agent to “convert Swift to Windows.”

Ask it to:

> Rebuild Wallpache for Windows, using the macOS app as the behavioral reference.

Recommended structure:

```text
Wallpache/
├── macOS/
│   └── Swift implementation
├── Windows/
│   └── C# implementation
├── Shared/
│   ├── Brand assets
│   ├── Product requirements
│   ├── Wallpaper metadata schema
│   └── Documentation
└── Website/
```

The first milestone is the Windows desktop-host proof of concept, not the settings interface.


# 16. Official Windows References

- Windows App SDK:
  https://learn.microsoft.com/windows/apps/windows-app-sdk/

- WPF:
  https://learn.microsoft.com/dotnet/desktop/wpf/

- SetParent:
  https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-setparent

- Multiple monitor APIs:
  https://learn.microsoft.com/windows/win32/gdi/multiple-display-monitors-functions

- EnumDisplayMonitors:
  https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-enumdisplaymonitors

- Windows media playback:
  https://learn.microsoft.com/windows/apps/develop/ui/controls/media-playback

- MediaPlayer looping:
  https://learn.microsoft.com/uwp/api/windows.media.playback.mediaplayer.isloopingenabled

- Power events:
  https://learn.microsoft.com/windows/win32/power/registering-for-power-events

- WM_POWERBROADCAST:
  https://learn.microsoft.com/windows/win32/power/wm-powerbroadcast-messages

- Session notifications:
  https://learn.microsoft.com/windows/win32/api/wtsapi32/nf-wtsapi32-wtsregistersessionnotification

- StartupTask:
  https://learn.microsoft.com/uwp/api/windows.applicationmodel.startuptask
