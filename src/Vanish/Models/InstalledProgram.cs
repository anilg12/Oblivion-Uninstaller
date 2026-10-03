using Vanish.Helpers;

namespace Vanish.Models;

/// <summary>
/// A classic (MSI / EXE) application discovered through the Windows
/// "Uninstall" registry hives.
/// </summary>
public sealed class InstalledProgram
{
    /// <summary>The registry sub-key name (often a product GUID or short id).</summary>
    public required string RegistryKeyName { get; init; }

    /// <summary>Which hive/view the entry was found in (used to re-open it).</summary>
    public required RegistryRoot Root { get; init; }

    public required string DisplayName { get; init; }
    public string? DisplayVersion { get; init; }
    public string? Publisher { get; init; }
    public string? InstallLocation { get; init; }
    public string? UninstallString { get; init; }
    public string? QuietUninstallString { get; init; }

    /// <summary>Raw DisplayIcon value (may be "path,index").</summary>
    public string? DisplayIcon { get; init; }

    /// <summary>Best icon source for the list ("path,index", .exe or .ico), resolved in the background.</summary>
    public string? IconPath { get; init; }

    /// <summary>Estimated install size in bytes (EstimatedSize * 1024).</summary>
    public long EstimatedSizeBytes { get; init; }

    public DateOnly? InstallDate { get; init; }

    /// <summary>True when the entry is an MSI product (UninstallString uses msiexec).</summary>
    public bool IsMsi { get; init; }

    /// <summary>The Windows Installer product code (GUID) when <see cref="IsMsi"/>.</summary>
    public string? ProductCode { get; init; }

    public string? UrlInfoAbout { get; init; }
    public string? Comments { get; init; }

    public string DisplaySize =>
        EstimatedSizeBytes > 0 ? ByteSize.Humanize(EstimatedSizeBytes) : "—";

    public string PublisherOrUnknown => string.IsNullOrWhiteSpace(Publisher) ? Loc.I["App_UnknownPublisher"] : Publisher!;

    public string InstallDateText => InstallDate?.ToString("dd.MM.yyyy") ?? "—";

    public string VersionText => string.IsNullOrWhiteSpace(DisplayVersion) ? "—" : DisplayVersion!;

    /// <summary>One or two letters shown while (or instead of) the icon loading.</summary>
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

    /// <summary>Architecture label derived from which hive/view the entry lives in.</summary>
    public string Architecture => Root switch
    {
        RegistryRoot.LocalMachine64 => "64-bit",
        RegistryRoot.LocalMachine32 => "32-bit",
        RegistryRoot.CurrentUser => Loc.I["App_PerUser"],
        _ => ""
    };

    /// <summary>Full registry path of the uninstall entry (for regedit / deletion).</summary>
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

/// <summary>Identifies the registry hive and bitness view an entry lives in.</summary>
public enum RegistryRoot
{
    /// <summary>HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall (64-bit view).</summary>
    LocalMachine64,

    /// <summary>HKLM\SOFTWARE\WOW6432Node\...\Uninstall (32-bit view).</summary>
    LocalMachine32,

    /// <summary>HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall.</summary>
    CurrentUser
}
