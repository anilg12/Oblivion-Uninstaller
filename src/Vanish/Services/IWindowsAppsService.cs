using Vanish.Models;

namespace Vanish.Services;

public interface IWindowsAppsService
{
    // enumerates installed Store / UWP / MSIX packages via PowerShell
    Task<IReadOnlyList<WindowsApp>> GetWindowsAppsAsync(CancellationToken ct = default);

    // removes a packaged app (Remove-AppxPackage)
    Task<bool> RemoveAsync(WindowsApp app, CancellationToken ct = default);
}
