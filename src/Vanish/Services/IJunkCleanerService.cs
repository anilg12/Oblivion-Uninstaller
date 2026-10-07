using Vanish.Models;

namespace Vanish.Services;

public interface IJunkCleanerService
{
    // creates the junk categories (unscanned, nothing selected)
    IReadOnlyList<JunkCategory> CreateCategories();

    // lists one category's removable entries (largest first) in the background
    Task<(IReadOnlyList<JunkEntry> Items, long Bytes, int Count)> ScanAsync(JunkCategory category, CancellationToken ct = default);

    // removes exactly the given entries. returns (removed, bytes freed, skipped)
    Task<(int Removed, long Freed, int Skipped)> CleanAsync(IReadOnlyList<JunkEntry> entries, IProgress<double>? progress = null, CancellationToken ct = default);
}
