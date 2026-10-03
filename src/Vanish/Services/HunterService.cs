using System.Diagnostics;
using System.Text;
using Vanish.Helpers;
using Vanish.Models;

namespace Vanish.Services;

/// <summary>
/// Hunter mode: drag the crosshair onto any window to identify its program, or pick
/// from the list of running apps with visible windows.
/// </summary>
public sealed class HunterService
{
    public Task<IReadOnlyList<RunningApp>> GetRunningAppsAsync(CancellationToken ct = default)
        => Task.Run<IReadOnlyList<RunningApp>>(() =>
        {
            var apps = new List<RunningApp>();
            int self = Environment.ProcessId;
            foreach (var proc in Process.GetProcesses())
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    if (proc.Id == self || proc.MainWindowHandle == IntPtr.Zero) continue;
                    var title = proc.MainWindowTitle;
                    if (string.IsNullOrWhiteSpace(title)) continue;
                    apps.Add(new RunningApp
                    {
                        ProcessId = proc.Id,
                        ProcessName = proc.ProcessName,
                        WindowTitle = title,
                        FilePath = Native.ProcessPath(proc.Id)
                    });
                }
                catch { /* process exited */ }
                finally { proc.Dispose(); }
            }
            return apps.OrderBy(a => a.ProcessName, StringComparer.CurrentCultureIgnoreCase).ToList();
        }, ct);

    /// <summary>The program owning the top-level window under the mouse cursor (null for our own window).</summary>
    public RunningApp? AppUnderCursor()
    {
        if (!Native.GetCursorPos(out var pt)) return null;
        var hwnd = Native.WindowFromPoint(pt);
        if (hwnd == IntPtr.Zero) return null;
        var root = Native.GetAncestor(hwnd, Native.GA_ROOT);
        if (root != IntPtr.Zero) hwnd = root;
        Native.GetWindowThreadProcessId(hwnd, out int pid);
        if (pid == 0 || pid == Environment.ProcessId) return null;

        var sb = new StringBuilder(512);
        Native.GetWindowText(hwnd, sb, sb.Capacity);
        string name;
        try
        {
            using var p = Process.GetProcessById(pid);
            name = p.ProcessName;
        }
        catch { name = $"PID {pid}"; }

        return new RunningApp
        {
            ProcessId = pid,
            ProcessName = name,
            WindowTitle = sb.Length > 0 ? sb.ToString() : name,
            FilePath = Native.ProcessPath(pid)
        };
    }

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
