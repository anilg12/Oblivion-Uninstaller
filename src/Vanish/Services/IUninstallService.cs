using Vanish.Models;

namespace Vanish.Services;

public interface IUninstallService
{
    // runs the app's own uninstaller. silent + a quiet command available -> no ui.
    // progress messages go through progress
    Task<UninstallResult> RunUninstallerAsync(
        InstalledProgram program,
        bool silent,
        IProgress<string>? progress = null,
        CancellationToken ct = default);
}

public sealed record UninstallResult(bool Succeeded, int ExitCode, string? Message);
