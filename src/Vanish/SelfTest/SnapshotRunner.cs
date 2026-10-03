using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Vanish.Controls;
using Vanish.Helpers;
using Vanish.Models;
using Vanish.Services;
using Vanish.ViewModels;
using Vanish.ViewModels.Pages;
using Vanish.Views;

namespace Vanish.SelfTest;

/// <summary>
/// CI self-test, only active when OBLIVION_SNAPSHOT_DIR is set.
/// <list type="bullet">
/// <item><c>OBLIVION_SELFTEST_MODE=perf</c>: measures start-up and page-switch times, then exits.</item>
/// <item>default (<c>full</c>): screenshots of every page (dark/light, TR/EN), the About dialog,
/// the junk cleaner with a category opened, and a leftover-scan safety check on a fixture app.</item>
/// </list>
/// Everything is written to report.json in that folder.
/// </summary>
public static class SnapshotRunner
{
    private static readonly string[] Pages =
    {
        "Dashboard", "Uninstaller", "Monitored", "WindowsApps", "BrowserExt", "SystemMonitor", "Logs", "Hunter", "Tools",
        "Junk", "Startup", "LargeFiles", "Shredder", "History", "Evidence", "Backups", "Settings"
    };

    private static readonly BindingErrorListener BindingErrors = new();

    /// <summary>Called before the main window is created so binding errors of the first page are caught too.</summary>
    public static void Prepare(string dir)
    {
        Directory.CreateDirectory(dir);
        CrashLog.ProgressFile = Path.Combine(dir, "progress.log");
        PresentationTraceSources.Refresh();
        PresentationTraceSources.DataBindingSource.Listeners.Add(BindingErrors);
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning;
    }

    private sealed class BindingErrorListener : TraceListener
    {
        private readonly StringBuilder _line = new();
        public List<string> Messages { get; } = new();
        public override void Write(string? message) => _line.Append(message);
        public override void WriteLine(string? message)
        {
            _line.Append(message);
            lock (Messages) Messages.Add(_line.ToString());
            _line.Clear();
        }
    }

