/**
 * Every outward-facing link in one place. Releases and the installer live in
 * the public dist repo — never in the source repo, whose assets are private.
 */
export const site = {
  name: "Wallpache",
  /** Absolute base for OG/Twitter image URLs — set NEXT_PUBLIC_SITE_URL at build time. */
  url: process.env.NEXT_PUBLIC_SITE_URL ?? "https://anuboost-long.github.io/wallpache",
  tagline: "Live video wallpapers for macOS and Windows",
  description:
    "Drop in a video and it loops behind your desktop icons — per display, at the scale you choose, paused whenever your machine needs the battery.",
  distRepo: "https://github.com/Anuboost-Long/wallpache-dist",
  releases: "https://github.com/Anuboost-Long/wallpache-dist/releases",
  latestRelease: "https://github.com/Anuboost-Long/wallpache-dist/releases/latest",
  installScript:
    "https://github.com/Anuboost-Long/wallpache-dist/blob/main/install.sh",
  installCommand:
    "curl -fsSL https://raw.githubusercontent.com/Anuboost-Long/wallpache-dist/main/install.sh | bash",
  quarantineCommand:
    "xattr -dr com.apple.quarantine /Applications/Wallpache.app",
  /**
   * Direct asset links via GitHub's `/releases/latest/download/<name>` alias,
   * which always resolves to whatever the current release attached under that
   * exact filename — no version number to keep in sync here. The macOS build
   * has always published under this fixed name; the Windows release additionally
   * uploads an unversioned `WallpacheSetup.exe` copy of the installer solely so
   * this link keeps working release over release.
   */
  macDownload:
    "https://github.com/Anuboost-Long/wallpache-dist/releases/latest/download/Wallpache.dmg",
  windowsDownload:
    "https://github.com/Anuboost-Long/wallpache-dist/releases/latest/download/WallpacheSetup.exe",
  /** Legal owner named in the copyright notice, Terms, and Privacy Policy. */
  copyrightHolder: "Anuboost-Long",
  legalEmail: "kimlongly57@gmail.com",
} as const;

export const navLinks = [
  { href: "#features", label: "Features" },
  { href: "#how", label: "How it works" },
  { href: "#download", label: "Download" },
  { href: "#donate", label: "Donate" },
] as const;

export const legalLinks = [
  { href: "/terms", label: "Terms of Service" },
  { href: "/privacy", label: "Privacy Policy" },
] as const;
