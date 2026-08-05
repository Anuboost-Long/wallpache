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
} as const;

export const navLinks = [
  { href: "#features", label: "Features" },
  { href: "#how", label: "How it works" },
  { href: "#download", label: "Download" },
] as const;
