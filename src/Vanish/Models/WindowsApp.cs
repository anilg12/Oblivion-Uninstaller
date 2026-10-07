namespace Vanish.Models;

// a packaged Microsoft Store / UWP / MSIX application
public sealed class WindowsApp
{
    public required string Name { get; init; }
    public required string PackageFullName { get; init; }
    public string? Publisher { get; init; }
    public string? Version { get; init; }
    public string? InstallLocation { get; init; }

    // absolute path to the package logo PNG, resolved from its manifest
    public string? LogoPath { get; init; }

    // true for system/framework packages that should not normally be removed
    public bool IsFramework { get; init; }

    public string PublisherOrUnknown => string.IsNullOrWhiteSpace(Publisher) ? "—" : Publisher!;
}
