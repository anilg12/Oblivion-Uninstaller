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

echo "==> Ad-hoc code signature (required to run on Apple Silicon)"
codesign --force --deep --sign - "$APP"
codesign --verify --verbose=2 "$APP"

echo "==> DMG"
DMGROOT="$OUT/dmg"
mkdir -p "$DMGROOT"
cp -R "$APP" "$DMGROOT/"
ln -s /Applications "$DMGROOT/Applications"
cp "README-ilk-acilis.txt" "$DMGROOT/ÖNCE OKU - Read Me.txt"
DMG="$OUT/$APP_NAME-$VERSION-macOS.dmg"
hdiutil create -volname "$APP_NAME" -srcfolder "$DMGROOT" -ov -format UDZO "$DMG"

echo ""
echo "Done: $DMG"
