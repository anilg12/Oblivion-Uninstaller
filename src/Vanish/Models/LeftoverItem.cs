using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Vanish.Helpers;
using Wpf.Ui.Controls;

namespace Vanish.Models;

/// <summary>A single remnant (file, folder, registry key/value or shortcut) found after an uninstall.</summary>
public sealed partial class LeftoverItem : ObservableObject
{
    public required LeftoverKind Kind { get; init; }

    /// <summary>Full filesystem path or registry key path (HKCU\…, HKLM\…).</summary>
    public required string Path { get; init; }

    /// <summary>For <see cref="LeftoverKind.RegistryValue"/>: the value name inside <see cref="Path"/>.</summary>
    public string? ValueName { get; init; }

    /// <summary>Size in bytes for files/folders; 0 for registry items.</summary>
    public long SizeBytes { get; init; }

    public required MatchConfidence Confidence { get; init; }

    /// <summary>Localization key explaining why this item matched.</summary>
    public string ReasonKey { get; init; } = "Reason_Name";

    [ObservableProperty] private bool _isSelected;

    public string Name => Kind == LeftoverKind.RegistryValue
        ? ValueName ?? Path
        : System.IO.Path.GetFileName(Path.TrimEnd('\\')) is { Length: > 0 } n ? n : Path;

    public string Location => Path;
    public string Reason => Loc.I[ReasonKey];

    public string DisplaySize => Kind is LeftoverKind.RegistryKey or LeftoverKind.RegistryValue
        ? Loc.I["Left_Registry"]
        : ByteSize.Humanize(SizeBytes);

    public string ConfidenceText => Confidence switch
    {
        MatchConfidence.High => Loc.I["Conf_High"],
        MatchConfidence.Medium => Loc.I["Conf_Medium"],
        _ => Loc.I["Conf_Low"]
    };

    public Color ConfidenceColor => Confidence switch
    {
        MatchConfidence.High => Color.FromRgb(0x22, 0xC5, 0x5E),
        MatchConfidence.Medium => Color.FromRgb(0xF5, 0xA5, 0x24),
        _ => Color.FromRgb(0x94, 0x96, 0xA8)
    };

    public SymbolRegular Symbol => Kind switch
    {
        LeftoverKind.File => SymbolRegular.Document24,
        LeftoverKind.Folder => SymbolRegular.Folder24,
        LeftoverKind.Shortcut => SymbolRegular.Link24,
        LeftoverKind.RegistryValue => SymbolRegular.Power24,
        _ => SymbolRegular.Key24
    };

    public bool IsRegistry => Kind is LeftoverKind.RegistryKey or LeftoverKind.RegistryValue;
}

public enum LeftoverKind
{
    File,
    Folder,
    Shortcut,
    RegistryKey,
    RegistryValue
}

public enum MatchConfidence
{
    /// <summary>Exact product-name match, install folder or orphaned uninstall entry.</summary>
    High,

    /// <summary>Strong but indirect match (exe name, install folder name elsewhere).</summary>
    Medium,

    /// <summary>Only a loose name match, or a folder shared with other software — review carefully.</summary>
    Low
}

/// <summary>
/// What we know about an app before its uninstaller runs (the install folder may be
/// gone afterwards), used to find what it left behind.
/// </summary>
public sealed class AppFingerprint
{
    public required string DisplayName { get; init; }
    public string? Publisher { get; init; }
    public string? InstallLocation { get; init; }
    public string? UninstallKeyPath { get; init; }
    public IReadOnlyList<string> ExeNames { get; init; } = Array.Empty<string>();
}
