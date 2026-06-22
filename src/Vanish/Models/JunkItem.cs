using CommunityToolkit.Mvvm.ComponentModel;

namespace Vanish.Models;

/// <summary>A category of removable junk (temp files, caches, etc.).</summary>
public sealed partial class JunkCategory : ObservableObject
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Glyph { get; init; }

    /// <summary>Filesystem roots scanned for this category.</summary>
    public required IReadOnlyList<string> Paths { get; init; }

    [ObservableProperty]
    private bool _isSelected = true;

    [ObservableProperty]
    private long _sizeBytes;

    [ObservableProperty]
    private int _fileCount;

    public string DisplaySize => Helpers.ByteSize.Humanize(SizeBytes);
}
