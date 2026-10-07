using Vanish.Helpers;

namespace Vanish.Models;

// classic (MSI/EXE) app from the Uninstall registry keys
public sealed class InstalledProgram
{
    // the registry sub-key name (often a product GUID or short id)
    public required string RegistryKeyName { get; init; }

    // which hive/view the entry was found in (used to re-open it)
    public required RegistryRoot Root { get; init; }

    public required string DisplayName { get; init; }
    public string? DisplayVersion { get; init; }
    public string? Publisher { get; init; }
    public string? InstallLocation { get; init; }
    public string? UninstallString { get; init; }
    public string? QuietUninstallString { get; init; }

    // raw DisplayIcon value (may be "path,index")
    public string? DisplayIcon { get; init; }

    // best icon source for the list ("path,index", .exe or .ico), resolved in the background
    public string? IconPath { get; init; }

    // estimated install size in bytes (EstimatedSize * 1024)
    public long EstimatedSizeBytes { get; init; }

    public DateOnly? InstallDate { get; init; }

    // true when the entry is an MSI product (UninstallString uses msiexec)
    public bool IsMsi { get; init; }

    // the Windows Installer product code (GUID) when IsMsi
    public string? ProductCode { get; init; }

    public string? UrlInfoAbout { get; init; }
    public string? Comments { get; init; }

    public string DisplaySize =>
        EstimatedSizeBytes > 0 ? ByteSize.Humanize(EstimatedSizeBytes) : "—";

    public string PublisherOrUnknown => string.IsNullOrWhiteSpace(Publisher) ? Loc.I["App_UnknownPublisher"] : Publisher!;

    public string InstallDateText => InstallDate?.ToString("dd.MM.yyyy") ?? "—";

    public string VersionText => string.IsNullOrWhiteSpace(DisplayVersion) ? "—" : DisplayVersion!;

    // one or two letters shown while (or instead of) the icon loading
    public string Initials
    {
        get
        {
            var words = DisplayName.Split(new[] { ' ', '-', '_', '.' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => char.IsLetterOrDigit(w[0])).ToList();
            if (words.Count == 0) return "?";
            return words.Count == 1
                ? words[0][..1].ToUpperInvariant()
                : string.Concat(words[0][0], words[1][0]).ToUpperInvariant();
        }
    }

    // architecture label derived from which hive/view the entry lives in
    public string Architecture => Root switch
    {
        RegistryRoot.LocalMachine64 => "64-bit",
        RegistryRoot.LocalMachine32 => "32-bit",
        RegistryRoot.CurrentUser => Loc.I["App_PerUser"],
        _ => ""
    };

    // full registry path of the uninstall entry (for regedit / deletion)
    public string UninstallRegistryPath => Root switch
    {
        RegistryRoot.LocalMachine64 =>
            $@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{RegistryKeyName}",
        RegistryRoot.LocalMachine32 =>
            $@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\{RegistryKeyName}",
        _ =>
            $@"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{RegistryKeyName}"
    };
}

// identifies the registry hive and bitness view an entry lives in
public enum RegistryRoot
{
    // HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall (64-bit view)
    LocalMachine64,

    // HKLM\SOFTWARE\WOW6432Node\...\Uninstall (32-bit view)
    LocalMachine32,

    // HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall
    CurrentUser
}
