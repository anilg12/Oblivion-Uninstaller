using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using Vanish.Helpers;
using Wpf.Ui.Controls;

namespace Vanish.Models;

/// <summary>A browser extension / add-on discovered on disk.</summary>
public sealed partial class BrowserExtension : ObservableObject
{
    [ObservableProperty] private bool _isSelected;

    public required string Name { get; init; }
    public required string Browser { get; init; }
    public required string Id { get; init; }
    public string? Version { get; init; }
    public string? Profile { get; init; }

    /// <summary>Browser executable (used for the browser icon), if known.</summary>
    public string? BrowserIcon { get; init; }

    /// <summary>Folder (Chromium) or .xpi file (Firefox) to remove.</summary>
    public required string Path { get; init; }

    public string Subtitle => string.Join(" · ", new[] { Version, Profile }.Where(s => !string.IsNullOrWhiteSpace(s)));
}

/// <summary>A running application with a visible window (Hunter mode target).</summary>
public sealed class RunningApp
{
    public required int ProcessId { get; init; }
    public required string ProcessName { get; init; }
    public required string WindowTitle { get; init; }
    public string? FilePath { get; init; }
    public string PidText => $"PID {ProcessId}";
}

/// <summary>One recorded action in the activity log (Logs database).</summary>
public sealed class LogEntry
{
    public DateTime Timestamp { get; set; }

    /// <summary>Localization key of the action (e.g. "Log_Uninstalled").</summary>
    public string ActionKey { get; set; } = "";

    /// <summary>Plain action text written by Oblivion 1.x/2.x (kept for old entries).</summary>
    public string Action { get; set; } = "";

    public string Detail { get; set; } = "";

    [JsonIgnore] public string Title => !string.IsNullOrEmpty(ActionKey) ? Loc.I[ActionKey] : Action;
    [JsonIgnore] public string TimeText => Timestamp.ToString("dd.MM.yyyy HH:mm");

    [JsonIgnore]
    public SymbolRegular Symbol => ActionKey switch
    {
        "Log_Uninstalled" => SymbolRegular.Delete24,
        "Log_Forced" => SymbolRegular.Flash24,
        "Log_Leftovers" => SymbolRegular.Sparkle24,
        "Log_Junk" => SymbolRegular.Broom24,
        "Log_Extension" => SymbolRegular.PuzzlePiece24,
        "Log_Shred" => SymbolRegular.Fire24,
        "Log_History" => SymbolRegular.History24,
        "Log_Startup" => SymbolRegular.Power24,
        "Log_Baseline" or "Log_MonitorRemoved" => SymbolRegular.EyeTracking24,
        "Log_LargeFiles" => SymbolRegular.DocumentSearch24,
        "Log_Evidence" => SymbolRegular.EraserTool24,
        "Log_Backup" or "Log_Restore" => SymbolRegular.ArchiveArrowBack24,
        "Log_StoreApp" => SymbolRegular.StoreMicrosoft24,
        "Log_EndTask" => SymbolRegular.Dismiss24,
        "Log_EntryRemoved" => SymbolRegular.DocumentDismiss24,
        "Log_Privacy" => SymbolRegular.EyeOff24,
        _ => SymbolRegular.Checkmark24
    };
}

/// <summary>A change detected by the install monitor (added program / folder / run key).</summary>
public sealed partial class MonitorChange : ObservableObject
{
    public required string Kind { get; init; }   // "Program" | "Folder" | "Registry"
    public required string Value { get; init; }

    /// <summary>For registry run values: the key path and value name.</summary>
    public string? RegistryKey { get; init; }
    public string? RegistryValue { get; init; }

    [ObservableProperty] private bool _isSelected;

    public string KindText => Kind switch
    {
        "Program" => Loc.I["Mon_KindProgram"],
        "Folder" => Loc.I["Mon_KindFolder"],
        _ => Loc.I["Mon_KindRegistry"]
    };

    public SymbolRegular Symbol => Kind switch
    {
        "Program" => SymbolRegular.Apps24,
        "Folder" => SymbolRegular.Folder24,
        _ => SymbolRegular.Key24
    };
}
