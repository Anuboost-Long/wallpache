# Wallpache: Why the video wallpaper wasn't showing

**Date:** 2026-08-04
**Scope:** `apps/windows/src/Wallpache.App` — video wallpaper rendering and in-app preview

---

## Summary in plain words

The video wallpaper looked completely broken, but it wasn't one bug — it was three bugs, stacked so that each one hid the next. We had to fix them one at a time, testing after each fix, before the real picture became clear.

1. **Nothing drew at all.** A specific Windows API call the app used to hand a window over to the video-drawing system was failing every single time, silently. So there was never anything to see — no wallpaper, no preview.
2. **Once that was fixed, the video was there but invisible.** It was being drawn *behind* the desktop icons instead of in front of the plain background, so the icon layer covered it completely.
3. **Once that was fixed, the video showed up but never moved.** It rendered exactly one frame and then froze, even though the video was actually still playing correctly in the background (audio position kept advancing normally — we could measure it). It just stopped showing new pictures on screen.

Each layer looked like "the fix" until we tested it directly and found the next problem underneath. All three are now fixed, verified by watching the actual screen play the video with the desktop icons visible on top.

---

## What was happening, one layer at a time

### Bug 1 — the video's drawing surface never got created

Windows has no built-in way to put a video as the desktop background. Wallpache works around that by drawing directly into a hidden window that sits behind the desktop icons, using a low-level Windows drawing system called `Windows.UI.Composition`.

To do that, the app needs to ask a helper object (called `ICompositorDesktopInterop`) for that drawing target. The exact line of code doing this was throwing an error — *"Specified cast is not valid"* — on every single attempt, both for the desktop wallpaper and for the in-app preview (they share the same code).

**Why:** This turned out to be a known limitation of the interop plumbing (`CsWinRT`) that Microsoft uses to bridge modern .NET code to this older Windows API. That plumbing only knows how to hand you certain kinds of Windows interfaces automatically — `ICompositorDesktopInterop` isn't one of them, and the standard way of asking for it (`.As<T>()`) simply doesn't work for it. This is confirmed by a years-old, still-open bug report on Microsoft's own `CsWinRT` GitHub repository (issue #959), where a Microsoft engineer says outright: *"this is a feature we still need to implement."*

**How we found it:** The app already writes a log file (`%LOCALAPPDATA%\Wallpache\Logs\wallpache.log`). It showed the exact same error, over and over, every time the app tried to draw a wallpaper.

### Bug 2 — the video was invisible behind the desktop icons

After fixing bug 1, the drawing surface got created successfully — but nothing still appeared on the desktop. Turning off the "mirror the video's still frame as the desktop picture" setting (a separate, unrelated feature) proved this: with that feature off, the desktop went back to showing the user's own plain wallpaper picture, meaning the actual video layer was rendering *nothing at all*.

**Why:** Windows draws the desktop icons in a special window that Explorer creates (the desktop icon list). Wallpache's video window is meant to sit in a specific spot in that stack: behind the icons, but in front of the plain background — like a sandwich. The app was instead sinking its video window to the very bottom of the whole stack, which (on this machine's current setup) put it *behind* the icon layer, and the icon layer was fully covering it. So the video was there, just hidden under the icons.

