<p align="center">
  <img src="https://raw.githubusercontent.com/anilg12/Oblivion-Uninstaller/main/docs/assets/banner.svg" alt="Oblivion" width="100%">
</p>

## ⬇️ Hangi dosyayı indirmeliyim?

| Bilgisayarın | İndir |
|---|---|
| 🪟 **Windows 10 / 11** (önerilen) | `Oblivion-{version}-Windows-Setup.exe` — kurulum sihirbazı, Başlat menüsü ve masaüstü kısayolu |
| 🪟 Windows, kurulumsuz | `Oblivion-{version}-Windows-Portable.exe` — tek dosya, çift tıkla çalışır |
| 🍏 **Mac — Apple Silicon** (M1, M2, M3, M4…) | `Oblivion-{version}-macOS-AppleSilicon.dmg` |
| 🍏 **Mac — Intel işlemcili** | `Oblivion-{version}-macOS-Intel.dmg` |
| 🍏 Mac — emin değilim | `Oblivion-{version}-macOS-Universal.dmg` — her Mac'te çalışır |

> 💡 Mac'te en kolay yol (uyarısız, işlemciyi kendisi seçer): Terminal'e yapıştır →
> `curl -fsSL https://raw.githubusercontent.com/anilg12/Oblivion-Uninstaller/main/macos/install-mac.sh | bash`

## ✨ 3.0'da neler yeni

### Her iki platformda
- 📊 **Canlı Sistem İzleyici** — işlemci, bellek, **işlemci sıcaklığı**, ağ, diskler, pil ve en çok kaynak kullanan işlemler. Sayfa kapalıyken hiç kaynak tüketmez.
- 🧹 **Yeniden yazılan gereksiz dosya temizleyici** — **hiçbir şey otomatik seçilmez.** Her kategori açılır, silinecek her dosya ve klasör tek tek görünür; yalnızca senin işaretlediklerin, onay penceresindeki tam listeyle silinir.
- 🛡️ **Kalıntılar asla senin yerine işaretlenmez** — istersen tek tıkla yalnızca “kesin” eşleşmeler seçilir. Microsoft / Apple ve paylaşılan klasörler hiç listelenmez.
- ✅ Her silme işleminden önce, etkilenecek dosyaların tam listesiyle **onay**.
- ⏯️ Başlangıç öğeleri **silinmeden kapatılıp yeniden açılabilir**.
- ℹ️ Geliştirici bilgileri ana ekrandan kaldırıldı; **ⓘ Hakkında** penceresine taşındı.
- 🎞️ Daha akıcı animasyonlar ve efektler — ekran kartını yormadan; istersen **Hareketi azalt**.

### 🪟 Windows
- ⚡ **İkinci ve sonraki açılışlardaki kasma giderildi** — açılış 1 saniyenin altında, sayfa geçişleri 0,25 saniyenin altında (CI ölçümü).
- 🆕 Yeni araçlar: **Büyük dosyalar**, **Dosya parçalayıcı**, **Geçmiş ve gizlilik**, **Kanıt temizleyici**, **Yedek yöneticisi**.
- 🎯 Avcı modunda nişangâh: herhangi bir pencerenin üzerine sürükle, programı bul.
- Sıcaklık, Windows'un bildirdiği ACPI termal bölge değerinden okunur (desteklemeyen cihazlarda “Ölçülemiyor” yazar).

### 🍏 macOS
- 🧠 **Apple Silicon ve Intel için ayrı yerel derlemeler** — Apple Silicon'da sıcaklık işlemci sensörlerinden (HID), Intel'de SMC'den okunur.
- 🔋 Pil sağlığı ve döngü sayısı, pilde çalışırken daha seyrek ölçüm, pencere gizliyken ölçüm durur.
- 📋 Uygulama menüsünde “Oblivion hakkında”.

---

<details>
<summary>🇬🇧 <b>English</b></summary>

### Which file do I need?

| Your computer | Download |
|---|---|
| 🪟 **Windows 10 / 11** (recommended) | `Oblivion-{version}-Windows-Setup.exe` — installer with Start menu and desktop shortcuts |
| 🪟 Windows, no install | `Oblivion-{version}-Windows-Portable.exe` — a single file, just run it |
| 🍏 **Mac — Apple Silicon** (M1, M2, M3, M4…) | `Oblivion-{version}-macOS-AppleSilicon.dmg` |
| 🍏 **Mac — Intel** | `Oblivion-{version}-macOS-Intel.dmg` |
| 🍏 Mac — not sure | `Oblivion-{version}-macOS-Universal.dmg` — runs on every Mac |

Easiest on a Mac (no Gatekeeper prompt, picks the right build): `curl -fsSL https://raw.githubusercontent.com/anilg12/Oblivion-Uninstaller/main/macos/install-mac.sh | bash`

### What's new in 3.0
- 📊 **Live System Monitor** — CPU, memory, **CPU temperature**, network, disks, battery and the busiest processes; costs nothing while it's closed.
- 🧹 **Rewritten junk cleaner** — **nothing is pre-selected.** Every category opens to show each file and folder; only what you tick is removed, after a confirmation with the full list.
- 🛡️ **Leftovers are never ticked for you** — one click selects only the exact matches if you want. Microsoft / Apple and shared folders are never offered.
- ✅ A confirmation with the exact list of files before every destructive action.
- ⏯️ Startup items can be switched off and back on without deleting them.
- ℹ️ The developer credits moved off the main screen into an **ⓘ About** window.
- 🎞️ Smoother animations and effects that stay light on the GPU, plus a **Reduce motion** option.
- 🪟 **Windows:** the stutter on later launches is gone (start-up under a second, page switches under 0.25 s in CI); new Large files, File shredder, History & privacy, Evidence remover and Backup manager tools; a crosshair in Hunter mode.
- 🍏 **macOS:** separate native builds for Apple Silicon and Intel with their own temperature readers (HID sensors / SMC); battery health and cycle count; sampling slows down on battery and stops while the window is hidden.

</details>
