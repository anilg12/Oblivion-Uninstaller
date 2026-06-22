namespace Vanish.Services;

public interface ISystemRestoreService
{
    /// <summary>
    /// Creates a System Restore point before a destructive operation.
    /// Returns true on success. Best-effort: failures are reported but not thrown.
    /// </summary>
    Task<bool> CreateRestorePointAsync(string description, CancellationToken ct = default);

    /// <summary>
    /// Exports the given registry key to a .reg backup file under the app's
    /// backup directory and returns the backup path (or null on failure).
    /// </summary>
    Task<string?> BackupRegistryKeyAsync(string registryPath, CancellationToken ct = default);
}