**How we found it:** We temporarily forced the video window to sit *above* the icons instead of "at the very bottom" — the video immediately appeared (though it hid the icons, which isn't the goal). That proved the drawing itself worked and the problem was purely about stacking order.

### Bug 3 — the video rendered once, then froze

After fixing bug 2, the video appeared **and was correctly positioned behind the icons** — but it was a single, frozen frame. It never moved. This was the most confusing part, because everything *looked* fixed.

We proved the video file itself was still playing normally the whole time, by watching the internal playback clock (which the video player reports on its own): it advanced smoothly, in real time, second by second, exactly as expected. So decoding was fine — only the *on-screen picture* was stuck.

Two real problems were found and fixed together:

- **A setup-order mistake.** The app was telling the video system "start drawing" before telling it the video's actual size, then correcting the size a moment later. Microsoft's own documentation says the size must be set *before* drawing starts, not after — doing it in the wrong order can quietly break the continuous flow of new video frames onto the screen, without any error being raised.
- **The wrong "shelf" in the desktop's window structure.** Modern Windows actually *does* create a dedicated, hidden helper window especially for wallpapers (this is the standard trick every wallpaper app uses) — but the app's code was looking for it in the wrong place, because Windows now nests that helper window somewhere slightly different than it used to on older Windows versions. So the app never found it, and fell back to parenting its video window directly into the wrong shelf (`Progman`) instead — a spot where Windows apparently only takes a single "snapshot" of what's there and doesn't keep refreshing it, exactly matching the frozen-frame symptom.

**How we found it:** By listing every window Explorer had created and inspecting the tree directly, we found the real dedicated wallpaper window Windows had already created for us — the app was just never looking in the right place for it. Once the app started parenting into that correct window instead, and the setup order was fixed, the video began playing smoothly with the icons correctly on top.

---

## The fix

Five files changed in `apps/windows/src/Wallpache.App`:

| File | What changed |
|---|---|
| `Interop/CompositionInterop.cs` | Stopped using the interop helper that doesn't support this particular Windows interface; ask Windows for it directly instead. |
| `Desktop/DesktopHostService.cs` | (a) Look for Explorer's dedicated wallpaper window in the place modern Windows actually puts it. (b) When that dedicated window truly doesn't exist, insert the wallpaper window directly behind the icon layer instead of sinking it to the very bottom of everything. |
| `Core/WallpaperSession.cs` | Set the video's size before handing it to the drawing system, not after. |
| `Playback/VideoSurfaceHost.cs` | Added a log line confirming the drawing surface bound successfully, to make this diagnosable in the future. |
| `Playback/VideoLoopPlayer.cs` | Added log lines for "video opened" and playback state changes, for the same reason. |

Verified end-to-end: rebuilt, relaunched, and confirmed on the real screen that the wallpaper now plays continuously with the desktop icons visible and usable on top of it.

---

## Code changes

### `Interop/CompositionInterop.cs` — the real Windows interface, without the broken helper

```csharp
try
{
    // Compositor.As<ICompositorDesktopInterop>() throws E_NOINTERFACE:
    // CsWinRT's As/TryAs helpers only resolve interfaces that are
    // themselves CsWinRT-projected (IInspectable-based), and this interop
    // interface is a classic IUnknown-derived COM interface with no
    // projection (see microsoft/CsWinRT#959 - unsupported by design).
    // Querying the compositor's underlying native pointer directly and
    // wrapping the result as a plain COM RCW sidesteps CsWinRT entirely.
    var nativeObject = ((IWinRTObject)Compositor).NativeObject;
    var interopGuid = typeof(ICompositorDesktopInterop).GUID;

    if (nativeObject.TryAs(interopGuid, out IntPtr interopPtr) < 0 || interopPtr == IntPtr.Zero)
    {
        Log.Playback.Error("Compositor does not support ICompositorDesktopInterop");
        return null;
    }

    try
    {
        var interop = (ICompositorDesktopInterop)Marshal.GetTypedObjectForIUnknown(
            interopPtr, typeof(ICompositorDesktopInterop));

        interop.CreateDesktopWindowTarget(hwnd, false, out var raw);
        if (raw == IntPtr.Zero)
        {
            return null;
        }

        return MarshalInspectable<DesktopWindowTarget>.FromAbi(raw);
    }
    finally
    {
        Marshal.Release(interopPtr);
    }
}
catch (Exception error)
{
    Log.Playback.Error($"Composition target creation failed: {error.GetType().FullName} 0x{error.HResult:X8} {error.Message}\n{error.StackTrace}");
    return null;
}
```

### `Desktop/DesktopHostService.cs` — finding the real wallpaper "shelf"

```csharp
public static IntPtr FindWallpaperHost()
{
    // Current Explorer builds nest the dedicated wallpaper WorkerW as a
    // child of Progman itself, alongside SHELLDLL_DefView, rather than as a
    // top-level sibling. A window merely sitting behind DefView as a
    // Progman-fallback sibling is not equivalent: DWM appears to only
    // composite that position once and never refresh it, freezing video
    // content there after the first frame. This dedicated WorkerW child, by
    // contrast, composites continuously - so it must be found and preferred
    // over the Progman-fallback path.
    var progman = FindProgman();
    if (progman != IntPtr.Zero)
    {
        var nestedWorkerW = NativeMethods.FindWindowEx(progman, IntPtr.Zero, "WorkerW", null);
        if (nestedWorkerW != IntPtr.Zero)
        {
            return nestedWorkerW;
        }
    }

    // Older Explorer builds create it as a top-level sibling instead.
    var defViewOwner = FindShellDefViewOwner();
    if (defViewOwner == IntPtr.Zero)
    {
        return IntPtr.Zero;
    }

    return NativeMethods.FindWindowEx(IntPtr.Zero, defViewOwner, "WorkerW", null);
}
```

...and the fallback z-order fix, for when no dedicated window exists at all:

```csharp
// Ordinarily the host is a WorkerW dedicated to the wallpaper, with
// nothing else parented to it, so sinking to the bottom is enough. When
// Explorer never split off that dedicated WorkerW, the host is Progman
// itself and the icon view (SHELLDLL_DefView) is a sibling; sinking to
// the absolute bottom then hides the wallpaper behind the (opaque, in
// that configuration) icon view instead of behind it. Inserting directly
// after the icon view keeps icons on top while the wallpaper still shows.
var insertAfter = NativeMethods.HWND_BOTTOM;
if (_host.IsValid)
{
    var defView = NativeMethods.FindWindowEx(_host.Handle, IntPtr.Zero, "SHELLDLL_DefView", null);
    if (defView != IntPtr.Zero && defView != window)
    {
        insertAfter = defView;
    }
}

NativeMethods.SetWindowPos(
    window,
    insertAfter,
    bounds.Left - originX,
    bounds.Top - originY,
    bounds.Width,
    bounds.Height,
    NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
```

### `Core/WallpaperSession.cs` — size before drawing starts, not after

```csharp
_windowController = new WallpaperWindowController(desktopHost, display);
_player = new VideoLoopPlayer(videoPath, configuration.IsMuted, configuration.PlaybackRate);

// The video's pixel size must be set before the player is attached: the
// composition surface is sized and bound (MediaPlayer.SetSurfaceSize then
// GetSurface) on attach, and resizing the surface again right after
// GetSurface has already handed out a bound surface stalls the frame
// pump after the first frame instead of raising an error.
_windowController.Apply(configuration.ScalingMode, record.Width, record.Height);
_windowController.Attach(_player);
_windowController.Show();
```

Same fix applied to switching wallpapers on a display that's already playing one:

```csharp
public void ReplaceVideo(Guid wallpaperId, string videoPath, int? videoWidth, int? videoHeight)
{
    WallpaperId = wallpaperId;

    // Same ordering requirement as construction: the new size must be in
    // place before ReplaceVideo rebuilds the player and re-binds the
    // surface, or the post-bind resize stalls the frame pump.
    _windowController.Apply(Configuration.ScalingMode, videoWidth, videoHeight);
    _player.ReplaceVideo(videoPath);
}
```

---

## How this was verified

- Rebuilt the app and read its own log file after each change — the exact error message disappeared once bug 1 was fixed, and "Started session" began appearing cleanly.
- Confirmed bug 2 by toggling the desktop-picture-mirroring setting off and on, isolating the video layer from the unrelated static-picture feature.
- Confirmed bug 3 by watching the video player's own internal playback clock advance steadily in real time (proving the video itself was never actually stuck), while comparing screenshots that showed the on-screen picture never changing.
- After every fix, watched the actual desktop directly rather than trusting the log alone — an early screenshot-based check gave a false "it's animating" result, caught and corrected before being reported as fixed.
- Final state confirmed by eye: the wallpaper visibly plays, and the desktop icons remain visible and on top of it, as intended.
