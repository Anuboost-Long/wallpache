<div align="center">

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="assets/logo-dark.png">
  <img src="assets/logo-light.png" alt="Wallpache" width="148">
</picture>

<h1>Wallpache</h1>

<p><strong>Live video wallpapers for macOS and Windows.</strong></p>

<p>
Drop in a video and it loops behind your desktop icons —<br>
per display, at the scale you choose, paused whenever your machine needs the battery.
</p>

<p>
<img src="https://img.shields.io/badge/macOS-13.0%2B-7C5CD6?style=for-the-badge&logo=apple&logoColor=white" alt="macOS 13.0+">
<img src="https://img.shields.io/badge/Windows-10%20%2F%2011-4CC9F0?style=for-the-badge&logo=windows&logoColor=white" alt="Windows 10/11">
<img src="https://img.shields.io/badge/Universal-Silicon%20%2B%20Intel-FF7A45?style=for-the-badge" alt="Universal binary">
<a href="https://github.com/Anuboost-Long/wallpache-dist/releases"><img src="https://img.shields.io/badge/Download-Latest%20Release-FFB347?style=for-the-badge&logo=github&logoColor=white" alt="Download"></a>
</p>

</div>

---

## ⚡ Install

### macOS

One line in Terminal:

```bash
curl -fsSL https://raw.githubusercontent.com/Anuboost-Long/wallpache-dist/main/install.sh | bash
```

Then launch it:

```bash
open -a Wallpache
```

That's it. The installer fetches the latest release, checks the download, installs
to `/Applications`, and clears the quarantine flag so macOS opens it without a fuss.

<details>
<summary><b>Install somewhere else</b></summary>

<br>

```bash
INSTALL_DIR="$HOME/Applications" bash -c "$(curl -fsSL https://raw.githubusercontent.com/Anuboost-Long/wallpache-dist/main/install.sh)"
```

</details>

<details>
<summary><b>Prefer the disk image?</b></summary>

<br>

