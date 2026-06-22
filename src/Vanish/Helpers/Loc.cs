using System.ComponentModel;

namespace Vanish.Helpers;

/// <summary>
/// Tiny runtime localization service (Turkish / English) with live switching.
/// Usage in XAML:  Text="{Binding [Nav_AllApps], Source={x:Static h:Loc.I}}"
/// Raising "Item[]" refreshes every indexer binding when the language changes.
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    public static Loc I { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    private string _lang = "tr";

    /// <summary>"tr" or "en".</summary>
    public string Language
    {
        get => _lang;
        set
        {
            var v = (value ?? "tr").ToLowerInvariant();
            if (v != "tr" && v != "en") v = "en";
            if (_lang == v) return;
            _lang = v;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsTurkish)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEnglish)));
        }
    }

    public bool IsTurkish => _lang == "tr";
    public bool IsEnglish => _lang == "en";

    public void Toggle() => Language = _lang == "tr" ? "en" : "tr";

    /// <summary>Localized lookup. Falls back to the key itself if missing.</summary>
    public string this[string key]
    {
        get
        {
            var table = _lang == "tr" ? Tr : En;
            return table.TryGetValue(key, out var v) ? v : key;
        }
    }

    /// <summary>Direct (non-binding) lookup helper for code-behind / view-models.</summary>
    public string T(string key) => this[key];

    private static readonly Dictionary<string, string> En = new()
    {
        ["App_Title"] = "Oblivion",
        ["App_Tagline"] = "Uninstall & clean \u2014 perfectly",
        ["Brand_By"] = "by ANIL G\u00dcL",
        ["Made_By"] = "Designed & built by ANIL G\u00dcL",

        ["Nav_Dashboard"] = "Dashboard",
        ["Nav_AllApps"] = "All applications",
        ["Nav_Monitored"] = "Monitored apps",
        ["Nav_WindowsApps"] = "Windows apps",
        ["Nav_BrowserExt"] = "Browser extensions",
        ["Nav_Logs"] = "Logs database",
        ["Nav_Hunter"] = "Hunter mode",
        ["Nav_Tools"] = "Tools",
        ["Nav_Settings"] = "Settings",

        ["Act_Uninstall"] = "Uninstall",
        ["Act_ForceUninstall"] = "Force uninstall",
        ["Act_OtherCommands"] = "Other commands",
        ["Act_Refresh"] = "Refresh",
        ["Act_Cancel"] = "Cancel",
        ["Act_Analyze"] = "Analyze",
        ["Act_Clean"] = "Clean now",
        ["Act_Remove"] = "Remove",
        ["Act_SelectAll"] = "Select all",
        ["Act_SelectNone"] = "None",
        ["Act_Skip"] = "Skip",
        ["Act_DeleteSelected"] = "Delete selected",
        ["Act_Open"] = "Open",

        ["Cmd_OpenInstallFolder"] = "Open install folder",
        ["Cmd_OpenRegistry"] = "Open in Registry Editor",
        ["Cmd_SearchWeb"] = "Search the web",
        ["Cmd_OpenWebsite"] = "Open publisher website",
        ["Cmd_CopyDetails"] = "Copy details",
        ["Cmd_RemoveEntry"] = "Remove entry from list",

        ["Col_App"] = "Application",
        ["Col_Size"] = "Size",
        ["Col_Version"] = "Version",
        ["Col_Type"] = "Type",
        ["Col_InstallDate"] = "Install date",
        ["Col_Publisher"] = "Publisher",
        ["Col_Path"] = "Path",
        ["Col_Match"] = "Match",
        ["Col_Location"] = "Location",
        ["Col_Command"] = "Command",
        ["Col_Name"] = "Name",

        ["Search_Placeholder"] = "Search applications\u2026",
        ["Desc_Panel"] = "Description panel",
        ["Desc_Empty"] = "Select an application to see its details.",

        ["Stat_Installed"] = "Installed applications",
        ["Stat_Footprint"] = "Total disk footprint",
        ["Stat_Protected"] = "Protected",
        ["Stat_RestoreNote"] = "Restore point on uninstall",
        ["Recently_Installed"] = "Recently installed",

        ["Group_NewApps"] = "Applications",
        ["Status_Reading"] = "Reading installed programs\u2026",
        ["Status_Apps"] = "applications installed.",

        ["Theme"] = "Theme",
        ["Language"] = "Language",
        ["Theme_Dark"] = "Dark",
        ["Theme_Light"] = "Light",

        ["Set_Appearance"] = "Appearance",
        ["Set_DarkTheme"] = "Dark theme",
        ["Set_DarkTheme_Desc"] = "Use the dark Fluent appearance",
        ["Set_Language"] = "Interface language",
        ["Set_Language_Desc"] = "Switch between Turkish and English",
        ["Set_Behavior"] = "Uninstall behavior",
        ["Set_RestorePoint"] = "Create a System Restore point before uninstalling",
        ["Set_Silent"] = "Silent uninstall when supported",
        ["Set_RecycleBin"] = "Send deleted leftovers to the Recycle Bin",
        ["Set_About"] = "About",
        ["About_Text"] = "A modern uninstaller and system cleaner for Windows 11. Inspired by Revo Uninstaller Pro, rebuilt with a Fluent design and a deeper, safer cleanup engine.",

        ["Opt_Silent"] = "Silent",
        ["Opt_RestorePoint"] = "Restore point",
        ["Opt_RecycleBin"] = "Recycle Bin",

        ["Leftovers_Title"] = "Leftovers found",
        ["Leftovers_Msg"] = "Review these remnants. High-confidence items are pre-selected. Selected files go to the Recycle Bin.",
        ["Leftovers_None"] = "No leftovers found. Clean uninstall!",

        ["Tools_Title"] = "Tools",
        ["Tools_Subtitle"] = "Everything Revo has \u2014 and more",
        ["Tool_Startup"] = "Startup manager",
        ["Tool_Startup_Desc"] = "Programs that launch with Windows",
        ["Tool_Junk"] = "Junk cleaner",
        ["Tool_Junk_Desc"] = "Reclaim space from temp files and caches",
        ["Tool_History"] = "History & privacy cleaner",
        ["Tool_History_Desc"] = "Clear recent files, run history and traces",
        ["Tool_Evidence"] = "Evidence remover",
        ["Tool_Evidence_Desc"] = "Wipe free space so deleted files are unrecoverable",
        ["Tool_Shredder"] = "Unrecoverable delete",
        ["Tool_Shredder_Desc"] = "Securely shred files beyond recovery",
        ["Tool_Backup"] = "Backup manager",
        ["Tool_Backup_Desc"] = "Restore points and registry backups created by Oblivion",

        ["WinApps_Title"] = "Windows apps (Store / UWP)",
        ["WinApps_Subtitle"] = "Microsoft Store and built-in packaged apps",
        ["WinApps_Remove"] = "Uninstall",

        ["Monitored_Title"] = "Monitored installations",
        ["Monitored_Subtitle"] = "Trace a new install for a complete uninstall later",
        ["Monitored_Body"] = "Start monitoring, run an installer, and Oblivion records every file and registry change so it can be reversed perfectly. Real-time tracing module is in progress.",
        ["Monitored_Start"] = "Start monitoring an installation",

        ["Hunter_Title"] = "Hunter mode",
        ["Hunter_Subtitle"] = "Drag the crosshair onto any window to uninstall, stop or locate it",
        ["Hunter_Body"] = "Hunter mode lets you point at any running program \u2014 a tray icon, a window, a shortcut \u2014 and act on it instantly. Window-picking module is in progress.",

        ["Browser_Title"] = "Browser extensions",
        ["Browser_Subtitle"] = "Manage add-ons across Edge, Chrome and Firefox",
        ["Browser_Body"] = "Oblivion lists installed browser extensions so you can disable or remove them in one place.",

        ["Logs_Title"] = "Logs database",
        ["Logs_Subtitle"] = "Community uninstall logs for a deeper clean",
        ["Logs_Body"] = "When a program is uninstalled using a matching community log, Oblivion removes leftovers that even the program's own uninstaller misses.",

        ["Coming_Soon"] = "In progress",
        ["Confirm"] = "Confirm",
        ["Cancel"] = "Cancel",

        ["Col_Browser"] = "Browser",
        ["Col_Extension"] = "Extension",
        ["Browser_Empty"] = "No extensions found. Make sure your browsers are closed, then refresh.",
        ["Browser_RemoveWarn"] = "Close the browser first, otherwise it may be recreated.",

        ["Mon_TakeBaseline"] = "Take baseline snapshot",
        ["Mon_Compare"] = "Compare changes",
        ["Mon_BaselineTaken"] = "Baseline captured: {0} programs, {1} folders. Now run your installer, then Compare.",
        ["Mon_NoBaseline"] = "Take a baseline snapshot first, then install something and compare.",
        ["Mon_Added"] = "Added since baseline",

        ["Hunter_End"] = "End task",
        ["Hunter_OpenLoc"] = "Open location",
        ["Col_Window"] = "Window",
        ["Col_Process"] = "Process",

        ["Logs_Empty"] = "No activity logged yet.",
        ["Logs_Clear"] = "Clear log",
        ["Col_Date"] = "Date",
        ["Col_Action"] = "Action",
        ["Col_Detail"] = "Detail",

        ["Dash_FreeSpace"] = "Free space (system drive)",
        ["Dash_Startup"] = "Startup items",
        ["Dash_System"] = "System",
        ["Dash_Largest"] = "Largest applications",
        ["Dash_RecentActivity"] = "Recent activity",
        ["Dash_QuickActions"] = "Quick actions",
        ["Dash_NoActivity"] = "Your uninstall and cleanup history will appear here.",

        ["Ok"] = "OK",
    };

    private static readonly Dictionary<string, string> Tr = new()
    {
        ["App_Title"] = "Oblivion",
        ["App_Tagline"] = "Kald\u0131r & temizle \u2014 kusursuzca",
        ["Brand_By"] = "ANIL G\u00dcL imzas\u0131yla",
        ["Made_By"] = "ANIL G\u00dcL taraf\u0131ndan tasarland\u0131 ve geli\u015ftirildi",

        ["Nav_Dashboard"] = "Panel",
        ["Nav_AllApps"] = "T\u00fcm uygulamalar",
        ["Nav_Monitored"] = "\u0130zlenen uygulamalar",
        ["Nav_WindowsApps"] = "Windows uygulamalar\u0131",
        ["Nav_BrowserExt"] = "Taray\u0131c\u0131 eklentileri",
        ["Nav_Logs"] = "G\u00fcnl\u00fck veritaban\u0131",
        ["Nav_Hunter"] = "Avc\u0131 kipi",
        ["Nav_Tools"] = "Ara\u00e7lar",
        ["Nav_Settings"] = "Ayarlar",

        ["Act_Uninstall"] = "Kald\u0131r",
        ["Act_ForceUninstall"] = "Zorla kald\u0131r",
        ["Act_OtherCommands"] = "Di\u011fer komutlar",
        ["Act_Refresh"] = "Yenile",
        ["Act_Cancel"] = "\u0130ptal",
        ["Act_Analyze"] = "\u00c7\u00f6z\u00fcmle",
        ["Act_Clean"] = "\u015eimdi temizle",
        ["Act_Remove"] = "Kald\u0131r",
        ["Act_SelectAll"] = "T\u00fcm\u00fcn\u00fc se\u00e7",
        ["Act_SelectNone"] = "Hi\u00e7biri",
        ["Act_Skip"] = "Atla",
        ["Act_DeleteSelected"] = "Se\u00e7ilenleri sil",
        ["Act_Open"] = "A\u00e7",

        ["Cmd_OpenInstallFolder"] = "Kurulum klas\u00f6r\u00fcn\u00fc a\u00e7",
        ["Cmd_OpenRegistry"] = "Kay\u0131t Defteri'nde a\u00e7",
        ["Cmd_SearchWeb"] = "Web'de ara",
        ["Cmd_OpenWebsite"] = "\u00dcretici web sitesini a\u00e7",
        ["Cmd_CopyDetails"] = "Ayr\u0131nt\u0131lar\u0131 kopyala",
        ["Cmd_RemoveEntry"] = "Girdiyi listeden kald\u0131r",

        ["Col_App"] = "Uygulama",
        ["Col_Size"] = "Boyut",
        ["Col_Version"] = "S\u00fcr\u00fcm",
        ["Col_Type"] = "T\u00fcr",
        ["Col_InstallDate"] = "Kurulum tarihi",
        ["Col_Publisher"] = "Kurum",
        ["Col_Path"] = "Yol",
        ["Col_Match"] = "E\u015fle\u015fme",
        ["Col_Location"] = "Konum",
        ["Col_Command"] = "Komut",
        ["Col_Name"] = "Ad",

        ["Search_Placeholder"] = "Uygulama ara\u2026",
        ["Desc_Panel"] = "A\u00e7\u0131klama panosu",
        ["Desc_Empty"] = "Ayr\u0131nt\u0131lar\u0131 g\u00f6rmek i\u00e7in bir uygulama se\u00e7in.",

        ["Stat_Installed"] = "Kurulu uygulama",
        ["Stat_Footprint"] = "Toplam disk kullan\u0131m\u0131",
        ["Stat_Protected"] = "Korumal\u0131",
        ["Stat_RestoreNote"] = "Kald\u0131rmada geri y\u00fckleme noktas\u0131",
        ["Recently_Installed"] = "Son y\u00fcklenenler",

        ["Group_NewApps"] = "Uygulamalar",
        ["Status_Reading"] = "Kurulu programlar okunuyor\u2026",
        ["Status_Apps"] = "uygulama kurulu.",

        ["Theme"] = "Tema",
        ["Language"] = "Dil",
        ["Theme_Dark"] = "Koyu",
        ["Theme_Light"] = "A\u00e7\u0131k",

        ["Set_Appearance"] = "G\u00f6r\u00fcn\u00fcm",
        ["Set_DarkTheme"] = "Koyu tema",
        ["Set_DarkTheme_Desc"] = "Koyu Fluent g\u00f6r\u00fcn\u00fcm\u00fc kullan",
        ["Set_Language"] = "Aray\u00fcz dili",
        ["Set_Language_Desc"] = "T\u00fcrk\u00e7e ve \u0130ngilizce aras\u0131nda ge\u00e7i\u015f yap",
        ["Set_Behavior"] = "Kald\u0131rma davran\u0131\u015f\u0131",
        ["Set_RestorePoint"] = "Kald\u0131rmadan \u00f6nce sistem geri y\u00fckleme noktas\u0131 olu\u015ftur",
        ["Set_Silent"] = "Destekleniyorsa sessiz kald\u0131rma",
        ["Set_RecycleBin"] = "Silinen kal\u0131nt\u0131lar\u0131 Geri D\u00f6n\u00fc\u015f\u00fcm Kutusu'na g\u00f6nder",
        ["Set_About"] = "Hakk\u0131nda",
        ["About_Text"] = "Windows 11 i\u00e7in modern bir kald\u0131r\u0131c\u0131 ve sistem temizleyici. Revo Uninstaller Pro'dan ilham al\u0131nd\u0131; Fluent tasar\u0131m ve daha derin, daha g\u00fcvenli bir temizleme motoruyla s\u0131f\u0131rdan yaz\u0131ld\u0131.",

        ["Opt_Silent"] = "Sessiz",
        ["Opt_RestorePoint"] = "Geri y\u00fckleme",
        ["Opt_RecycleBin"] = "Geri D\u00f6n\u00fc\u015f\u00fcm",

        ["Leftovers_Title"] = "Kal\u0131nt\u0131lar bulundu",
        ["Leftovers_Msg"] = "Bu kal\u0131nt\u0131lar\u0131 g\u00f6zden ge\u00e7irin. Y\u00fcksek g\u00fcvenli \u00f6\u011feler \u00f6nceden se\u00e7ilidir. Se\u00e7ilen dosyalar Geri D\u00f6n\u00fc\u015f\u00fcm Kutusu'na gider.",
        ["Leftovers_None"] = "Kal\u0131nt\u0131 bulunamad\u0131. Temiz kald\u0131rma!",

        ["Tools_Title"] = "Ara\u00e7lar",
        ["Tools_Subtitle"] = "Revo'da ne varsa \u2014 fazlas\u0131yla",
        ["Tool_Startup"] = "Ba\u015flang\u0131\u00e7 y\u00f6neticisi",
        ["Tool_Startup_Desc"] = "Windows ile birlikte a\u00e7\u0131lan programlar",
        ["Tool_Junk"] = "Gereksiz dosya temizleyici",
        ["Tool_Junk_Desc"] = "Ge\u00e7ici dosya ve \u00f6nbelleklerden yer kazan\u0131n",
        ["Tool_History"] = "Ge\u00e7mi\u015f ve gizlilik temizleyici",
        ["Tool_History_Desc"] = "Son dosyalar, \u00e7al\u0131\u015ft\u0131rma ge\u00e7mi\u015fi ve izleri silin",
        ["Tool_Evidence"] = "Kan\u0131t temizleyici",
        ["Tool_Evidence_Desc"] = "Bo\u015f alan\u0131 silerek silinen dosyalar\u0131 kurtar\u0131lamaz yap\u0131n",
        ["Tool_Shredder"] = "Kurtar\u0131lamaz silme",
        ["Tool_Shredder_Desc"] = "Dosyalar\u0131 kurtar\u0131lamayacak \u015fekilde g\u00fcvenli par\u00e7alay\u0131n",
        ["Tool_Backup"] = "Yedek y\u00f6neticisi",
        ["Tool_Backup_Desc"] = "Oblivion'in olu\u015fturdu\u011fu geri y\u00fckleme ve kay\u0131t yedekleri",

        ["WinApps_Title"] = "Windows uygulamalar\u0131 (Store / UWP)",
        ["WinApps_Subtitle"] = "Microsoft Store ve yerle\u015fik paketli uygulamalar",
        ["WinApps_Remove"] = "Kald\u0131r",

        ["Monitored_Title"] = "\u0130zlenen kurulumlar",
        ["Monitored_Subtitle"] = "Yeni bir kurulumu izleyerek sonra eksiksiz kald\u0131r\u0131n",
        ["Monitored_Body"] = "\u0130zlemeyi ba\u015flat\u0131n, bir kurulum \u00e7al\u0131\u015ft\u0131r\u0131n; Oblivion her dosya ve kay\u0131t defteri de\u011fi\u015fikli\u011fini kaydeder, b\u00f6ylece kusursuzca geri al\u0131nabilir. Ger\u00e7ek zamanl\u0131 izleme mod\u00fcl\u00fc geli\u015ftiriliyor.",
        ["Monitored_Start"] = "Bir kurulumu izlemeye ba\u015fla",

        ["Hunter_Title"] = "Avc\u0131 kipi",
        ["Hunter_Subtitle"] = "Ni\u015fan i\u015faretini herhangi bir pencereye s\u00fcr\u00fckleyip kald\u0131r\u0131n, durdurun veya bulun",
        ["Hunter_Body"] = "Avc\u0131 kipi, \u00e7al\u0131\u015fan herhangi bir program\u0131 \u2014 tepsi simgesi, pencere, k\u0131sayol \u2014 i\u015faret edip an\u0131nda i\u015flem yapman\u0131z\u0131 sa\u011flar. Pencere se\u00e7me mod\u00fcl\u00fc geli\u015ftiriliyor.",

        ["Browser_Title"] = "Taray\u0131c\u0131 eklentileri",
        ["Browser_Subtitle"] = "Edge, Chrome ve Firefox eklentilerini y\u00f6netin",
        ["Browser_Body"] = "Oblivion kurulu taray\u0131c\u0131 eklentilerini listeler; hepsini tek yerden devre d\u0131\u015f\u0131 b\u0131rakabilir veya kald\u0131rabilirsiniz.",

        ["Logs_Title"] = "G\u00fcnl\u00fck veritaban\u0131",
        ["Logs_Subtitle"] = "Daha derin temizlik i\u00e7in topluluk kald\u0131rma g\u00fcnl\u00fckleri",
        ["Logs_Body"] = "Bir program e\u015fle\u015fen bir topluluk g\u00fcnl\u00fc\u011f\u00fc ile kald\u0131r\u0131ld\u0131\u011f\u0131nda, Oblivion program\u0131n kendi kald\u0131r\u0131c\u0131s\u0131n\u0131n bile atlad\u0131\u011f\u0131 kal\u0131nt\u0131lar\u0131 temizler.",

        ["Coming_Soon"] = "Geli\u015ftiriliyor",
        ["Confirm"] = "Onayla",
        ["Cancel"] = "\u0130ptal",

        ["Col_Browser"] = "Taray\u0131c\u0131",
        ["Col_Extension"] = "Eklenti",
        ["Browser_Empty"] = "Eklenti bulunamad\u0131. Taray\u0131c\u0131lar\u0131n\u0131z\u0131n kapal\u0131 oldu\u011fundan emin olup yenileyin.",
        ["Browser_RemoveWarn"] = "\u00d6nce taray\u0131c\u0131y\u0131 kapat\u0131n, aksi halde yeniden olu\u015fturulabilir.",

        ["Mon_TakeBaseline"] = "Taban anl\u0131k g\u00f6r\u00fcnt\u00fc al",
        ["Mon_Compare"] = "De\u011fi\u015fiklikleri kar\u015f\u0131la\u015ft\u0131r",
        ["Mon_BaselineTaken"] = "Taban al\u0131nd\u0131: {0} program, {1} klas\u00f6r. \u015eimdi kurulumu \u00e7al\u0131\u015ft\u0131r\u0131p kar\u015f\u0131la\u015ft\u0131r\u0131n.",
        ["Mon_NoBaseline"] = "\u00d6nce taban anl\u0131k g\u00f6r\u00fcnt\u00fc al\u0131n, sonra bir \u015fey kurup kar\u015f\u0131la\u015ft\u0131r\u0131n.",
        ["Mon_Added"] = "Tabandan beri eklenenler",

        ["Hunter_End"] = "G\u00f6revi sonland\u0131r",
        ["Hunter_OpenLoc"] = "Konumu a\u00e7",
        ["Col_Window"] = "Pencere",
        ["Col_Process"] = "\u0130\u015flem",

        ["Logs_Empty"] = "Hen\u00fcz kay\u0131t yok.",
        ["Logs_Clear"] = "G\u00fcnl\u00fc\u011f\u00fc temizle",
        ["Col_Date"] = "Tarih",
        ["Col_Action"] = "\u0130\u015flem",
        ["Col_Detail"] = "Ayr\u0131nt\u0131",

        ["Dash_FreeSpace"] = "Bo\u015f alan (sistem s\u00fcr\u00fcc\u00fcs\u00fc)",
        ["Dash_Startup"] = "Ba\u015flang\u0131\u00e7 \u00f6\u011fesi",
        ["Dash_System"] = "Sistem",
        ["Dash_Largest"] = "En b\u00fcy\u00fck uygulamalar",
        ["Dash_RecentActivity"] = "Son etkinlik",
        ["Dash_QuickActions"] = "H\u0131zl\u0131 i\u015flemler",
        ["Dash_NoActivity"] = "Kald\u0131rma ve temizleme ge\u00e7mi\u015finiz burada g\u00f6r\u00fcnecek.",

        ["Ok"] = "Tamam",
    };
}
