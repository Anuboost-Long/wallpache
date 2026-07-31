#!/bin/bash
#
# Installs Wallpache from the latest GitHub release.
#
# Usage:
#   curl -fsSL https://raw.githubusercontent.com/Anuboost-Long/wallpache-dist/main/install.sh | bash
#
# The build is signed ad-hoc rather than with a Developer ID, so a copy that
# arrives through a browser is quarantined and macOS calls it damaged. This
# clears the quarantine flag on a copy the user asked for by name, which is the
# whole reason the install is a script rather than a download link.
#
# ZIP_URL points somewhere else when testing a build before it is released:
#   ZIP_URL="file:///path/to/Wallpache.zip" ./install.sh

set -euo pipefail

# The public release repo, never the source repo. Release assets inherit the
# repository's visibility, so a private repo's download URL answers 404 to
# anyone without a token — which would be every user of this script. This file
# is copied into that repo verbatim; it needs no edits between the two.
REPO="Anuboost-Long/wallpache-dist"
ZIP_URL="${ZIP_URL:-https://github.com/$REPO/releases/latest/download/Wallpache.zip}"
APP_NAME="Wallpache.app"

if [ "$(uname -s)" != "Darwin" ]; then
  echo "ERROR: Wallpache is a macOS app." >&2
  exit 1
fi

# /Applications is writable by admin users, but not by every account. Falling
# back keeps the install working without asking for a password.
TARGET_DIR="${INSTALL_DIR:-/Applications}"
if [ ! -w "$TARGET_DIR" ]; then
  TARGET_DIR="$HOME/Applications"
  mkdir -p "$TARGET_DIR"
  echo "==> /Applications is not writable, installing to $TARGET_DIR"
fi

TARGET="$TARGET_DIR/$APP_NAME"

# Replacing the bundle under a running app leaves it running from a path that no
# longer exists, and its wallpaper windows outlive it.
if pgrep -x Wallpache >/dev/null 2>&1; then
  echo "==> Quitting the running copy"
  osascript -e 'quit app "Wallpache"' >/dev/null 2>&1 || true
  sleep 2
fi

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

echo "==> Downloading"
curl -fSL --progress-bar "$ZIP_URL" -o "$WORK/Wallpache.zip"

# ditto, not unzip: it is the extraction that keeps the code signature intact.
echo "==> Extracting"
ditto -x -k "$WORK/Wallpache.zip" "$WORK/extracted"

if [ ! -d "$WORK/extracted/$APP_NAME" ]; then
  echo "ERROR: the download did not contain $APP_NAME." >&2
  exit 1
fi

# The signature is ad-hoc, so this proves only that the bytes arrived intact and
# nothing rewrote the bundle in transit. It is not a claim about who built it.
echo "==> Verifying the download"
if ! codesign --verify --strict "$WORK/extracted/$APP_NAME" 2>/dev/null; then
  echo "ERROR: the downloaded app failed signature verification." >&2
  exit 1
fi

echo "==> Installing to $TARGET"
rm -rf "$TARGET"
ditto "$WORK/extracted/$APP_NAME" "$TARGET"

# Downloads carry com.apple.quarantine, and an ad-hoc signature turns that into
# "damaged and can't be opened" rather than a prompt the user can dismiss.
xattr -dr com.apple.quarantine "$TARGET" 2>/dev/null || true

echo
echo "Done. Installed $TARGET"
echo "Open it with:  open -a Wallpache"
