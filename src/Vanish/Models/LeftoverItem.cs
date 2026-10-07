using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Vanish.Helpers;
using Wpf.Ui.Controls;

namespace Vanish.Models;

// a single remnant (file, folder, registry key/value or shortcut) found after an uninstall
public sealed partial class LeftoverItem : ObservableObject
{
    public required LeftoverKind Kind { get; init; }

    // full filesystem path or registry key path (HKCU\..., HKLM\...)
    public required string Path { get; init; }

    // for LeftoverKind.RegistryValue: the value name inside Path
    public string? ValueName { get; init; }

    // size in bytes for files/folders, 0 for registry items
    public long SizeBytes { get; init; }

    public required MatchConfidence Confidence { get; init; }

    // localization key explaining why this item matched
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
    // exact product-name match, install folder or orphaned uninstall entry
    High,

    // strong but indirect match (exe name, install folder name elsewhere)
    Medium,

    // loose name match or a folder shared with other software, check carefully
    Low
}

// info about the app taken before its uninstaller runs (the install folder may be gone after),
// used to find the leftovers
public sealed class AppFingerprint
{
    public required string DisplayName { get; init; }
    public string? Publisher { get; init; }
    public string? InstallLocation { get; init; }
    public string? UninstallKeyPath { get; init; }
    public IReadOnlyList<string> ExeNames { get; init; } = Array.Empty<string>();
}
