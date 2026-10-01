# Oblivion for macOS

Native **SwiftUI** version of Oblivion for Apple Silicon (M1–M4) and Intel Macs,
running on **macOS 14 Sonoma, 15 Sequoia and 26 Tahoe**. Same feature set and
design language as the Windows app — rebuilt around how macOS actually stores apps.

**Tasarım & geliştirme: ANIL GÜL**

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
| **Design** | Daccord-style layout: icon rail, labelled navigation, frosted-glass window, right profile panel, animations, light/dark/system theme, Turkish & English. |

Everything Oblivion removes goes to the **Trash** first (except the Shredder and cache cleaning).

## Install

**Easiest (no Gatekeeper prompt):** open Terminal and run

```bash
curl -fsSL https://raw.githubusercontent.com/anilg12/Oblivion-Uninstaller/main/macos/install-mac.sh | bash
```

**Or manually:**

1. Download `Oblivion-<version>-macOS.dmg` from the GitHub release.
2. Drag **Oblivion** onto **Applications**.
3. First launch: the app is ad-hoc signed (not notarized), so macOS will block it once.
   Go to **System Settings → Privacy & Security → Open Anyway**, or run:
   `xattr -dr com.apple.quarantine /Applications/Oblivion.app`
4. Recommended: grant **Full Disk Access** (Settings button inside the app).

## Build it yourself

Requires macOS 14+ with Xcode 15 or newer.

```bash
cd macos
./build-mac.sh            # -> build/Oblivion-2.0.0-macOS.dmg  (universal arm64 + x86_64)
```

CI builds the same DMG on every change to `macos/` (`.github/workflows/build-macos.yml`).
Running that workflow manually with `release_tag: v2.0.0` attaches the DMG to the release.
