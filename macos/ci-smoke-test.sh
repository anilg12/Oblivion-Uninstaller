#!/usr/bin/env bash
# CI smoke test (run after build-mac.sh on a macOS runner).
# Creates a fake app with typical leftovers, launches Oblivion in snapshot mode
# (it captures every page, uninstalls the fake app, force-uninstalls a "ghost"
# app and quits), then reports what was removed. Output: build/snapshots/
set -uo pipefail
cd "$(dirname "$0")"

OUT="$PWD/build/snapshots"
rm -rf "$OUT"
mkdir -p "$OUT"
L="$HOME/Library"

echo "==> Fixtures"
APP="$HOME/Applications/OblivionTestApp.app"
mkdir -p "$APP/Contents/MacOS"
cat > "$APP/Contents/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleIdentifier</key><string>com.oblivion.testapp</string>
  <key>CFBundleName</key><string>OblivionTestApp</string>
  <key>CFBundleShortVersionString</key><string>1.2.3</string>
  <key>CFBundleExecutable</key><string>OblivionTestApp</string>
  <key>CFBundlePackageType</key><string>APPL</string>
</dict>
</plist>
PLIST
printf '#!/bin/sh\nexit 0\n' > "$APP/Contents/MacOS/OblivionTestApp"
chmod +x "$APP/Contents/MacOS/OblivionTestApp"
dd if=/dev/zero of="$APP/Contents/MacOS/payload.bin" bs=1048576 count=6 2>/dev/null

LEFTOVERS=(
  "$L/Application Support/OblivionTestApp"
  "$L/Application Support/com.oblivion.testapp"
  "$L/Caches/com.oblivion.testapp"
  "$L/Saved Application State/com.oblivion.testapp.savedState"
  "$L/Logs/OblivionTestApp"
  "$L/HTTPStorages/com.oblivion.testapp"
  "$L/Containers/com.oblivion.testapp"
  "$L/Application Support/com.oblivion.ghost"
)
for d in "${LEFTOVERS[@]}"; do
  mkdir -p "$d" && echo "test data" > "$d/data.txt" || echo "   (could not create $d)"
done
defaults write com.oblivion.testapp lastRun -string today
defaults write com.oblivion.ghost lastRun -string today
mkdir -p "$L/LaunchAgents"
cat > "$L/LaunchAgents/com.oblivion.testapp.helper.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>Label</key><string>com.oblivion.testapp.helper</string>
  <key>ProgramArguments</key><array><string>/usr/bin/true</string></array>
  <key>RunAtLoad</key><false/>
</dict>
</plist>
PLIST
# Decoy that must NOT be touched.
mkdir -p "$L/Application Support/UnrelatedVendor" && echo keep > "$L/Application Support/UnrelatedVendor/keep.txt"
sleep 2

CHECK=(
  "$APP"
  "${LEFTOVERS[@]}"
  "$L/Preferences/com.oblivion.testapp.plist"
  "$L/Preferences/com.oblivion.ghost.plist"
  "$L/LaunchAgents/com.oblivion.testapp.helper.plist"
)
echo "== Before ==" > "$OUT/fixtures.txt"
for f in "${CHECK[@]}"; do [ -e "$f" ] && echo "present  $f" || echo "MISSING  $f"; done >> "$OUT/fixtures.txt"

echo "==> Launch Oblivion in snapshot mode"
OBLIVION_SNAPSHOT_DIR="$OUT" ./build/Oblivion.app/Contents/MacOS/Oblivion > "$OUT/app-log.txt" 2>&1 &
PID=$!
sleep 14
( screencapture -x "$OUT/screen-composited.png" >/dev/null 2>&1 & ) ; sleep 4

for _ in $(seq 1 300); do
  kill -0 "$PID" 2>/dev/null || break
  sleep 1
done
TIMED_OUT=0
if kill -0 "$PID" 2>/dev/null; then
  TIMED_OUT=1
  echo "::warning::Oblivion was still running after the timeout; killing it"
  kill "$PID"
fi
wait "$PID"
STATUS=$?
echo "app exit status: $STATUS (timed out: $TIMED_OUT)"

{
  echo ""
  echo "== After =="
  for f in "${CHECK[@]}"; do [ -e "$f" ] && echo "STILL THERE  $f" || echo "removed      $f"; done
  [ -e "$L/Application Support/UnrelatedVendor/keep.txt" ] && echo "decoy kept   (good)" || echo "DECOY REMOVED (bad!)"
  echo ""
  echo "== Trash =="
  ls -1 "$HOME/.Trash" 2>&1 | head -40
} >> "$OUT/fixtures.txt"

echo "------------------------------------------------------------"
cat "$OUT/report.txt" 2>/dev/null || echo "(no report.txt)"
echo "------------------------------------------------------------"
cat "$OUT/fixtures.txt"
echo "------------------------------------------------------------"
echo "app log:"; tail -50 "$OUT/app-log.txt"
ls -1 "$OUT"

# A crash (signal) fails the job; a timeout only warns.
if [ "$TIMED_OUT" = 0 ] && [ "$STATUS" -ne 0 ]; then
  echo "::error::Oblivion exited with status $STATUS"
  ls -t "$HOME/Library/Logs/DiagnosticReports/" 2>/dev/null | grep -i oblivion | head -1 | while read -r r; do
    cp "$HOME/Library/Logs/DiagnosticReports/$r" "$OUT/" ; head -120 "$HOME/Library/Logs/DiagnosticReports/$r"
  done
  exit 1
fi
exit 0
