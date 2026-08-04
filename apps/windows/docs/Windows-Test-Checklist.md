# Wallpache for Windows — Manual Test Checklist

The engine touches Explorer, the compositor, power management, and the session
manager. None of that can be covered from a build machine, so this list is the
acceptance gate for a release.

Run on Windows 11 unless a row says otherwise.

## What is already covered automatically

`dotnet test tests/Wallpache.App.Tests` runs on any host, including a macOS or
Linux build machine, and covers the parts that are platform-independent by
design:

- the pause/resume policy, including precedence between reasons;
- settings round-tripping, defaults for files written by older builds, and
  recovery from a corrupt file;
- the storage layout, deletion, and orphan cleanup;
- the scaling-mode mappings;
- loading every view, window, and brand resource.

It does **not** cover the desktop host, composition, media decoding, power and
session events, the registry, or the tray. Those are what the rest of this
document is for.

## 1. Proof of concept

- [ ] Import an H.264 `.mp4` and apply it.
- [ ] The video appears behind the desktop icons.
- [ ] Icons remain clickable: single click, double click, right-click menu.
- [ ] Rubber-band selection still works on the desktop.
- [ ] Win+D shows the desktop; the wallpaper is still playing.
- [ ] Normal application windows stay above the video.
- [ ] The wallpaper window is absent from Alt+Tab.
- [ ] The wallpaper window is absent from the taskbar.
- [ ] Quitting Wallpache from the tray restores the normal Windows wallpaper.

## 2. Library

- [ ] Import via the tray menu and via **Import Video…**.
- [ ] Drag and drop one file onto the Library tab.
- [ ] Drag and drop several files at once.
- [ ] Importing the same file twice does not create a second copy.
- [ ] Rename a wallpaper; the file on disk does not move and playback is undisturbed.
- [ ] Delete a wallpaper that is in use; that display falls back to the normal wallpaper.
- [ ] **Delete All Imported Videos** empties the library and stops playback.
- [ ] **Show in File Explorer** opens `%LOCALAPPDATA%\Wallpache\Wallpapers`.
- [ ] Thumbnails appear for every entry.

## 3. Media handling

- [ ] H.264 `.mp4`, 1080p.
- [ ] H.264 `.mp4`, 4K.
- [ ] `.mov` that the installed codecs can decode.
- [ ] `.m4v`.
- [ ] A clip shorter than two seconds loops without a visible gap.
- [ ] A clip longer than ten minutes loops correctly.
- [ ] A portrait clip reports portrait dimensions.
- [ ] A corrupt file is rejected at import with a readable message.
- [ ] A renamed non-video file (`notes.txt` → `notes.mp4`) is rejected.
- [ ] A DRM-protected file is rejected with the protected-content message.
- [ ] Deleting the imported copy outside the app, then relaunching, removes the
      entry and explains why.

## 4. Playback controls

- [ ] Fill crops and fills the display.
- [ ] Fit shows the whole frame on black.
- [ ] Stretch distorts to the display's aspect ratio.
- [ ] Center draws the clip at its natural pixel size on black.
- [ ] Changing the mode does not restart the video.
- [ ] Each playback rate from 0.25× to 2× takes effect immediately.
- [ ] **Play audio** unmutes; the default is muted.
- [ ] Pause and Resume from the tray.
- [ ] Stop from the tray reveals the normal wallpaper.

## 5. Desktop picture

- [ ] **Match desktop picture** sets the still frame as the Windows wallpaper.
- [ ] Turning it off restores the wallpaper the user had before.
- [ ] With several monitors, only the chosen display changes.
- [ ] Quitting Wallpache leaves the still frame in place.

## 6. Multiple displays

- [ ] One, two, and three monitors.
- [ ] **Apply** sets the same wallpaper everywhere.
- [ ] The per-cell **Display** menu targets one monitor.
- [ ] Different wallpapers on different monitors persist across a restart.
- [ ] Change the primary monitor.
- [ ] Rotate a monitor to portrait.
- [ ] Mixed DPI (100% and 150%) — no cropping or letterboxing artefacts.
- [ ] Change a monitor's resolution while playing.
- [ ] Unplug a monitor while playing; the others keep running.
- [ ] Replug it; its assignment comes back.
- [ ] Rearrange monitors in Display settings.
- [ ] No app restart is needed for any of the above.

## 7. Power, lock, and sleep

- [ ] Lock the session; playback pauses (with the setting on).
- [ ] Unlock; playback resumes.
- [ ] Switch users and back.
- [ ] Sleep and wake; playback resumes and windows are still attached.
- [ ] Hibernate and resume.
- [ ] Turn the displays off and on.
- [ ] Unplug the charger with **Pause when running on battery** on.
- [ ] Enable Battery Saver with **Pause in Battery Saver** on.
- [ ] Each pause reason appears in the tray header.
- [ ] Repeat sleep/wake five times; no duplicate windows appear.

## 8. Explorer recovery

- [ ] End `explorer.exe` in Task Manager and let it restart.
- [ ] The wallpaper comes back without restarting Wallpache.
- [ ] Repeat three times in a row.
- [ ] Toggle "Show desktop icons" off and on.
- [ ] Change the Windows wallpaper in Settings while Wallpache is running.

## 9. Lifecycle

- [ ] **Launch at sign-in** on; sign out and back in; the wallpaper restores.
- [ ] Launch at sign-in off; the registry entry is removed.
- [ ] Move the installed folder; the startup entry is repaired on next launch.
- [ ] Launching a second copy does nothing (single instance).
- [ ] Closing the window keeps the wallpaper running.
- [ ] Double-clicking the tray icon reopens the window.
- [ ] Kill the process; relaunch; the last valid configuration is restored.
- [ ] Delete `settings.json`; the app launches with an empty library.
- [ ] Corrupt `settings.json`; the app launches, keeps the broken file as
      `settings.json.corrupt`, and does not crash.
- [ ] Windows restart; the wallpaper restores.

## 10. Endurance

- [ ] Eight hours of continuous playback on 1080p.
- [ ] Memory in Task Manager is stable from hour one to hour eight.
- [ ] CPU and GPU usage stay within what a compositor-driven video costs.
- [ ] No log growth beyond the 2 MB cap in `%LOCALAPPDATA%\Wallpache\Logs`.

## 11. Privacy

- [ ] With a network monitor attached, no outbound traffic during import,
      playback, or idle.
- [ ] No account, sign-in, or telemetry prompt anywhere in the UI.