1. Download `Wallpache.dmg` from [Releases](https://github.com/Anuboost-Long/wallpache-dist/releases).
2. Open it and drag **Wallpache** onto the **Applications** shortcut.
3. Clear the quarantine flag — **this step is not optional**:

   ```bash
   xattr -dr com.apple.quarantine /Applications/Wallpache.app
   ```

4. Open Wallpache from Applications.

</details>

### Windows

1. Download `WallpacheSetup-<version>.exe` from [Releases](https://github.com/Anuboost-Long/wallpache-dist/releases).
2. Run it. No admin rights needed — it installs to your user profile.
3. Windows SmartScreen will likely warn about an unrecognised publisher the
   first time — see [below](#️-if-windows-says-the-publisher-is-unknown) for why
   that's expected.
4. Wallpache launches automatically after install, and lives in the system tray
   from then on.

---

## ✨ What it does

<table>
<tr>
<td width="50%" valign="top">

### 🖥 Every display, its own way

Each screen keeps its own video, scaling mode, playback speed, and mute setting.
One wallpaper everywhere, or a different one on each.

</td>
<td width="50%" valign="top">

### 🔋 Never costs you battery

Playback stops on battery saver, a locked screen, and while the machine sleeps.
On Windows it also survives an Explorer restart without leaving a blank desktop.

</td>
</tr>
<tr>
<td width="50%" valign="top">

### 🖱 Invisible to your clicks

The wallpaper sits below your icons and passes every click straight through to
the desktop. It never keeps your display awake, and stays out of Alt+Tab and
the taskbar.

</td>
<td width="50%" valign="top">

### 📂 Your files stay yours

Imports are copied into Wallpache's own storage, so moving or deleting the
original video later won't break anything.

</td>
</tr>
</table>

---

## 🚀 First run

| | |
|:--:|---|
| **1** | **Open Wallpache.** The library window opens by itself the first time. After that, reach it from the menu-bar icon (macOS) or tray icon (Windows). |
| **2** | **Add a video.** Drag an `.mp4`, `.mov`, or `.m4v` onto the window, or press **Import Video…** |
| **3** | **Preview it** to check the loop before you commit to it. |
| **4** | **Apply it.** **Apply** covers every display, or use the **Display** menu on the tile for one screen at a time. |
| **5** | *Optional* — turn on **Launch at login** (macOS) or **Launch at sign-in** (Windows) in Settings → Startup. |

---

## 🎛 Scaling modes

| Mode | Behaviour |
|---|---|
| **Fill** | Covers the screen, cropping the overflow |
| **Fit** | Whole frame visible, letterboxed |
| **Stretch** | Fills the screen, ignoring aspect ratio |
| **Center** | Native pixel size, centred |

---

## 🔋 Energy saving

Wallpache stops rather than burn battery on frames nobody is looking at:

`Low Power Mode / Battery Saver` &nbsp;·&nbsp; `Thermal pressure (macOS)` &nbsp;·&nbsp; `Screen locked` &nbsp;·&nbsp; `Machine asleep`

The menu bar / tray icon always names the current state — *Paused in Low Power
Mode*, *Paused while the Mac is hot* — so a stopped wallpaper is never a
mystery.

---

## ⚠️ If macOS says the app is "damaged"

> **"Wallpache" is damaged and can't be opened. You should move it to the Trash.**

**Your download is fine.** Nothing is corrupted.

Wallpache is signed ad-hoc rather than with a paid Apple Developer ID certificate.
macOS marks anything arriving through a browser with a quarantine attribute, and
for an ad-hoc signed app it reports that as damage instead of the usual
"unidentified developer" prompt. Clearing the flag fixes it:

```bash
xattr -dr com.apple.quarantine /Applications/Wallpache.app
```

> 💡 The one-line installer does this for you — which is exactly why it's the
> recommended route.

## ⚠️ If Windows says the publisher is unknown

> **Windows protected your PC — Microsoft Defender SmartScreen prevented an
> unrecognised app from starting.**

**Your download is fine.** Wallpache isn't yet signed with a paid code-signing
certificate, so Windows has no publisher identity to vouch for. Click **More
info**, then **Run anyway**. This is the same trust gap the macOS build
documents above — just Windows' version of the warning.

---

## 📁 Where your files go

**macOS**

```
~/Library/Application Support/Wallpache/
├── videos/       imported copies of your videos
├── thumbnails/   library grid thumbnails
└── stills/       preview frames
```

**Windows**

```
%LOCALAPPDATA%\Wallpache\
├── Wallpapers\   imported copies of your videos
├── Thumbnails\   library grid thumbnails
└── Stills\       preview frames
```

Settings → Storage has **Show in Finder / Explorer** and **Delete All Imported
Videos…**. Removing a wallpaper returns that display to its normal desktop
wallpaper.

On macOS, Wallpache runs in the App Sandbox and asks for read-only access only
to the files you pick yourself.

<details>
<summary><b>Uninstall</b></summary>

<br>

**macOS**

```bash
rm -rf /Applications/Wallpache.app
rm -rf ~/Library/Application\ Support/Wallpache
```

**Windows** — use **Settings → Apps**, or the **Uninstall Wallpache** shortcut
the installer adds to the Start menu. It removes the app and its startup
registration; your imported videos under `%LOCALAPPDATA%\Wallpache` are left in
place.

</details>

---

## 📋 Requirements

| | macOS | Windows |
|---|---|---|
| **OS version** | 13.0 Ventura or later | Windows 10 (2004) or later, and 11 |
| **Architecture** | Universal — Apple Silicon and Intel | x64 |
| **Formats** | `.mp4` &nbsp; `.mov` &nbsp; `.m4v` | `.mp4` &nbsp; `.mov` &nbsp; `.m4v` |

---

<div align="center">

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="assets/logo-dark.png">
  <img src="assets/logo-light.png" alt="" width="52">
</picture>

<sub>This repository hosts the releases and the installer.<br>
Wallpache is not open source — the source is kept in a private repository.</sub>

</div>
