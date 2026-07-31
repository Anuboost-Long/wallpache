#!/bin/bash
#
# Builds a distributable macOS Wallpache.app and packages it into dist/.
#
# Usage:
#   Scripts/make-dmg.sh                      # auto-detects the best identity
#   APP_ONLY=1 Scripts/make-dmg.sh           # signed .app only, for an external packager
#   SIGN_IDENTITY="Wallpache Dev" Scripts/make-dmg.sh   # self-signed cert
#   SIGN_IDENTITY="Developer ID Application: Name (TEAMID)" NOTARIZE=1 Scripts/make-dmg.sh
#
# Settings can also live in Scripts/signing.env (gitignored, see
# signing.env.example) so a release does not need a long command line.
#
# Ad-hoc and self-signed builds do NOT pass Gatekeeper. Because a downloaded
# copy carries the quarantine flag, macOS reports them as "damaged and can't be
# opened" rather than as an untrusted developer, so recipients must use
# System Settings > Privacy & Security > Open Anyway, or run:
#   xattr -dr com.apple.quarantine /Applications/Wallpache.app
# Only a Developer ID signature plus notarization removes that step.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT="$REPO_ROOT/apps/macos/Wallpache.xcodeproj"
BUILD_DIR="$REPO_ROOT/apps/macos/build"
DIST_DIR="$REPO_ROOT/dist"
APP="$BUILD_DIR/Build/Products/Release/Wallpache.app"

# Installer window styling. The backdrop is optional: without it the window is
# still icon view, still the right size, still has the two icons placed — just
# no artwork behind them. Regenerate it with:
#   swift Scripts/make-dmg-background.swift assets/dmg-background.png
DMG_BACKGROUND="${DMG_BACKGROUND:-$REPO_ROOT/assets/dmg-background.png}"
# HFS+ caps a volume name at 27 characters and hdiutil fails rather than
# truncating one that is too long.
VOLUME_NAME="Wallpache"

# Optional local config so release settings are not retyped every time. It is
# gitignored: the identity name is machine-specific, not a project constant.
CONFIG="$REPO_ROOT/Scripts/signing.env"
if [ -f "$CONFIG" ]; then
  echo "==> Loading $CONFIG"
  # Saved and restored so a one-off `SIGN_IDENTITY=... Scripts/make-dmg.sh`
  # still overrides the file rather than the other way round.
  ENV_SIGN_IDENTITY="${SIGN_IDENTITY:-}"
  ENV_NOTARIZE="${NOTARIZE:-}"
  ENV_NOTARY_PROFILE="${NOTARY_PROFILE:-}"
  # shellcheck disable=SC1090
  . "$CONFIG"
  if [ -n "$ENV_SIGN_IDENTITY" ]; then SIGN_IDENTITY="$ENV_SIGN_IDENTITY"; fi
  if [ -n "$ENV_NOTARIZE" ]; then NOTARIZE="$ENV_NOTARIZE"; fi
  if [ -n "$ENV_NOTARY_PROFILE" ]; then NOTARY_PROFILE="$ENV_NOTARY_PROFILE"; fi
fi

