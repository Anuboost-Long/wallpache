# WorkerW / Explorer Compatibility

Windows has no public API that accepts a video as the desktop wallpaper.
Wallpache creates its own native window per monitor and parents it into
Explorer's desktop hierarchy, behind the icon view.

```text
Normal Windows wallpaper
        ↓
Wallpache video window        ← what this document is about
        ↓
Desktop icons (SHELLDLL_DefView)
        ↓
Normal application windows
```

Every operation that depends on Explorer's internals lives in
`Desktop/DesktopHostService.cs`. If a Windows update changes the arrangement,
that file is the only one that needs to be repaired.

## Discovery sequence

`DesktopHostService.Discover()`:

1. `FindWindow("Progman", null)`.
2. `SendMessageTimeout(progman, 0x052C, …)` — the undocumented message that asks
   Explorer to split the desktop into a wallpaper `WorkerW` and an icon view.
   Three payloads are sent in turn: `(0, 0)`, `(0x0D, 0)`, and `(0x0D, 1)`.
   Windows 10 and the various Windows 11 builds disagree about which one works,
   and sending all three costs a few milliseconds.
3. `EnumWindows` to find the top-level window that owns a `SHELLDLL_DefView`
   child. That window is the icon view's host.
4. `FindWindowEx(NULL, thatWindow, "WorkerW", NULL)` — the sibling that follows
   the icon view in z-order is the one Explorer paints the wallpaper into.
5. If no `WorkerW` is found, fall back to `Progman` itself.

## Attachment

`SetParent(wallpaperWindow, host)`, then:

| Style | Why |
|---|---|
| `WS_CHILD` | Required once the window has an Explorer parent. |
| `WS_VISIBLE` | The window is never shown through `ShowWindow` as a top-level. |
| `WS_CLIPSIBLINGS`, `WS_CLIPCHILDREN` | Avoids painting over the icon view. |
| `WS_DISABLED` | The window must never receive input. |
| `WS_EX_TOOLWINDOW` | Keeps it out of Alt+Tab and the taskbar. |
| `WS_EX_NOACTIVATE` | It must never take focus. |
| `WS_EX_TRANSPARENT` | Hit testing falls through to the desktop. |

`WS_EX_APPWINDOW` is explicitly cleared: it would put the window back into the
taskbar, which is exactly what a wallpaper must not do.

The window procedure also returns `HTTRANSPARENT` for `WM_NCHITTEST` and
`MA_NOACTIVATE` for `WM_MOUSEACTIVATE`, so icon clicks, rubber-band selection,
and right-click menus keep reaching Explorer even if the styles are ever
changed.

Placement uses `SetWindowPos(…, HWND_BOTTOM, …)` with coordinates relative to
the host window's own rectangle, so a monitor at a negative virtual-screen
origin lands in the right place.

## Known risks

| Risk | Consequence | Mitigation |
|---|---|---|
| `0x052C` stops spawning a `WorkerW` | No wallpaper host | Progman fallback plus `HWND_BOTTOM` ordering |
| Explorer restarts | Child windows are destroyed with their parent | `ExplorerMonitor` watches the `TaskbarCreated` broadcast and polls the host handle; the coordinator drops unusable sessions and reconciliation rebuilds them |
| Explorer publishes `TaskbarCreated` before the desktop is rebuilt | Reattach races the shell | 750 ms settle delay before rediscovery |
| The wallpaper host moves in z-order | Video covers the icons, or is covered by the static wallpaper | `ReassertDesktopPlacement()` runs after every wake, display change, and Explorer event |
| Multiple app instances | Duplicate windows and players on one desktop | Single-instance mutex in `Program.cs` |
| Virtual desktops / Win+D | Explorer may hide the whole host | Handled by Explorer itself; the window is a child and follows the host |

## Rendering

The wallpaper is drawn by `Windows.UI.Composition`, not GDI:

```text
Local file → MediaSource → Windows.Media.Playback.MediaPlayer
           → MediaPlayerSurface → CompositionSurfaceBrush → SpriteVisual
           → DesktopWindowTarget (the wallpaper HWND)
```

`ICompositorDesktopInterop::CreateDesktopWindowTarget` is what binds a
composition tree to an arbitrary HWND. It requires a `DispatcherQueue` on the
calling thread; Wallpache creates one on the UI thread with
`DQTAT_COM_NONE`, because `[STAThread]` has already initialised the apartment
and asking the queue to do it again fails with `RPC_E_CHANGED_MODE`.

The media surface is created at the clip's own pixel size and the scaling mode
is resolved by `CompositionSurfaceBrush.Stretch`:

| Wallpache mode | `CompositionStretch` |
|---|---|
| Fill | `UniformToFill` |
| Fit | `Uniform` |
| Stretch | `Fill` |
| Center | `None` |

Doing the fit in the brush rather than in the decoder means changing the mode
never reloads the video.

## Replacing this layer

If a future Windows release makes the `WorkerW` technique unusable, only
`DesktopHostService` and `Windowing/WallpaperWindow` need replacing. The
coordinator, the sessions, the library, and the UI address the desktop only
through:

```text
Discover() / Rediscover()
Attach(hwnd, bounds)
Position(hwnd, bounds)
ReassertPlacement(hwnd, bounds)
Detach(hwnd)
IsHostValid
```
