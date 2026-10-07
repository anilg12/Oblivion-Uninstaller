using Vanish.Models;

namespace Vanish.Services;

public interface IInstalledProgramsService
{
    // classic (MSI/EXE) apps from all uninstall keys, without os updates, system components
    // and orphaned entries
    Task<IReadOnlyList<InstalledProgram>> GetInstalledProgramsAsync(CancellationToken ct = default);

    // deletes only the uninstall registry entry ("Remove entry" for broken ones), files stay
    void RemoveUninstallEntry(InstalledProgram program);

    // true while the program's uninstall entry still exists (e.g. the uninstaller was cancelled)
    bool StillInstalled(InstalledProgram program);
}
