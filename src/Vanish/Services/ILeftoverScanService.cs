using Vanish.Models;

namespace Vanish.Services;

public interface ILeftoverScanService
{
    /// <summary>
    /// Scans the filesystem and registry for remnants of a (just) uninstalled program.
    /// </summary>
    Task<IReadOnlyList<LeftoverItem>> ScanAsync(
        InstalledProgram program,
        IProgress<string>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes the selected leftovers. Files/folders go to the Recycle Bin when
    /// <paramref name="useRecycleBin"/> is true; registry keys are removed directly
    /// (a backup is taken by the caller beforehand).
    /// </summary>
    Task<int> DeleteAsync(
        IEnumerable<LeftoverItem> items,
        bool useRecycleBin,
        IProgress<string>? progress = null,
        CancellationToken ct = default);
}