# Auto-signer: only a Developer ID Application certificate produces something a
# recipient can open without the quarantine dance, so that is the only kind
# worth picking automatically. Anything else falls back to ad-hoc, which is the
# previous behaviour.
if [ -z "${SIGN_IDENTITY:-}" ]; then
  SIGN_IDENTITY="$(security find-identity -v -p codesigning 2>/dev/null \
    | sed -n 's/.*"\(Developer ID Application: [^"]*\)".*/\1/p' | head -1)"
  if [ -n "$SIGN_IDENTITY" ]; then
    echo "==> Auto-detected signing identity: $SIGN_IDENTITY"
  else
    SIGN_IDENTITY="-"
    echo "==> No Developer ID Application certificate found; signing ad-hoc"
  fi
fi

NOTARIZE="${NOTARIZE:-0}"
# Keychain profile created once via: xcrun notarytool store-credentials
NOTARY_PROFILE="${NOTARY_PROFILE:-wallpache}"

# Notarization requires a Developer ID signature; submitting anything else just
# wastes a round trip and returns "Invalid".
if [ "$NOTARIZE" = "1" ] && [ "${SIGN_IDENTITY#Developer ID Application}" = "$SIGN_IDENTITY" ]; then
  echo "ERROR: NOTARIZE=1 needs a Developer ID Application identity, got: $SIGN_IDENTITY" >&2
  exit 1
fi

echo "==> Building Release (universal) with identity: $SIGN_IDENTITY"
rm -rf "$BUILD_DIR"
xcodebuild \
  -project "$PROJECT" \
  -scheme Wallpache \
  -configuration Release \
  -derivedDataPath "$BUILD_DIR" \
  CODE_SIGN_IDENTITY="$SIGN_IDENTITY" \
  CODE_SIGN_STYLE=Manual \
  DEVELOPMENT_TEAM="" \
  ARCHS="arm64 x86_64" \
  ONLY_ACTIVE_ARCH=NO \
  build

# Xcode's "Sign to Run Locally" injects get-task-allow even in Release, which
# makes the app unlaunchable on any other Mac. Re-sign with the same entitlements
# minus that key. Passing --entitlements is required: a bare `codesign --force`
# would silently strip app-sandbox and friends.
ENTITLEMENTS="$BUILD_DIR/dist-entitlements.plist"
codesign -d --entitlements "$ENTITLEMENTS" --xml "$APP" 2>/dev/null
plutil -convert xml1 "$ENTITLEMENTS"
# Dots must be escaped: plutil treats them as keypath separators. The key is
# absent whenever the build was signed with a real identity, and `plutil -remove`
# exits 1 on a missing key, which `set -e` would turn into a failed release.
plutil -remove 'com\.apple\.security\.get-task-allow' "$ENTITLEMENTS" 2>/dev/null || true

echo "==> Re-signing without get-task-allow"
# codesign does not contact a timestamp authority unless asked, and notarization
# rejects a signature without a secure timestamp. Ad-hoc signatures cannot carry
# one at all, hence the split.
TIMESTAMP_FLAG="--timestamp"
if [ "$SIGN_IDENTITY" = "-" ]; then
  TIMESTAMP_FLAG="--timestamp=none"
fi

codesign --force --options runtime $TIMESTAMP_FLAG \
  --entitlements "$ENTITLEMENTS" \
  --sign "$SIGN_IDENTITY" "$APP"

echo "==> Verifying signature"
codesign --verify --deep --strict --verbose=2 "$APP"

# get-task-allow must be absent, or the app is unlaunchable on other Macs.
if codesign -d --entitlements - --xml "$APP" 2>/dev/null | grep -q "get-task-allow"; then
  echo "ERROR: get-task-allow is present. This build will not run on another Mac." >&2
  exit 1
fi

mkdir -p "$DIST_DIR"

# The app itself is notarized and stapled before anything is packaged. Stapling
# only the .dmg leaves the copy the user drags out unstapled, so it fails to
# open on a machine that is offline, and a .zip built beforehand is never
# stapled at all.
if [ "$NOTARIZE" = "1" ]; then
  echo "==> Notarizing the app (requires a paid Developer ID identity)"
  ditto -c -k --keepParent "$APP" "$BUILD_DIR/notarize.zip"
  xcrun notarytool submit "$BUILD_DIR/notarize.zip" --keychain-profile "$NOTARY_PROFILE" --wait
  xcrun stapler staple "$APP"
  xcrun stapler validate "$APP"
fi

# Hands the signed bundle to an external packager instead of building the image
# here. ditto rather than cp: it is the copy that keeps extended attributes and
# a stapled notarization ticket intact.
if [ "${APP_ONLY:-0}" = "1" ]; then
  echo "==> App only, skipping zip and dmg"
  rm -rf "$DIST_DIR/Wallpache.app"
  ditto "$APP" "$DIST_DIR/Wallpache.app"
  codesign --verify --deep --strict "$DIST_DIR/Wallpache.app"

  echo "==> Gatekeeper assessment"
  spctl -a -vvv -t exec "$DIST_DIR/Wallpache.app" || echo "    (rejected as expected unless Developer ID + notarized)"

  echo
  echo "Done. App at: $DIST_DIR/Wallpache.app"
  exit 0
fi

rm -f "$DIST_DIR/Wallpache.zip" "$DIST_DIR/Wallpache.dmg"

# ditto, not zip -r: plain zip strips xattrs and breaks the code signature.
echo "==> Packaging"
ditto -c -k --keepParent "$APP" "$DIST_DIR/Wallpache.zip"

# Staged so the disk image carries an /Applications shortcut. Running the app
# from the read-only image leaves the login item pointing at a volume that is
# gone after the next reboot.
STAGE="$BUILD_DIR/dmg-stage"
SCRATCH_DMG="$BUILD_DIR/scratch.dmg"
rm -rf "$STAGE"
rm -f "$SCRATCH_DMG"
mkdir -p "$STAGE"
# ditto, not cp -R, for the same reason as the zip: it is the only copy that
# reliably carries extended attributes and the stapled notarization ticket.
ditto "$APP" "$STAGE/Wallpache.app"
ln -s /Applications "$STAGE/Applications"

# The backdrop is staged before the image is built, because Finder has to be
# able to point at it in the same breath as it sets the window layout.
# Dot-prefixed so the window shows two icons and nothing else.
BACKGROUND_NAME=""
if [ -f "$DMG_BACKGROUND" ]; then
  mkdir -p "$STAGE/.background"
  if sips -s format png "$DMG_BACKGROUND" --out "$STAGE/.background/background.png" >/dev/null 2>&1; then
    BACKGROUND_NAME="background.png"
  else
    echo "    (background image unreadable, the window will be plain)"
    rm -rf "$STAGE/.background"
  fi
fi

# Read/write and deliberately roomier than its contents: the .DS_Store and the
# volume icon are both written after this is mounted, and an image sized exactly
# to its contents has nowhere to put them. HFS+ because an APFS image will not
# mount on older systems and nothing here needs what APFS offers.
STAGE_MB=$(( $(du -sk "$STAGE" | cut -f1) / 1024 + 32 ))
hdiutil create -volname "$VOLUME_NAME" -srcfolder "$STAGE" \
  -fs HFS+ -size "${STAGE_MB}m" -format UDRW -ov "$SCRATCH_DMG" >/dev/null

echo "==> Styling the installer window"
ATTACH_OUTPUT="$(hdiutil attach "$SCRATCH_DMG" -nobrowse -noverify -noautoopen)"
MOUNT_POINT="$(echo "$ATTACH_OUTPUT" | grep -o '/Volumes/.*' | head -1)"

if [ -z "$MOUNT_POINT" ]; then
  echo "ERROR: the disk image mounted somewhere this could not find." >&2
  exit 1
fi

# Detach on the way out however this ends: a volume left mounted by a failed
# build is the one piece of mess the user would have to clear up by hand.
trap 'hdiutil detach "$MOUNT_POINT" -quiet 2>/dev/null || hdiutil detach "$MOUNT_POINT" -quiet -force 2>/dev/null || true' EXIT

# Addressed by disk name rather than path because that is the only handle Finder
# takes. Mounting a second volume of the same name gets you "Wallpache 1", so
# the name is read back from the mount point rather than assumed.
DISK_NAME="$(basename "$MOUNT_POINT")"
BACKGROUND_ALIAS=""
BACKGROUND_CLAUSE=""
if [ -n "$BACKGROUND_NAME" ]; then
  # The alias is resolved outside the `tell disk` block on purpose. A colon path
  # written inside it — "Disk:.background:file.png" — is read as a Finder object
  # of that disk and raises -10006, which aborts the whole script and takes the
  # icon positions down with it.
  BACKGROUND_ALIAS="set bgFile to POSIX file \"$MOUNT_POINT/.background/$BACKGROUND_NAME\" as alias"
  BACKGROUND_CLAUSE="set background picture of viewOptions to bgFile"
fi

# Icon centres at quarter and three-quarter width, slightly above centre so the
# labels have room. These fractions are mirrored in make-dmg-background.swift —
# change one and the arrow stops pointing at the icons.
# The close/open/update is not superstition: Finder writes the .DS_Store when
# the window closes, and without reopening it the positions set here can still
# be sitting in memory when the volume unmounts.
LAYOUT_SCRIPT=$(cat <<APPLESCRIPT
tell application "Finder"
  $BACKGROUND_ALIAS
  tell disk "$DISK_NAME"
    open
    set current view of container window to icon view
    set toolbar visible of container window to false
    set statusbar visible of container window to false
    set the bounds of container window to {200, 120, 860, 520}
    set viewOptions to the icon view options of container window
    set arrangement of viewOptions to not arranged
    set icon size of viewOptions to 128
    set text size of viewOptions to 12
    $BACKGROUND_CLAUSE
    set position of item "Wallpache.app" of container window to {172, 184}
    set position of item "Applications" of container window to {488, 184}
    close
    open
    update without registering applications
    delay 1
  end tell
end tell
APPLESCRIPT
)

# A plain window on a working image beats no image at all, so a refused
# Automation prompt is a warning rather than a failed release.
if ! LAYOUT_ERROR="$(osascript -e "$LAYOUT_SCRIPT" 2>&1 >/dev/null)"; then
  echo "    WARNING: could not style the window, the image will open as a plain folder."
  echo "    ${LAYOUT_ERROR}"
  echo "    If this is a permissions error, allow Terminal to control Finder in"
  echo "    System Settings > Privacy & Security > Automation, then build again."
fi

# Finder records a backdrop in the .DS_Store as a BKGD entry. On macOS 26 the
# `background picture` property is accepted without error and then quietly
# ignored, so the only way to know whether the artwork took is to look. When it
# did not, the unused PNG is dropped rather than shipped as dead weight.
if [ -n "$BACKGROUND_NAME" ]; then
  if xxd "$MOUNT_POINT/.DS_Store" 2>/dev/null | grep -qi "BKGD"; then
    echo "    Backdrop applied."
  else
    echo "    NOTE: this macOS ignored the window backdrop, so it was left out."
    echo "    The window is still laid out; only the artwork is missing."
    rm -rf "$MOUNT_POINT/.background"
  fi
fi

# After the layout, never before: Finder deletes .VolumeIcon.icns and clears the
# custom-icon flag as it writes the window out.
if [ -f "$APP/Contents/Resources/AppIcon.icns" ]; then
  ditto "$APP/Contents/Resources/AppIcon.icns" "$MOUNT_POINT/.VolumeIcon.icns"
  # kHasCustomIcon (0x0400) in the Finder flags at offset 8 of the 32-byte
  # com.apple.FinderInfo attribute. SetFile would need the Xcode command line
  # tools; xattr is in every macOS.
  xattr -wx com.apple.FinderInfo \
    "0000000000000000040000000000000000000000000000000000000000000000" \
    "$MOUNT_POINT" 2>/dev/null || true
fi

hdiutil detach "$MOUNT_POINT" -quiet 2>/dev/null || hdiutil detach "$MOUNT_POINT" -quiet -force
trap - EXIT

# UDZO is the compressed read-only format a distributable image uses. The
# headroom left above compresses away to nothing here.
echo "==> Compressing the disk image"
hdiutil convert "$SCRATCH_DMG" -format UDZO -o "$DIST_DIR/Wallpache.dmg" -ov >/dev/null
rm -f "$SCRATCH_DMG"

if [ "$NOTARIZE" = "1" ]; then
  echo "==> Signing and notarizing the disk image"
  codesign --force --timestamp --sign "$SIGN_IDENTITY" "$DIST_DIR/Wallpache.dmg"
  xcrun notarytool submit "$DIST_DIR/Wallpache.dmg" --keychain-profile "$NOTARY_PROFILE" --wait
  xcrun stapler staple "$DIST_DIR/Wallpache.dmg"
  xcrun stapler validate "$DIST_DIR/Wallpache.dmg"
fi

echo "==> Gatekeeper assessment"
spctl -a -vvv -t exec "$APP" || echo "    (rejected as expected unless Developer ID + notarized)"

echo
echo "Done. Artifacts in dist/:"
ls -lh "$DIST_DIR"
