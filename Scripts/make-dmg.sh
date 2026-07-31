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
rm -rf "$STAGE"
mkdir -p "$STAGE"
# ditto, not cp -R, for the same reason as the zip: it is the only copy that
# reliably carries extended attributes and the stapled notarization ticket.
ditto "$APP" "$STAGE/Wallpache.app"
ln -s /Applications "$STAGE/Applications"
hdiutil create -volname Wallpache -srcfolder "$STAGE" -ov -format UDZO "$DIST_DIR/Wallpache.dmg" >/dev/null

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
