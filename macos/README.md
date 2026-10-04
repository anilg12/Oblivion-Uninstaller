# Oblivion for macOS

Native **SwiftUI** version of Oblivion for Apple Silicon (M1–M4) and Intel Macs,
running on **macOS 14 Sonoma, 15 Sequoia and 26 Tahoe**. Same feature set and
design language as the Windows app — rebuilt around how macOS actually stores apps.

## Features

| Section | What it does on macOS |
|---|---|
| **All applications** | Every `.app` in `/Applications` and `~/Applications` with real icons, size, version, architecture (Apple Silicon / Universal / Intel), install & last-opened dates. Sortable, searchable. |
| **Uninstall** | Quits the app, moves it to the Trash, then deep-scans `~/Library` and `/Library` (Application Support, Caches, Preferences, Containers, Group Containers, Saved State, LaunchAgents/Daemons, PrivilegedHelperTools, cookies, logs…) and `pkgutil` receipts. Each leftover gets a confidence score; you review before anything is removed. |
| **Force uninstall** | Aggressive scan by name or bundle ID — even for apps that are already deleted or broken. Drag an app onto the dialog to fill it in. |
| **Other commands** | Open, Show in Finder, Show package contents, Get Info, copy details, search the web. |
| **App Store apps** | Mac App Store apps (`_MASReceipt`), removable with admin approval. |
| **Monitored installs** | Baseline → install → compare: see (and remove) everything an installer added. |
| **Browser extensions** | Chrome, Edge, Brave, Arc, Vivaldi, Opera, Firefox (+ Safari, read-only). |
| **Hunter mode** | Click any app window and Oblivion locks onto it: quit, force quit, reveal or uninstall. |
| **Logs database** | Persistent history of everything Oblivion did. |
| **Tools** | Startup manager (LaunchAgents/Daemons), Junk cleaner, Large file finder, Shredder, History & privacy cleaner. |
| **System monitor** | Live CPU, memory, CPU temperature (Apple Silicon HID sensors / Intel SMC), network, disks, battery health and the busiest processes. Sampling stops while nothing shows it. |
| **Design** | Daccord-style layout: icon rail, labelled navigation, frosted-glass window, live right panel, animations (with a Reduce motion setting), light/dark/system theme, Turkish & English. About sheet from the (i) button or the app menu. |

Nothing is ever pre-selected, every removal is confirmed with the exact list, and everything goes to the **Trash** first (except the Shredder and cache / log / Trash cleaning).

## Install

**Easiest (no Gatekeeper prompt):** open Terminal and run

```bash
curl -fsSL https://raw.githubusercontent.com/anilg12/Oblivion-Uninstaller/main/macos/install-mac.sh | bash
```

**Or manually:**

1. Download the DMG for your Mac from the GitHub release: `…-macOS-AppleSilicon.dmg` (M1 and newer), `…-macOS-Intel.dmg`, or `…-macOS-Universal.dmg` (both).
2. Drag **Oblivion** onto **Applications**.
3. First launch: the app is ad-hoc signed (not notarized), so macOS will block it once.
   Go to **System Settings → Privacy & Security → Open Anyway**, or run:
   `xattr -dr com.apple.quarantine /Applications/Oblivion.app`
4. Recommended: grant **Full Disk Access** (Settings button inside the app).

## Build it yourself

Requires macOS 14+ with Xcode 15 or newer.

```bash
cd macos
./build-mac.sh            # -> build/Oblivion-3.0.0-macOS-{AppleSilicon,Intel,Universal}.dmg
```

CI builds the DMGs and runs the self-test on Apple Silicon and Intel runners on every change to
`macos/` (`.github/workflows/build-macos.yml`). Pushing a `v*` tag publishes a release with the
Windows and Mac downloads (`.github/workflows/release.yml`).
