using CommunityToolkit.Mvvm.ComponentModel;

namespace Vanish.Models;

/// <summary>A single remnant (file, folder or registry key) found after an uninstall.</summary>
public sealed partial class LeftoverItem : ObservableObject
{
    public required LeftoverKind Kind { get; init; }

    /// <summary>Full filesystem path or registry path.</summary>
    public required string Path { get; init; }

    /// <summary>Size in bytes for files/folders; 0 for registry keys.</summary>
    public long SizeBytes { get; init; }

    /// <summary>
    /// How confident the scanner is that this belongs to the removed app.
    /// Only High-confidence items are pre-selected.
    /// </summary>
    public required MatchConfidence Confidence { get; init; }

    /// <summary>Short human reason this item was matched (shown in the UI).</summary>
    public string? Reason { get; init; }

    [ObservableProperty]
    private bool _isSelected;

    public string DisplaySize => Kind == LeftoverKind.RegistryKey
        ? "registry"
        : Helpers.ByteSize.Humanize(SizeBytes);

    public string KindGlyph => Kind switch
    {
        LeftoverKind.File => "",        // Document
        LeftoverKind.Folder => "",      // Folder
        LeftoverKind.RegistryKey => "", // Repair/keys
        _ => ""
    };
}

public enum LeftoverKind
{
    File,
    Folder,
    RegistryKey
}

public enum MatchConfidence
{
    /// <summary>Path/key contains the install location or exact product code.</summary>
    High,

    /// <summary>Name + publisher both match.</summary>
    Medium,

    /// <summary>Only a loose name match — review carefully.</summary>
    Low
}