    /// <summary>Writes a report for a crash that stopped the self-test.</summary>
    public static void WriteFailure(string dir, Exception ex)
    {
        try
        {
            Directory.CreateDirectory(dir);
            var report = new Dictionary<string, object?> { ["errors"] = new[] { ex.ToString() }, ["fatal"] = true };
            lock (BindingErrors.Messages)
                report["bindingErrors"] = BindingErrors.Messages.Distinct().Take(300).ToList();
            File.WriteAllText(Path.Combine(dir, "report.json"),
                JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
        }
        catch { /* ignore */ }
    }

    public static async Task RunAsync(MainWindow window, string dir)
    {
        CrashLog.Mark("selftest: started");
        var report = new Dictionary<string, object?>();
        var errors = new List<string>();
        try
        {
            Directory.CreateDirectory(dir);
            window.Width = 1440;
            window.Height = 900;
            window.WindowState = WindowState.Normal;
            await Idle();
            await Task.Delay(1500);
            report["firstRenderMs"] = MainWindow.FirstRenderMs;
            report["renderTier"] = RenderCapability.Tier >> 16;

            var mode = Environment.GetEnvironmentVariable("OBLIVION_SELFTEST_MODE") ?? "full";
            CrashLog.Mark($"selftest: mode {mode}, first render {MainWindow.FirstRenderMs} ms");
            report["mode"] = mode;
            if (mode == "perf") await PerfAsync(window, report);
            else await FullAsync(window, dir, report, errors);
        }
        catch (Exception ex)
        {
            errors.Add(ex.ToString());
        }
        finally
        {
            lock (BindingErrors.Messages)
                report["bindingErrors"] = BindingErrors.Messages.Distinct().Take(300).ToList();
            report["errors"] = errors;
            CrashLog.Mark($"selftest: finished with {errors.Count} error(s)");
            try
            {
                File.WriteAllText(Path.Combine(dir, "report.json"),
                    JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
            }
            catch { /* nothing else we can do */ }
            Application.Current.Shutdown(errors.Count == 0 ? 0 : 3);
        }
    }

    // ----------------------------------------------------------------- perf

    private static async Task PerfAsync(MainWindow window, Dictionary<string, object?> report)
    {
        // Let the dashboard finish its background loads first.
        await Task.Delay(2500);
        var nav = new Dictionary<string, long>();
        foreach (var tag in Pages)
        {
            CrashLog.Mark($"perf: {tag}");
            var sw = Stopwatch.StartNew();
            window.NavigateTo(tag);
            await Idle();
            nav[tag] = sw.ElapsedMilliseconds;
            await Task.Delay(350);
        }
        report["navigationMs"] = nav;
        report["navigationMaxMs"] = nav.Values.Max();

        // Frames rendered while switching pages (animation smoothness).
        int frames = 0;
        void OnFrame(object? s, EventArgs e) => frames++;
        CompositionTarget.Rendering += OnFrame;
        var clock = Stopwatch.StartNew();
        for (int i = 0; i < 6; i++)
        {
            window.NavigateTo(i % 2 == 0 ? "Tools" : "Dashboard");
            await Task.Delay(400);
        }
        CompositionTarget.Rendering -= OnFrame;
        report["transitionFps"] = Math.Round(frames / clock.Elapsed.TotalSeconds, 1);
        report["logEntries"] = Ioc.Resolve<OperationLogService>().GetAll().Count;
        report["workingSetMb"] = Environment.WorkingSet / 1024 / 1024;
    }

    // ----------------------------------------------------------------- full

    private static async Task FullAsync(MainWindow window, string dir, Dictionary<string, object?> report, List<string> errors)
    {
        var settings = Ioc.Resolve<SettingsService>().Current;
        var main = Ioc.Resolve<MainWindowViewModel>();
        Reveal.AnimationsEnabled = false;

        // Make sure the slower lists are loaded before the screenshots.
        await Ioc.Resolve<UninstallerViewModel>().EnsureLoadedAsync(false);
        try { await Ioc.Resolve<WindowsAppsViewModel>().EnsureLoadedAsync(false); } catch (Exception ex) { errors.Add("store: " + ex.Message); }

        var shots = new List<string>();
        foreach (var (theme, lang) in new[] { ("dark", "tr"), ("dark", "en"), ("light", "tr") })
        {
            settings.Theme = theme;
            if (Loc.I.Language != lang)
            {
                Loc.I.Language = lang;
                settings.Language = lang;
                main.RefreshAllTexts();
            }
            await Idle();
            foreach (var tag in Pages)
            {
                if (lang == "en" && tag is not ("Dashboard" or "Junk" or "SystemMonitor" or "Uninstaller" or "Tools")) continue;
                CrashLog.Mark($"full: {theme}/{lang}/{tag}");
                window.NavigateTo(tag);
                await Idle();
                await Task.Delay(tag is "SystemMonitor" or "Junk" or "Dashboard" ? 2600 : 900);
                shots.Add(Save(window, dir, $"{theme}-{lang}-{tag}"));
                CheckSymbols(window, tag, errors);
            }
        }

        // The junk cleaner with its first non-empty category opened (nothing ticked).
        settings.Theme = "dark";
        Loc.I.Language = "tr";
        settings.Language = "tr";
        main.RefreshAllTexts();
        window.NavigateTo("Junk");
        var junk = Ioc.Resolve<JunkCleanerViewModel>();
        for (int i = 0; i < 40 && junk.IsScanning; i++) await Task.Delay(250);
        report["junkSelectedAfterScan"] = junk.SelectedCount;
        if (junk.SelectedCount != 0) errors.Add("Junk cleaner pre-selected items");
        if (junk.Categories.FirstOrDefault(c => c.HasItems) is { } cat)
        {
            cat.IsExpanded = true;
            await Idle();
            await Task.Delay(600);
            shots.Add(Save(window, dir, "dark-tr-Junk-expanded"));
            CheckSymbols(window, "Junk (expanded)", errors);
            cat.IsExpanded = false;
        }

        // About dialog.
        window.NavigateTo("Dashboard");
        await Idle();
        _ = main.ShowAboutCommand.ExecuteAsync(null);
        await Task.Delay(900);
        shots.Add(Save(window, dir, "dark-tr-About"));
        CheckSymbols(window, "About", errors);
        Ioc.Resolve<DialogService>().Close(false);
        await Task.Delay(300);

        // Leftover safety check on the CI fixture (if the workflow installed one).
        await LeftoverCheckAsync(window, dir, report, errors, shots);

        report["screenshots"] = shots;
    }

    private static async Task LeftoverCheckAsync(MainWindow window, string dir, Dictionary<string, object?> report, List<string> errors, List<string> shots)
    {
        var fixtureName = Environment.GetEnvironmentVariable("OBLIVION_FIXTURE_APP");
        if (string.IsNullOrWhiteSpace(fixtureName)) return;

        var apps = Ioc.Resolve<UninstallerViewModel>();
        await apps.EnsureLoadedAsync(true);
        var program = apps.FindByName(fixtureName);
        if (program is null)
        {
            errors.Add($"Fixture '{fixtureName}' not found in the program list");
            return;
        }

        var scan = Ioc.Resolve<ILeftoverScanService>();
        var fp = scan.CaptureFingerprint(program);
        var all = await Ioc.Resolve<IInstalledProgramsService>().GetInstalledProgramsAsync();
        var others = all.Where(p => p.RegistryKeyName != program.RegistryKeyName).Select(p => p.InstallLocation ?? "").ToList();
        var found = await scan.ScanAsync(fp, others);

        report["fixtureFound"] = found.Select(f => new { f.Path, f.ValueName, Kind = f.Kind.ToString(), Confidence = f.Confidence.ToString(), f.IsSelected }).ToList();
        foreach (var f in found)
        {
            if (f.IsSelected) errors.Add($"Leftover pre-selected: {f.Path}");
            bool isFile = f.Kind is LeftoverKind.File or LeftoverKind.Folder or LeftoverKind.Shortcut;
            if (isFile && LeftoverScanService.IsProtectedPath(f.Path)) errors.Add($"Protected path offered: {f.Path}");
            if (f.Kind == LeftoverKind.RegistryKey && LeftoverScanService.IsProtectedRegistryPath(f.Path)) errors.Add($"Protected key offered: {f.Path}");
        }

        // Screenshot of the review screen with these results (nothing is deleted).
        window.NavigateTo("Uninstaller");
        apps.ShowReviewForSelfTest(program, found);
        await Idle();
        await Task.Delay(800);
        shots.Add(Save(window, dir, "dark-tr-Leftovers"));
        apps.BackToListCommand.Execute(null);
    }

    // ----------------------------------------------------------------- helpers

    private static Task Idle() =>
        Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle).Task;

    /// <summary>
    /// WPF-UI draws a symbol as a single UTF-16 char, so icons whose code point is above U+FFFF
    /// come out as a stray accent ("˘"). Reports every such icon on screen.
    /// </summary>
    private static void CheckSymbols(DependencyObject root, string page, List<string> errors)
    {
        var stack = new Stack<DependencyObject>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node is Wpf.Ui.Controls.SymbolIcon icon && (int)icon.Symbol > 0xFFFF)
            {
                var message = $"Icon {icon.Symbol} can't be drawn (code point above U+FFFF) on {page}";
                if (!errors.Contains(message)) errors.Add(message);
            }
            int count = VisualTreeHelper.GetChildrenCount(node);
            for (int i = 0; i < count; i++) stack.Push(VisualTreeHelper.GetChild(node, i));
        }
    }

    private static string Save(Window window, string dir, string name)
    {
        var root = (FrameworkElement)window.Content;
        var dpi = VisualTreeHelper.GetDpi(root);
        int w = (int)Math.Ceiling(root.ActualWidth * dpi.DpiScaleX), h = (int)Math.Ceiling(root.ActualHeight * dpi.DpiScaleY);
        var rtb = new RenderTargetBitmap(w, h, dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        rtb.Render(root);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));
        var file = Path.Combine(dir, name + ".png");
        using var fs = File.Create(file);
        encoder.Save(fs);
        return Path.GetFileName(file);
    }
}
