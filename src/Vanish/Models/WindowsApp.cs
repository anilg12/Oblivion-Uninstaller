namespace Vanish.Models;

/// <summary>A packaged Microsoft Store / UWP / MSIX application.</summary>
public sealed class WindowsApp
{
    public required string Name { get; init; }
    public required string PackageFullName { get; init; }
    public string? Publisher { get; init; }
    public string? Version { get; init; }
    public string? InstallLocation { get; init; }

    /// <summary>Absolute path to the package logo PNG, resolved from its manifest.</summary>
    public string? LogoPath { get; init; }

    /// <summary>True for system/framework packages that should not normally be removed.</summary>
    public bool IsFramework { get; init; }

    public string PublisherOrUnknown => string.IsNullOrWhiteSpace(Publisher) ? "—" : Publisher!;
}
