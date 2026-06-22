using System.Diagnostics;
using Vanish.Models;

namespace Vanish.Services;

/// <summary>
/// Hunter mode: lists running applications that own a visible window and lets the
/// user act on them (end the task, open its folder). This is the practical
/// equivalent of Revo's "hunter" crosshair — identify a running program and act.
/// </summary>
public sealed class HunterService
{
    public Task<IReadOnlyList<RunningApp>> GetRunningAppsAsync(CancellationToken ct = default)
        => Task.Run<IReadOnlyList<RunningApp>>(() =>
        {
            var apps = new List<RunningApp>();
            foreach (var proc in Process.GetProcesses())
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    if (proc.MainWindowHandle == IntPtr.Zero) continue;
                    var title = proc.MainWindowTitle;
                    if (string.IsNullOrWhiteSpace(title)) continue;

                    string? path = null;
                    try { path = proc.MainModule?.FileName; } catch { /* protected */ }

                    apps.Add(new RunningApp
                    {
                        ProcessId = proc.Id,
                        ProcessName = proc.ProcessName,
                        WindowTitle = title,
                        FilePath = path
                    });
                }
                catch { /* process exited */ }
                finally { proc.Dispose(); }
            }

            return apps
                .OrderBy(a => a.ProcessName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }, ct);

    public void EndTask(RunningApp app)
    {
        using var proc = Process.GetProcessById(app.ProcessId);
        proc.Kill(entireProcessTree: true);
    }

    public void OpenLocation(RunningApp app)
    {
        if (string.IsNullOrWhiteSpace(app.FilePath)) return;
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"/select,\"{app.FilePath}\"",
            UseShellExecute = true
        });
    }
}
