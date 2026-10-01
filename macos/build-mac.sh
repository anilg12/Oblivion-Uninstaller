#!/usr/bin/env bash
# Builds Oblivion.app (universal: Apple Silicon + Intel), ad-hoc signs it and
# packages a drag-to-install DMG.  Run on macOS with Xcode / Command Line Tools:
#     cd macos && ./build-mac.sh
set -euo pipefail
cd "$(dirname "$0")"

APP_NAME="Oblivion"
VERSION="${OBLIVION_VERSION:-2.0.0}"
OUT="build"
APP="$OUT/$APP_NAME.app"

rm -rf "$OUT"
mkdir -p "$OUT"

echo "==> Swift build (release, arm64 + x86_64)"
swift build -c release --arch arm64 --arch x86_64
BIN_DIR="$(swift build -c release --arch arm64 --arch x86_64 --show-bin-path)"
echo "    binaries: $BIN_DIR"
lipo -info "$BIN_DIR/$APP_NAME"

echo "==> Assemble $APP_NAME.app"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp "$BIN_DIR/$APP_NAME" "$APP/Contents/MacOS/$APP_NAME"
sed "s/__VERSION__/$VERSION/g" Resources/Info.plist > "$APP/Contents/Info.plist"
printf 'APPL????' > "$APP/Contents/PkgInfo"

echo "==> App icon"
ICONSET="$OUT/AppIcon.iconset"
mkdir -p "$ICONSET"
for s in 16 32 128 256 512; do
  sips -z "$s" "$s" Assets/AppIcon.png --out "$ICONSET/icon_${s}x${s}.png" >/dev/null
  d=$((s * 2))
  sips -z "$d" "$d" Assets/AppIcon.png --out "$ICONSET/icon_${s}x${s}@2x.png" >/dev/null
done
iconutil -c icns "$ICONSET" -o "$APP/Contents/Resources/AppIcon.icns"

# Signing: with a Developer ID (env DEVELOPER_ID, e.g. "Developer ID Application: Anil Gul (TEAMID)")
# the app is signed with the hardened runtime and later notarized, so it opens with no
# Gatekeeper warning. Without it, an ad-hoc signature is used (needed to run on Apple Silicon).
if [ -n "${DEVELOPER_ID:-}" ]; then
  echo "==> Developer ID signature (hardened runtime)"
  codesign --force --options runtime --timestamp \
    --entitlements Resources/Oblivion.entitlements --sign "$DEVELOPER_ID" "$APP"
else
  echo "==> Ad-hoc code signature (required to run on Apple Silicon)"
  codesign --force --deep --sign - "$APP"
fi
codesign --verify --verbose=2 "$APP"

echo "==> DMG"
DMGROOT="$OUT/dmg"
mkdir -p "$DMGROOT"
cp -R "$APP" "$DMGROOT/"
ln -s /Applications "$DMGROOT/Applications"
cp "README-ilk-acilis.txt" "$DMGROOT/ÖNCE OKU - Read Me.txt"
DMG="$OUT/$APP_NAME-$VERSION-macOS.dmg"
hdiutil create -volname "$APP_NAME" -srcfolder "$DMGROOT" -ov -format UDZO "$DMG"

# Notarization (only when Apple credentials are provided): Apple scans the DMG and
# the ticket is stapled to it, so it opens directly even offline.
if [ -n "${DEVELOPER_ID:-}" ] && [ -n "${APPLE_ID:-}" ] && [ -n "${APPLE_TEAM_ID:-}" ] && [ -n "${APPLE_APP_PASSWORD:-}" ]; then
  echo "==> Sign + notarize DMG"
  codesign --force --timestamp --sign "$DEVELOPER_ID" "$DMG"
  xcrun notarytool submit "$DMG" --apple-id "$APPLE_ID" --team-id "$APPLE_TEAM_ID" \
    --password "$APPLE_APP_PASSWORD" --wait
  xcrun stapler staple "$DMG"
  xcrun stapler validate "$DMG"
  spctl --assess --type open --context context:primary-signature --verbose=2 "$DMG"
fi

echo ""
echo "Done: $DMG"
