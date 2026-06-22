using Vanish.Models;

namespace Vanish.Services;

public interface IWindowsAppsService
{
    /// <summary>Enumerates installed Store / UWP / MSIX packages via PowerShell.</summary>
    Task<IReadOnlyList<WindowsApp>> GetWindowsAppsAsync(CancellationToken ct = default);

    /// <summary>Removes a packaged app (Remove-AppxPackage).</summary>
    Task<bool> RemoveAsync(WindowsApp app, CancellationToken ct = default);
}
