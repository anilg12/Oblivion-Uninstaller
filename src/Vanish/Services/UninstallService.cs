using System.Diagnostics;
using Vanish.Models;

namespace Vanish.Services;

/// <summary>
/// Launches a program's native uninstaller. Handles both MSI products (msiexec /x)
/// and EXE uninstallers, parsing the registry UninstallString into an executable
/// and arguments.
/// </summary>
public sealed class UninstallService : IUninstallService
{
    public async Task<UninstallResult> RunUninstallerAsync(
        InstalledProgram program,
        bool silent,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var command = ResolveCommand(program, silent);
        if (command is null)
            return new UninstallResult(false, -1, "No uninstall command is registered for this program.");

        var (fileName, arguments) = command.Value;
        progress?.Report($"Running: {fileName} {arguments}".Trim());

        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = true, // let UAC/installer UI surface normally
            Verb = "runas"
        };

        try
        {
            using var proc = Process.Start(psi);
            if (proc is null)
                return new UninstallResult(false, -1, "Failed to start the uninstaller process.");

            await proc.WaitForExitAsync(ct);

            int code = proc.ExitCode;
            // msiexec returns 0 (success) or 3010 (success, reboot required).
            bool ok = code is 0 or 3010 or 1605 /* product already removed */;
            progress?.Report(ok
                ? "Uninstaller finished."
                : $"Uninstaller exited with code {code}.");

            return new UninstallResult(ok, code,
                ok ? null : $"The uninstaller returned exit code {code}.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new UninstallResult(false, -1, ex.Message);
        }
    }

    /// <summary>
    /// Produces the (executable, arguments) pair to launch, normalising MSI commands
    /// to a clean uninstall (/x) and appending quiet switches when requested.
    /// </summary>
    private static (string FileName, string Arguments)? ResolveCommand(InstalledProgram program, bool silent)
    {
        // MSI: build a guaranteed-correct command from the product code.
        if (program.IsMsi && !string.IsNullOrWhiteSpace(program.ProductCode))
        {
            var args = $"/x {program.ProductCode}";
            if (silent) args += " /qn /norestart";
            return ("msiexec.exe", args);
        }

        // Prefer a registered quiet command when running silently.
        var raw = silent && !string.IsNullOrWhiteSpace(program.QuietUninstallString)
            ? program.QuietUninstallString
            : program.UninstallString;

        if (string.IsNullOrWhiteSpace(raw))
            return null;

        return SplitCommandLine(raw!);
    }

    /// <summary>
    /// Splits a raw command line into executable + arguments, respecting quotes.
    /// e.g. <c>"C:\App\unins000.exe" /SILENT</c> -> ("C:\App\unins000.exe", "/SILENT").
    /// </summary>
    private static (string FileName, string Arguments) SplitCommandLine(string commandLine)
    {
        commandLine = commandLine.Trim();

        if (commandLine.StartsWith('"'))
        {
            int closing = commandLine.IndexOf('"', 1);
            if (closing > 0)
            {
                var exe = commandLine.Substring(1, closing - 1);
                var args = commandLine[(closing + 1)..].Trim();
                return (exe, args);
            }
        }

        // Unquoted: split on the first space after a ".exe" token when present.
        int exeIdx = commandLine.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (exeIdx > 0)
        {
            int end = exeIdx + 4;
            var exe = commandLine[..end];
            var args = commandLine[end..].Trim();
            return (exe, args);
        }

        // Fallback: first whitespace-delimited token is the executable.
        int space = commandLine.IndexOf(' ');
        return space < 0
            ? (commandLine, string.Empty)
            : (commandLine[..space], commandLine[(space + 1)..].Trim());
    }
}
