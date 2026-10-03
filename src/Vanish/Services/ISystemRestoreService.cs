namespace Vanish.Services;

public interface ISystemRestoreService
{
    /// <summary>Creates a System Restore point (best effort; false when System Restore is off).</summary>
    Task<bool> CreateRestorePointAsync(string description, CancellationToken ct = default);

    /// <summary>Exports a registry key to a .reg file in the backup folder; returns its path or null.</summary>
    Task<string?> BackupRegistryKeyAsync(string registryPath, CancellationToken ct = default);

    Task<IReadOnlyList<RegistryBackupInfo>> ListRegistryBackupsAsync();
    Task<bool> RestoreRegistryBackupAsync(string file, CancellationToken ct = default);
    Task<IReadOnlyList<RestorePointInfo>> ListRestorePointsAsync();
}
