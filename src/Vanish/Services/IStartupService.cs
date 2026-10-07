using Vanish.Models;

namespace Vanish.Services;

public interface IStartupService
{
    Task<IReadOnlyList<StartupEntry>> GetStartupEntriesAsync(CancellationToken ct = default);

    // enables or disables an entry the way Task Manager does (StartupApproved), without deleting it
    Task SetEnabledAsync(StartupEntry entry, bool enabled, CancellationToken ct = default);

    // removes an auto-start entry entirely (registry value backed up first)
    Task DeleteAsync(StartupEntry entry, CancellationToken ct = default);
}
