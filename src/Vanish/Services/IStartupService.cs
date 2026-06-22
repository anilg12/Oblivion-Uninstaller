using Vanish.Models;

namespace Vanish.Services;

public interface IStartupService
{
    Task<IReadOnlyList<StartupEntry>> GetStartupEntriesAsync(CancellationToken ct = default);

    /// <summary>Removes an auto-start entry entirely.</summary>
    Task DeleteAsync(StartupEntry entry, CancellationToken ct = default);
}
