using Vanish.Models;

namespace Vanish.Services;

public interface IStartupService
{
    Task<IReadOnlyList<StartupEntry>> GetStartupEntriesAsync(CancellationToken ct = default);

    /// <summary>Enables or disables an entry the way Task Manager does (StartupApproved), without deleting it.</summary>
    Task SetEnabledAsync(StartupEntry entry, bool enabled, CancellationToken ct = default);

    /// <summary>Removes an auto-start entry entirely (registry value backed up first).</summary>
    Task DeleteAsync(StartupEntry entry, CancellationToken ct = default);
}
