using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Vanish.Models;

namespace Vanish.Services;

[JsonSerializable(typeof(LogEntry))]
internal sealed partial class LogJsonContext : JsonSerializerContext { }

/// <summary>
/// The activity log ("Logs database"): one JSON object per line in
/// %LOCALAPPDATA%\Oblivion\activity.log. The file is read once (in the background)
/// and kept in memory, so navigating never re-parses it on the UI thread.
/// </summary>
public sealed class OperationLogService
{
    private static readonly string LogFile = Path.Combine(SettingsService.DataDir, "activity.log");
    private const int MaxEntries = 2000;

    private readonly object _gate = new();
    private List<LogEntry>? _entries; // newest first
    private Task? _loading;

    /// <summary>Raised (on any thread) after an entry is appended or the log is cleared.</summary>
    public event Action? Changed;

    /// <summary>Starts reading the log file in the background (call once at startup).</summary>
    public Task WarmUpAsync() => _loading ??= Task.Run(EnsureLoaded);

    public void Append(string actionKey, string detail)
    {
        var entry = new LogEntry { Timestamp = DateTime.Now, ActionKey = actionKey, Detail = detail };
        try
        {
            EnsureLoaded();
            lock (_gate)
            {
                _entries!.Insert(0, entry);
                if (_entries.Count > MaxEntries) _entries.RemoveRange(MaxEntries, _entries.Count - MaxEntries);
                Directory.CreateDirectory(SettingsService.DataDir);
                File.AppendAllText(LogFile, JsonSerializer.Serialize(entry, LogJsonContext.Default.LogEntry) + "\n", new UTF8Encoding(false));
            }
        }
        catch { /* logging must never throw */ }
        Changed?.Invoke();
    }

    public IReadOnlyList<LogEntry> GetAll()
    {
        EnsureLoaded();
        lock (_gate) return _entries!.ToList();
    }

    public IReadOnlyList<LogEntry> GetRecent(int count)
    {
        EnsureLoaded();
        lock (_gate) return _entries!.Take(count).ToList();
    }

    public void Clear()
    {
        lock (_gate)
        {
            _entries = new List<LogEntry>();
            try { if (File.Exists(LogFile)) File.Delete(LogFile); } catch { /* ignore */ }
        }
        Changed?.Invoke();
    }

    private void EnsureLoaded()
    {
        lock (_gate)
        {
            if (_entries is not null) return;
            var list = new List<LogEntry>();
            try
            {
                if (File.Exists(LogFile))
                {
                    foreach (var line in File.ReadLines(LogFile))
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        try
                        {
                            var e = JsonSerializer.Deserialize(line, LogJsonContext.Default.LogEntry);
                            if (e is not null) list.Add(e);
                        }
                        catch { /* skip malformed */ }
                    }
                }
            }
            catch { /* unreadable -> empty */ }
            list.Reverse();
            if (list.Count > MaxEntries) list.RemoveRange(MaxEntries, list.Count - MaxEntries);
            _entries = list;
        }
    }
}
