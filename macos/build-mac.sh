#!/usr/bin/env bash
# Builds Oblivion.app for macOS and packages three drag-to-install DMGs:
#   Oblivion-<version>-macOS-AppleSilicon.dmg   (arm64 only — M1 and newer)
#   Oblivion-<version>-macOS-Intel.dmg          (x86_64 only)
#   Oblivion-<version>-macOS-Universal.dmg      (both, runs everywhere)
# Run on macOS with Xcode / Command Line Tools:
#     cd macos && ./build-mac.sh
set -euo pipefail
cd "$(dirname "$0")"

APP_NAME="Oblivion"
VERSION="${OBLIVION_VERSION:-3.1.0}"
OUT="build"

rm -rf "$OUT"
mkdir -p "$OUT"

echo "==> Swift build (release, arm64 + x86_64)"
swift build -c release --arch arm64 --arch x86_64
BIN_DIR="$(swift build -c release --arch arm64 --arch x86_64 --show-bin-path)"
UNIVERSAL="$BIN_DIR/$APP_NAME"
echo "    binaries: $BIN_DIR"
lipo -info "$UNIVERSAL"

echo "==> Thin binaries"
lipo "$UNIVERSAL" -thin arm64 -output "$OUT/$APP_NAME-arm64"
lipo "$UNIVERSAL" -thin x86_64 -output "$OUT/$APP_NAME-x86_64"
lipo -info "$OUT/$APP_NAME-arm64"
lipo -info "$OUT/$APP_NAME-x86_64"

echo "==> App icon"
ICONSET="$OUT/AppIcon.iconset"
mkdir -p "$ICONSET"
for s in 16 32 128 256 512; do
  sips -z "$s" "$s" Assets/AppIcon.png --out "$ICONSET/icon_${s}x${s}.png" >/dev/null
  d=$((s * 2))
  sips -z "$d" "$d" Assets/AppIcon.png --out "$ICONSET/icon_${s}x${s}@2x.png" >/dev/null
done
iconutil -c icns "$ICONSET" -o "$OUT/AppIcon.icns"

# make_app <destination .app> <executable>
# Signing: with a Developer ID (env DEVELOPER_ID, e.g. "Developer ID Application: Anil Gul (TEAMID)")
# the app is signed with the hardened runtime and later notarized, so it opens with no
# Gatekeeper warning. Without it, an ad-hoc signature is used (needed to run on Apple Silicon).
make_app() {
  local app="$1" exe="$2"
  mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
  cp "$exe" "$app/Contents/MacOS/$APP_NAME"
  sed "s/__VERSION__/$VERSION/g" Resources/Info.plist > "$app/Contents/Info.plist"
  printf 'APPL????' > "$app/Contents/PkgInfo"
  cp "$OUT/AppIcon.icns" "$app/Contents/Resources/AppIcon.icns"
  if [ -n "${DEVELOPER_ID:-}" ]; then
    codesign --force --options runtime --timestamp \
      --entitlements Resources/Oblivion.entitlements --sign "$DEVELOPER_ID" "$app"
  else
    codesign --force --deep --sign - "$app"
  fi
  codesign --verify --verbose=2 "$app"
}

# make_dmg <.app> <dmg file name>
make_dmg() {
  local app="$1" name="$2"
  local root
  root="$(mktemp -d "$OUT/dmg.XXXXXX")"
  cp -R "$app" "$root/"
  ln -s /Applications "$root/Applications"
  cp "README-ilk-acilis.txt" "$root/ÖNCE OKU - Read Me.txt"
  hdiutil create -volname "$APP_NAME" -srcfolder "$root" -ov -format UDZO "$OUT/$name"
  rm -rf "$root"

  # Notarization (only when Apple credentials are provided): Apple scans the DMG and
  # the ticket is stapled to it, so it opens directly even offline.
  if [ -n "${DEVELOPER_ID:-}" ] && [ -n "${APPLE_ID:-}" ] && [ -n "${APPLE_TEAM_ID:-}" ] && [ -n "${APPLE_APP_PASSWORD:-}" ]; then
    echo "==> Sign + notarize $name"
    codesign --force --timestamp --sign "$DEVELOPER_ID" "$OUT/$name"
    xcrun notarytool submit "$OUT/$name" --apple-id "$APPLE_ID" --team-id "$APPLE_TEAM_ID" \
      --password "$APPLE_APP_PASSWORD" --wait
    xcrun stapler staple "$OUT/$name"
    xcrun stapler validate "$OUT/$name"
    spctl --assess --type open --context context:primary-signature --verbose=2 "$OUT/$name"
  fi
}

echo "==> Assemble the apps"
make_app "$OUT/$APP_NAME.app" "$UNIVERSAL"                       # universal (also used by the smoke test)
make_app "$OUT/arm64/$APP_NAME.app" "$OUT/$APP_NAME-arm64"       # Apple Silicon
make_app "$OUT/x86_64/$APP_NAME.app" "$OUT/$APP_NAME-x86_64"     # Intel

echo "==> DMGs"
make_dmg "$OUT/arm64/$APP_NAME.app" "$APP_NAME-$VERSION-macOS-AppleSilicon.dmg"
make_dmg "$OUT/x86_64/$APP_NAME.app" "$APP_NAME-$VERSION-macOS-Intel.dmg"
make_dmg "$OUT/$APP_NAME.app" "$APP_NAME-$VERSION-macOS-Universal.dmg"

echo ""
ls -lh "$OUT"/*.dmg
echo "Done."
