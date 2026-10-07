namespace Vanish.Services;

public interface ISystemRestoreService
{
    // creates a System Restore point (best effort, false when System Restore is off)
    Task<bool> CreateRestorePointAsync(string description, CancellationToken ct = default);

    // exports a registry key to a .reg file in the backup folder, returns its path or null
    Task<string?> BackupRegistryKeyAsync(string registryPath, CancellationToken ct = default);

    Task<IReadOnlyList<RegistryBackupInfo>> ListRegistryBackupsAsync();
    Task<bool> RestoreRegistryBackupAsync(string file, CancellationToken ct = default);
    Task<IReadOnlyList<RestorePointInfo>> ListRestorePointsAsync();
}
