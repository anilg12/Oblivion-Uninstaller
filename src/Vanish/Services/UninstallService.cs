using System.Diagnostics;
using System.IO;
using Vanish.Helpers;
using Vanish.Models;

namespace Vanish.Services;

// starts the program's own uninstaller (MSI or EXE) and waits until it's really done.
// lots of uninstallers copy themselves to %TEMP%, relaunch from there and exit right away,
// so we wait for those child processes too before scanning
public sealed class UninstallService : IUninstallService
{
    private static readonly HashSet<string> IgnoredChildren = new(StringComparer.OrdinalIgnoreCase)
    {
        "chrome.exe", "msedge.exe", "firefox.exe", "opera.exe", "brave.exe", "vivaldi.exe", "iexplore.exe",
        "explorer.exe", "conhost.exe", "werfault.exe", "dllhost.exe", "rundll32.exe", "msedgewebview2.exe"
    };

    public async Task<UninstallResult> RunUninstallerAsync(
        InstalledProgram program,
        bool silent,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var command = ResolveCommand(program, silent);
        if (command is null)
            return new UninstallResult(false, -1, Loc.I["Uninst_NoCommand"]);

        var (fileName, arguments) = command.Value;
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = true, // let the installer UI surface normally
            Verb = "runas"
        };

        try
        {
            using var proc = Process.Start(psi);
            if (proc is null)
                return new UninstallResult(false, -1, Loc.I["Uninst_StartFailed"]);

            int rootPid = proc.Id;
            await proc.WaitForExitAsync(ct);
            int code = proc.ExitCode;

            progress?.Report("Work_WaitUninstaller");
            await WaitForChildUninstallersAsync(rootPid, program.InstallLocation, ct);

            // msiexec: 0 success, 3010 success+reboot, 1605 already removed
            bool ok = code is 0 or 3010 or 1605 or 1641;
            return new UninstallResult(ok, code, ok ? null : string.Format(Loc.I["Uninst_ExitCodeFmt"], code));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return new UninstallResult(false, 1223, Loc.I["Uninst_Declined"]); // UAC "No"
        }
        catch (Exception ex)
        {
            return new UninstallResult(false, -1, ex.Message);
        }
    }

    // wait while child processes that look like uninstaller stages are running (copies in %TEMP%,
    // msiexec, files in the install folder, unins*/Au_/Un_A). browsers opened for "sorry to see
    // you go" pages are ignored
    private static async Task WaitForChildUninstallersAsync(int rootPid, string? installLocation, CancellationToken ct)
    {
        var tracked = new HashSet<int> { rootPid };
        var temp = Path.GetTempPath().TrimEnd('\\');
        var sw = Stopwatch.StartNew();
        int quietRounds = 0;

        while (sw.Elapsed < TimeSpan.FromMinutes(30))
        {
            ct.ThrowIfCancellationRequested();
            var tree = Native.ProcessTree();
            bool added;
            do
            {
                added = false;
                foreach (var (pid, parent, _) in tree)
                    if (tracked.Contains(parent) && tracked.Add(pid)) added = true;
            } while (added);

            bool waiting = false;
            foreach (var (pid, _, exe) in tree)
            {
                if (pid == rootPid || !tracked.Contains(pid)) continue;
                if (IgnoredChildren.Contains(exe)) continue;
                if (LooksLikeUninstallerStage(pid, exe, temp, installLocation)) { waiting = true; break; }
            }

            if (!waiting)
            {
                // the next stage can start a bit after the first process exits, so wait for a few quiet checks
                if (++quietRounds >= 3) return;
            }
            else quietRounds = 0;

            await Task.Delay(500, ct);
        }
    }

    private static bool LooksLikeUninstallerStage(int pid, string exe, string temp, string? installLocation)
    {
        var lower = exe.ToLowerInvariant();
        if (lower is "msiexec.exe" or "au_.exe" or "un_a.exe" or "un_b.exe" ||
            lower.StartsWith("unins") || lower.StartsWith("_iu") || lower.Contains("uninst") || lower.EndsWith(".tmp"))
            return true;
        var path = Native.ProcessPath(pid);
        if (path is null) return false;
        if (path.StartsWith(temp, StringComparison.OrdinalIgnoreCase)) return true;
        return !string.IsNullOrWhiteSpace(installLocation) &&
               path.StartsWith(installLocation!.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase);
    }

    // (exe, args) to run. MSI commands get normalized to /x, quiet switches added if requested
    private static (string FileName, string Arguments)? ResolveCommand(InstalledProgram program, bool silent)
    {
        if (program.IsMsi && !string.IsNullOrWhiteSpace(program.ProductCode))
        {
            var args = $"/x {program.ProductCode}";
            if (silent) args += " /qn /norestart";
            return ("msiexec.exe", args);
        }

        var raw = silent && !string.IsNullOrWhiteSpace(program.QuietUninstallString)
            ? program.QuietUninstallString
            : program.UninstallString;

        if (string.IsNullOrWhiteSpace(raw))
            return null;

        return SplitCommandLine(raw!);
    }

    // split a command line into exe + args, respecting quotes.
    // "C:\App\unins000.exe" /SILENT -> ("C:\App\unins000.exe", "/SILENT")
    private static (string FileName, string Arguments) SplitCommandLine(string commandLine)
    {
        commandLine = Environment.ExpandEnvironmentVariables(commandLine.Trim());

        if (commandLine.StartsWith('"'))
        {
            int closing = commandLine.IndexOf('"', 1);
            if (closing > 0)
                return (commandLine.Substring(1, closing - 1), commandLine[(closing + 1)..].Trim());
        }

        int exeIdx = commandLine.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (exeIdx > 0)
        {
            int end = exeIdx + 4;
            return (commandLine[..end], commandLine[end..].Trim());
        }

        int space = commandLine.IndexOf(' ');
        return space < 0
            ? (commandLine, string.Empty)
            : (commandLine[..space], commandLine[(space + 1)..].Trim());
    }
}
