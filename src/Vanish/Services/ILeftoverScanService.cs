using Vanish.Models;

namespace Vanish.Services;

public interface ILeftoverScanService
{
    /// <summary>Records what is needed to find leftovers before the uninstaller removes the app.</summary>
    AppFingerprint CaptureFingerprint(InstalledProgram program);

    /// <summary>
    /// Scans the filesystem and registry for remnants of an app. Nothing is pre-selected:
    /// the user reviews the list (each item shows why it matched and how sure we are).
    /// Folders that contain another installed program are downgraded to low confidence.
    /// </summary>
    Task<IReadOnlyList<LeftoverItem>> ScanAsync(
        AppFingerprint fingerprint,
        IReadOnlyList<string> otherInstallLocations,
        IProgress<string>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes the given leftovers. Files/folders go to the Recycle Bin when
    /// <paramref name="useRecycleBin"/> is true; registry items are exported to a .reg
    /// backup first. Protected locations are refused.
    /// </summary>
    Task<(int Removed, long Freed, int Failed)> DeleteAsync(
        IReadOnlyList<LeftoverItem> items,
        bool useRecycleBin,
        IProgress<string>? progress = null,
        CancellationToken ct = default);
}
