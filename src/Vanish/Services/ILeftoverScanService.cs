using Vanish.Models;

namespace Vanish.Services;

public interface ILeftoverScanService
{
    // records what is needed to find leftovers before the uninstaller removes the app
    AppFingerprint CaptureFingerprint(InstalledProgram program);

    // scans files + registry for leftovers. nothing pre-selected, every item shows why it matched
    // and how sure we are. folders containing another installed program drop to low confidence
    Task<IReadOnlyList<LeftoverItem>> ScanAsync(
        AppFingerprint fingerprint,
        IReadOnlyList<string> otherInstallLocations,
        IProgress<string>? progress = null,
        CancellationToken ct = default);

    // deletes leftovers. files/folders go to the recycle bin if useRecycleBin, registry items
    // get exported to a .reg backup first. protected locations are refused
    Task<(int Removed, long Freed, int Failed)> DeleteAsync(
        IReadOnlyList<LeftoverItem> items,
        bool useRecycleBin,
        IProgress<string>? progress = null,
        CancellationToken ct = default);
}
