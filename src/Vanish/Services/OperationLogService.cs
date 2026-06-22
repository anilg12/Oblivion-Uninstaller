using System.IO;
using System.Text;
using System.Text.Json;
using Vanish.Models;

namespace Vanish.Services;

/// <summary>
/// Persists a simple activity log (one JSON object per line) of the operations
/// Oblivion performs — shown in the "Logs database" section.
/// </summary>
public sealed class OperationLogService
{
    private static readonly string LogFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Oblivion", "activity.log");

    private readonly object _gate = new();

    public void Append(string action, string detail)
    {
        try
        {
            lock (_gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogFile)!);
                var entry = new LogEntry { Timestamp = DateTime.Now, Action = action, Detail = detail };
                File.AppendAllText(LogFile, JsonSerializer.Serialize(entry) + "\n", new UTF8Encoding(false));
            }
        }
        catch { /* logging must never throw */ }
    }

    public IReadOnlyList<LogEntry> GetAll()
    {
        try
        {
            if (!File.Exists(LogFile)) return Array.Empty<LogEntry>();
            var entries = new List<LogEntry>();
            foreach (var line in File.ReadAllLines(LogFile))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    var e = JsonSerializer.Deserialize<LogEntry>(line);
                    if (e is not null) entries.Add(e);
                }
                catch { /* skip malformed */ }
            }
            entries.Reverse(); // newest first
            return entries;
        }
        catch
        {
            return Array.Empty<LogEntry>();
        }
    }

    public void Clear()
    {
        try { if (File.Exists(LogFile)) File.Delete(LogFile); }
        catch { /* ignore */ }
    }
}
