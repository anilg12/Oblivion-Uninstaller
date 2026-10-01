OBLIVION for macOS  -  ANIL GÜL
==================================

KURULUM (Türkçe)
----------------
1) Oblivion simgesini sağdaki "Applications" (Uygulamalar) klasörüne sürükle.
2) Uygulamalar klasöründen Oblivion'u çift tıklayarak aç.

"Oblivion açılamıyor / geliştirici doğrulanamadı" uyarısı çıkarsa:
   Bu uygulama Apple'ın ücretli geliştirici imzasıyla değil, yerel imzayla
   gelir. Bir kerelik izin vermen yeterli:
   a) Sistem Ayarları > Gizlilik ve Güvenlik'e git.
   b) Aşağıda "Oblivion engellendi" satırının yanındaki
      "Yine de Aç" düğmesine bas, parolanı gir.
   c) Oblivion'u tekrar aç ve "Aç" de.

   Alternatif (Terminal ile tek satır):
      xattr -dr com.apple.quarantine /Applications/Oblivion.app

EN İYİ SONUÇ İÇİN: Tam Disk Erişimi
   Kalıntıları (Containers, Mail, Safari vb.) eksiksiz bulabilmesi için:
   Sistem Ayarları > Gizlilik ve Güvenlik > Tam Disk Erişimi > + > Oblivion
   (Oblivion içindeki Panel'de de tek tıkla açabileceğin bir düğme var.)

Silinen her şey önce Çöp Sepeti'ne gider; yanlışlıkla silinenleri geri alabilirsin.


INSTALLATION (English)
----------------------
1) Drag the Oblivion icon onto the "Applications" folder.
2) Open Oblivion from your Applications folder.

If macOS says "Oblivion can't be opened / developer cannot be verified":
   The app is ad-hoc signed (not notarized). Allow it once:
   a) Open System Settings > Privacy & Security.
   b) Next to "Oblivion was blocked", click "Open Anyway" and confirm.
   c) Open Oblivion again and click "Open".

   Alternative (Terminal, one line):
      xattr -dr com.apple.quarantine /Applications/Oblivion.app

FOR BEST RESULTS: Full Disk Access
   System Settings > Privacy & Security > Full Disk Access > + > Oblivion

Everything Oblivion removes goes to the Trash first, so it can be restored.
