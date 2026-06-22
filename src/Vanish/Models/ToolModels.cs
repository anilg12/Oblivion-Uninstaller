namespace Vanish.Models;

/// <summary>A browser extension / add-on discovered on disk.</summary>
public sealed class BrowserExtension
{
    public required string Name { get; init; }
    public required string Browser { get; init; }
    public required string Id { get; init; }
    public string? Version { get; init; }

    /// <summary>Folder (Chromium) or .xpi file (Firefox) to remove.</summary>
    public required string Path { get; init; }
}

/// <summary>A running application with a visible window (Hunter mode target).</summary>
public sealed class RunningApp
{
    public required int ProcessId { get; init; }
    public required string ProcessName { get; init; }
    public required string WindowTitle { get; init; }
    public string? FilePath { get; init; }
}

/// <summary>One recorded action in the activity log (Logs database).</summary>
public sealed class LogEntry
{
    public DateTime Timestamp { get; set; }
    public string Action { get; set; } = "";
    public string Detail { get; set; } = "";

    public string TimeText => Timestamp.ToString("yyyy-MM-dd HH:mm");
}

/// <summary>A change detected by the install monitor (added program / folder).</summary>
public sealed class MonitorChange
{
    public required string Kind { get; init; }   // "Program" | "Folder" | "Registry"
    public required string Value { get; init; }

    public string Glyph => Kind switch
    {
        "Program" => "",
        "Folder" => "",
        _ => ""
    };
}
