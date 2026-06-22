using Vanish.Models;

namespace Vanish.Services;

public interface IInstalledProgramsService
{
    /// <summary>
    /// Enumerates classic (MSI/EXE) installed applications from every uninstall hive.
    /// Filters out OS updates, system components and orphaned entries.
    /// </summary>
    Task<IReadOnlyList<InstalledProgram>> GetInstalledProgramsAsync(CancellationToken ct = default);

    /// <summary>
    /// Deletes the program's own uninstall registry entry (used by "Remove entry"
    /// for broken/orphaned items). Does not touch the program's files.
    /// </summary>
    void RemoveUninstallEntry(InstalledProgram program);
}
