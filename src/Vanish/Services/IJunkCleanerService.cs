using Vanish.Models;

namespace Vanish.Services;

public interface IJunkCleanerService
{
    /// <summary>Returns the scannable junk categories (unscanned; sizes are 0).</summary>
    IReadOnlyList<JunkCategory> GetCategories();

    /// <summary>Measures each selected category's size on disk, updating it in place.</summary>
    Task ScanAsync(IEnumerable<JunkCategory> categories, IProgress<string>? progress = null, CancellationToken ct = default);

    /// <summary>Deletes the contents of the selected categories. Returns bytes freed.</summary>
    Task<long> CleanAsync(IEnumerable<JunkCategory> categories, IProgress<string>? progress = null, CancellationToken ct = default);
}
