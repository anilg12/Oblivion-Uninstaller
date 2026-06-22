using Vanish.Models;

namespace Vanish.Services;

public interface IUninstallService
{
    /// <summary>
    /// Runs the application's own uninstaller. When <paramref name="silent"/> is true
    /// and a quiet uninstall command is available, it runs without UI.
    /// Progress messages are reported through <paramref name="progress"/>.
    /// </summary>
    Task<UninstallResult> RunUninstallerAsync(
        InstalledProgram program,
        bool silent,
        IProgress<string>? progress = null,
        CancellationToken ct = default);
}

public sealed record UninstallResult(bool Succeeded, int ExitCode, string? Message);
