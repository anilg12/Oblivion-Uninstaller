using Vanish.Models;

namespace Vanish.Services;

public interface IJunkCleanerService
{
    /// <summary>Creates the junk categories (unscanned, nothing selected).</summary>
    IReadOnlyList<JunkCategory> CreateCategories();

    /// <summary>Lists one category's removable entries (largest first) in the background.</summary>
    Task<(IReadOnlyList<JunkEntry> Items, long Bytes, int Count)> ScanAsync(JunkCategory category, CancellationToken ct = default);

    /// <summary>Removes exactly the given entries. Returns (removed, bytes freed, skipped).</summary>
    Task<(int Removed, long Freed, int Skipped)> CleanAsync(IReadOnlyList<JunkEntry> entries, IProgress<double>? progress = null, CancellationToken ct = default);
}
