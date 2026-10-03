#!/usr/bin/env bash
# Oblivion — one-line installer for macOS.
#   curl -fsSL https://raw.githubusercontent.com/anilg12/Oblivion-Uninstaller/main/macos/install-mac.sh | bash
#
# Picks the build for this Mac (Apple Silicon or Intel) from the latest GitHub release,
# copies Oblivion.app to /Applications and opens it. Files fetched with curl are not
# tagged as "downloaded from the internet", so macOS opens the app without the
# "Apple could not verify…" prompt.
set -euo pipefail

REPO="anilg12/Oblivion-Uninstaller"
DMG="${OBLIVION_DMG:-}"            # CI: use a local DMG instead of downloading
TMP="$(mktemp -d)"
MNT="$TMP/mnt"
trap 'hdiutil detach -quiet "$MNT" >/dev/null 2>&1 || true; rm -rf "$TMP"' EXIT

# hw.optional.arm64 is 1 on Apple Silicon even when this shell runs under Rosetta.
if [ "$(sysctl -n hw.optional.arm64 2>/dev/null || echo 0)" = "1" ]; then
  ARCH="AppleSilicon"
else
  ARCH="Intel"
fi

echo "==> Oblivion kuruluyor / installing… ($ARCH)"
if [ -z "$DMG" ]; then
  URLS="$(curl -fsSL "https://api.github.com/repos/$REPO/releases/latest" \
    | grep -o '"browser_download_url": *"[^"]*\.dmg"' | sed -E 's/.*"(https[^"]+)"$/\1/' || true)"
  URL="$(printf '%s\n' "$URLS" | grep -- "-$ARCH\.dmg$" | head -1 || true)"
  [ -n "$URL" ] || URL="$(printf '%s\n' "$URLS" | grep -- "-Universal\.dmg$" | head -1 || true)"
  [ -n "$URL" ] || URL="$(printf '%s\n' "$URLS" | head -1 || true)"
  if [ -z "$URL" ]; then
    echo "!! İndirme bağlantısı bulunamadı / no macOS download found: https://github.com/$REPO/releases"
    exit 1
  fi
  echo "    $URL"
  DMG="$TMP/Oblivion.dmg"
  curl -fL --progress-bar "$URL" -o "$DMG"
fi

mkdir -p "$MNT"
hdiutil attach -nobrowse -readonly -quiet -mountpoint "$MNT" "$DMG"

if pgrep -xq Oblivion; then osascript -e 'quit app "Oblivion"' >/dev/null 2>&1 || true; sleep 1; fi

DEST="/Applications/Oblivion.app"
if [ -w /Applications ]; then
  rm -rf "$DEST"
  cp -R "$MNT/Oblivion.app" "$DEST"
  xattr -dr com.apple.quarantine "$DEST" 2>/dev/null || true
else
  echo "    (Applications klasörü için Mac şifren istenecek / your Mac password is needed)"
  sudo rm -rf "$DEST"
  sudo cp -R "$MNT/Oblivion.app" "$DEST"
  sudo xattr -dr com.apple.quarantine "$DEST" 2>/dev/null || true
fi

echo "==> Tamam! Oblivion /Applications klasöründe. / Done!"
if [ -z "${OBLIVION_NO_OPEN:-}" ]; then open "$DEST"; fi
