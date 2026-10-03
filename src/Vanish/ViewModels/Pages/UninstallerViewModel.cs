using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vanish.Helpers;
using Vanish.Models;
using Vanish.Services;

namespace Vanish.ViewModels.Pages;

public enum UninstallStage { Browsing, Working, Review, Done }

/// <summary>
/// All applications: the program list and the uninstall workflow
/// (restore point → the app's own uninstaller → leftover scan → review → delete).
/// Leftovers are never pre-selected; the user ticks what goes.
/// </summary>
public sealed partial class UninstallerViewModel : PageViewModel
{
    private readonly IInstalledProgramsService _programs;
    private readonly IUninstallService _uninstall;
    private readonly ILeftoverScanService _scan;
    private readonly ISystemRestoreService _restore;

    private readonly ObservableCollectionEx<InstalledProgram> _items = new();
    private readonly ListCollectionView _view;
    private List<InstalledProgram> _all = new();
    private Task? _loadTask;
    private CancellationTokenSource? _cts;

    public UninstallerViewModel(IInstalledProgramsService programs, IUninstallService uninstall,
        ILeftoverScanService scan, ISystemRestoreService restore)
    {
        _programs = programs;
        _uninstall = uninstall;
        _scan = scan;
        _restore = restore;

        _view = (ListCollectionView)CollectionViewSource.GetDefaultView(_items);
        _view.Filter = FilterProgram;
        _view.CustomSort = new ProgramComparer(this);

        LeftoverView = new ListCollectionView(Leftovers);
        LeftoverView.Filter = o => o is LeftoverItem l && LeftoverFilter switch
        {
            "files" => !l.IsRegistry,
            "registry" => l.IsRegistry,
            _ => true
        };
    }

    // ===================================================================== list

    public ICollectionView Programs => _view;

    public bool IsLoaded { get; private set; }

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private string _totalSizeText = "—";
    [ObservableProperty] private int _visibleCount;

    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _filter = "all";   // all | recent | large
    [ObservableProperty] private string _sort = "name";    // name | size | date | publisher

    partial void OnSearchTextChanged(string value) => RefreshView();
    partial void OnFilterChanged(string value) => RefreshView();
    partial void OnSortChanged(string value) => RefreshView();

    /// <summary>"No match" placeholder: only once the list has loaded.</summary>
    public bool ShowEmpty => IsLoaded && !IsLoading && VisibleCount == 0;

    partial void OnVisibleCountChanged(int value) => OnPropertyChanged(nameof(ShowEmpty));
    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(ShowEmpty));

    private void RefreshView()
    {
        _view.Refresh();
        VisibleCount = _view.Count;
    }

    private bool FilterProgram(object obj)
    {
        if (obj is not InstalledProgram p) return false;
        switch (Filter)
        {
            case "recent":
                if (p.InstallDate is not { } d || d.ToDateTime(TimeOnly.MinValue) < DateTime.Today.AddDays(-30)) return false;
                break;
            case "large":
                if (p.EstimatedSizeBytes < 500L * 1024 * 1024) return false;
                break;
        }
        var q = SearchText?.Trim();
        if (string.IsNullOrEmpty(q)) return true;
        return p.DisplayName.Contains(q, StringComparison.CurrentCultureIgnoreCase) ||
               (p.Publisher?.Contains(q, StringComparison.CurrentCultureIgnoreCase) ?? false);
    }

    private sealed class ProgramComparer(UninstallerViewModel owner) : System.Collections.IComparer
    {
        public int Compare(object? x, object? y)
        {
            if (x is not InstalledProgram a || y is not InstalledProgram b) return 0;
            int r = owner.Sort switch
            {
                "size" => b.EstimatedSizeBytes.CompareTo(a.EstimatedSizeBytes),
                "date" => Nullable.Compare(b.InstallDate, a.InstallDate),
                "publisher" => string.Compare(a.PublisherOrUnknown, b.PublisherOrUnknown, StringComparison.CurrentCultureIgnoreCase),
                _ => 0
            };
            return r != 0 ? r : string.Compare(a.DisplayName, b.DisplayName, StringComparison.CurrentCultureIgnoreCase);
        }
    }

    public override void OnShown()
    {
        base.OnShown();
        _ = EnsureLoadedAsync(false);
    }

    public override void RefreshTexts()
    {
        OnPropertyChanged(nameof(SelectionHint));
        _items.Reset(_all);
        RefreshView();
        var keep = Leftovers.ToList();
        Leftovers.Reset(keep);
        UpdateLeftoverSelection();
        if (Stage == UninstallStage.Done && Target is not null) OnPropertyChanged(nameof(DoneTitle));
    }

    /// <summary>Loads the list once (or again when <paramref name="force"/>); shared with the dashboard.</summary>
    public Task EnsureLoadedAsync(bool force)
    {
        if (_loadTask is null || (force && _loadTask.IsCompleted)) _loadTask = LoadCoreAsync();
        return _loadTask;
    }

    [RelayCommand]
    private Task RefreshAsync() => EnsureLoadedAsync(true);

    private async Task LoadCoreAsync()
    {
        IsLoading = true;
        try
        {
            var selectedKey = SelectedProgram?.RegistryKeyName;
            _all = (await _programs.GetInstalledProgramsAsync()).ToList();
            _items.Reset(_all);
            TotalCount = _all.Count;
            TotalSizeText = ByteSize.Humanize(_all.Sum(p => p.EstimatedSizeBytes));
            RefreshView();
            IsLoaded = true;
            SelectedProgram = selectedKey is null ? null : _all.FirstOrDefault(p => p.RegistryKeyName == selectedKey);
        }
        catch (Exception ex)
        {
            Toast.Show(ex.Message, ToastKind.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Finds the installed program an executable belongs to (Hunter mode).</summary>
    public InstalledProgram? FindByExecutable(string? exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath)) return null;
        InstalledProgram? best = null;
        int bestLen = 0;
        foreach (var p in _all)
        {
            var folder = p.InstallLocation ?? LeftoverScanService.GuessInstallFolder(p);
            if (string.IsNullOrWhiteSpace(folder)) continue;
            folder = folder.TrimEnd('\\') + "\\";
            if (folder.Length > bestLen && exePath.StartsWith(folder, StringComparison.OrdinalIgnoreCase))
            {
                best = p;
                bestLen = folder.Length;
            }
        }
        return best;
    }

    /// <summary>Selects a program by name (used by other pages) and opens this page.</summary>
    public void Reveal(InstalledProgram program)
    {
        SearchText = "";
        Filter = "all";
        SelectedProgram = program;
        Navigation.Navigate("Uninstaller");
    }

    public InstalledProgram? FindByName(string displayName) =>
        _all.FirstOrDefault(p => string.Equals(p.DisplayName, displayName, StringComparison.OrdinalIgnoreCase));

    // ================================================================ selection

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyPropertyChangedFor(nameof(SelectionHint))]
    [NotifyCanExecuteChangedFor(nameof(UninstallCommand))]
    [NotifyCanExecuteChangedFor(nameof(ForceUninstallCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenInstallFolderCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenRegistryCommand))]
    [NotifyCanExecuteChangedFor(nameof(SearchWebCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenWebsiteCommand))]
    [NotifyCanExecuteChangedFor(nameof(CopyDetailsCommand))]
    [NotifyCanExecuteChangedFor(nameof(RemoveEntryCommand))]
    private InstalledProgram? _selectedProgram;

    public bool HasSelection => SelectedProgram is not null && Stage == UninstallStage.Browsing;

    public string SelectionHint => SelectedProgram is { } p ? p.DisplayName : T("Apps_SelectHint");

    // ================================================================= workflow

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBrowsing))]
    [NotifyPropertyChangedFor(nameof(IsWorking))]
    [NotifyPropertyChangedFor(nameof(IsReviewing))]
    [NotifyPropertyChangedFor(nameof(IsDone))]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyCanExecuteChangedFor(nameof(UninstallCommand))]
    [NotifyCanExecuteChangedFor(nameof(ForceUninstallCommand))]
    private UninstallStage _stage = UninstallStage.Browsing;

    public bool IsBrowsing => Stage == UninstallStage.Browsing;
    public bool IsWorking => Stage == UninstallStage.Working;
    public bool IsReviewing => Stage == UninstallStage.Review;
    public bool IsDone => Stage == UninstallStage.Done;

    [ObservableProperty] private InstalledProgram? _target;
    [ObservableProperty] private string _workTitle = "";
    [ObservableProperty] private string _workStatus = "";
    [ObservableProperty] private int _workStep;
    [ObservableProperty] private bool _restoreStepEnabled = true;
    [ObservableProperty] private bool _canCancel;

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private Task UninstallAsync() => RunAsync(force: false);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private Task ForceUninstallAsync() => RunAsync(force: true);

    private async Task RunAsync(bool force)
    {
        var program = SelectedProgram;
        if (program is null || Stage != UninstallStage.Browsing) return;

        if (Settings.ConfirmBeforeUninstall || force)
        {
            var details = new List<string> { $"{T("Apps_Publisher")}: {program.PublisherOrUnknown}" };
            if (!string.IsNullOrWhiteSpace(program.DisplayVersion)) details.Add($"{T("Apps_Version")}: {program.DisplayVersion}");
            if (!string.IsNullOrWhiteSpace(program.InstallLocation)) details.Add($"{T("Apps_Location")}: {program.InstallLocation}");
            details.Add(Settings.CreateRestorePoint ? T("Uninst_WillCreateRestorePoint") : T("Uninst_NoRestorePoint"));
            bool ok = await Dialogs.ConfirmAsync(
                F(force ? "Uninst_ForceConfirmTitleFmt" : "Uninst_ConfirmTitleFmt", program.DisplayName),
                T(force ? "Uninst_ForceConfirmText" : "Uninst_ConfirmText"),
                T(force ? "Act_ForceUninstall" : "Act_Uninstall"),
                force ? DialogTone.Danger : DialogTone.Warning,
                details);
            if (!ok) return;
        }

        Navigation.Navigate("Uninstaller");
        Target = program;
        WorkTitle = F("Work_TitleFmt", program.DisplayName);
        RestoreStepEnabled = Settings.CreateRestorePoint;
        WorkStep = 0;
        WorkStatus = T("Work_Preparing");
        CanCancel = false;
        Stage = UninstallStage.Working;

        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        try
        {
            // What the app looks like *before* its uninstaller runs (the folder may vanish).
            var fingerprint = await Task.Run(() => _scan.CaptureFingerprint(program), ct);

            if (Settings.CreateRestorePoint)
            {
                WorkStatus = T("Work_RestorePoint");
                bool created = await _restore.CreateRestorePointAsync(F("Work_RestorePointDescFmt", program.DisplayName), ct);
                if (!created) Toast.Show(T("Work_RestorePointFailed"), ToastKind.Warning);
            }

            WorkStep = 1;
            UninstallResult? result = null;
            bool hasUninstaller = !string.IsNullOrWhiteSpace(program.UninstallString) || !string.IsNullOrWhiteSpace(program.QuietUninstallString);
            if (hasUninstaller)
            {
                WorkStatus = T("Work_RunUninstaller");
                var progress = new Progress<string>(k => WorkStatus = T(k));
                result = await _uninstall.RunUninstallerAsync(program, Settings.SilentUninstall, progress, ct);
            }

            bool stillInstalled = _programs.StillInstalled(program) && FolderHasFiles(fingerprint.InstallLocation);
            if (stillInstalled && !force)
            {
                // The uninstaller was cancelled or failed: scanning now could offer the app's own files.
                bool scanAnyway = await Dialogs.ConfirmAsync(
                    T("Uninst_NotFinishedTitle"),
                    (result?.Message is { Length: > 0 } m ? m + "\n\n" : "") + T("Uninst_NotFinishedText"),
                    T("Uninst_ScanAnyway"),
                    DialogTone.Warning);
                if (!scanAnyway)
                {
                    Stage = UninstallStage.Browsing;
                    Toast.Show(F("Uninst_StoppedFmt", program.DisplayName));
                    return;
                }
            }
            else if (!stillInstalled)
            {
                Log.Append(force ? "Log_Forced" : "Log_Uninstalled", program.DisplayName);
            }

            WorkStep = 2;
            CanCancel = true;
            WorkStatus = T("Work_Folders");
            var others = _all.Where(p => !ReferenceEquals(p, program)).Select(p => p.InstallLocation ?? "").ToList();
            var scanProgress = new Progress<string>(k => WorkStatus = T(k));
            var found = await _scan.ScanAsync(fingerprint, others, scanProgress, ct);
            CanCancel = false;

            StillInstalledWarning = stillInstalled;
            var ordered = found
                .OrderBy(l => l.IsRegistry)
                .ThenBy(l => l.Confidence)
                .ThenBy(l => l.Path, StringComparer.OrdinalIgnoreCase)
                .ToList();
            SetLeftovers(ordered);

            if (ordered.Count == 0)
            {
                ShowDone(0, 0, 0, nothingFound: true);
                _ = EnsureLoadedAsync(true);
            }
            else
            {
                LeftoverFilter = "all";
                Stage = UninstallStage.Review;
            }
        }
        catch (OperationCanceledException)
        {
            Stage = UninstallStage.Browsing;
            Toast.Show(T("Work_Cancelled"));
            _ = EnsureLoadedAsync(true);
        }
        catch (Exception ex)
        {
            Stage = UninstallStage.Browsing;
            Toast.Show(ex.Message, ToastKind.Error);
        }
        finally
        {
            CanCancel = false;
        }
    }

    private static bool FolderHasFiles(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder)) return true; // unknown: assume installed
        try
        {
            return Directory.Exists(folder) && Directory.EnumerateFiles(folder, "*.exe", SearchOption.TopDirectoryOnly).Any();
        }
        catch
        {
            return true;
        }
    }

    [RelayCommand]
    private void Cancel() => _cts?.Cancel();

    // =================================================================== review

    public ObservableCollectionEx<LeftoverItem> Leftovers { get; } = new();
    public ListCollectionView LeftoverView { get; }

    [ObservableProperty] private string _leftoverFilter = "all";   // all | files | registry
    [ObservableProperty] private int _selectedLeftoverCount;
    [ObservableProperty] private string _selectedLeftoverText = "";
    [ObservableProperty] private int _fileLeftoverCount;
    [ObservableProperty] private int _registryLeftoverCount;
    [ObservableProperty] private int _highLeftoverCount;
    [ObservableProperty] private string _leftoverSummary = "";
    [ObservableProperty] private bool _stillInstalledWarning;

    partial void OnLeftoverFilterChanged(string value) => LeftoverView.Refresh();

    private void SetLeftovers(IReadOnlyList<LeftoverItem> items)
    {
        foreach (var old in Leftovers) old.PropertyChanged -= OnLeftoverChanged;
        Leftovers.Reset(items);
        foreach (var l in items) l.PropertyChanged += OnLeftoverChanged;
        FileLeftoverCount = items.Count(l => !l.IsRegistry);
        RegistryLeftoverCount = items.Count(l => l.IsRegistry);
        HighLeftoverCount = items.Count(l => l.Confidence == MatchConfidence.High);
        LeftoverSummary = F("Left_SummaryFmt", items.Count, ByteSize.Humanize(items.Where(l => !l.IsRegistry).Sum(l => l.SizeBytes)));
        UpdateLeftoverSelection();
    }

    private void OnLeftoverChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LeftoverItem.IsSelected)) UpdateLeftoverSelection();
    }

    private void UpdateLeftoverSelection()
    {
        var selected = Leftovers.Where(l => l.IsSelected).ToList();
        SelectedLeftoverCount = selected.Count;
        SelectedLeftoverText = selected.Count == 0
            ? T("Left_NoneSelected")
            : F("Left_SelectedFmt", selected.Count, ByteSize.Humanize(selected.Sum(l => l.SizeBytes)));
        DeleteLeftoversCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void SelectSafeLeftovers()
    {
        foreach (var l in Leftovers) l.IsSelected = l.Confidence == MatchConfidence.High;
    }

    [RelayCommand]
    private void SelectAllLeftovers()
    {
        foreach (var l in LeftoverView.Cast<LeftoverItem>()) l.IsSelected = true;
    }

    [RelayCommand]
    private void SelectNoneLeftovers()
    {
        foreach (var l in Leftovers) l.IsSelected = false;
    }

    [RelayCommand]
    private void RevealLeftover(LeftoverItem? item)
    {
        if (item is null) return;
        if (item.IsRegistry) Shell.OpenRegistry(item.Path);
        else Shell.Reveal(item.Path);
    }

    private bool CanDeleteLeftovers() => SelectedLeftoverCount > 0;

    [RelayCommand(CanExecute = nameof(CanDeleteLeftovers))]
    private async Task DeleteLeftoversAsync()
    {
        var selected = Leftovers.Where(l => l.IsSelected).ToList();
        if (selected.Count == 0) return;

        var lowCount = selected.Count(l => l.Confidence == MatchConfidence.Low);
        var message = T(Settings.UseRecycleBin ? "Left_DeleteTextRecycle" : "Left_DeleteTextPermanent");
        if (lowCount > 0) message += "\n\n" + F("Left_LowWarningFmt", lowCount);
        bool ok = await Dialogs.ConfirmAsync(
            F("Left_DeleteTitleFmt", selected.Count),
            message,
            T("Act_DeleteSelected"),
            DialogTone.Danger,
            selected.Select(s => s.IsRegistry && s.ValueName is not null ? $"{s.Path}  →  {s.ValueName}" : s.Path));
        if (!ok) return;

        WorkStep = 3;
        WorkStatus = T("Work_Deleting");
        Stage = UninstallStage.Working;
        try
        {
            var progress = new Progress<string>(k => WorkStatus = T(k));
            var (removed, freed, failed) = await _scan.DeleteAsync(selected, Settings.UseRecycleBin, progress);
            Log.Append("Log_Leftovers", F("Log_LeftoversDetailFmt", Target?.DisplayName ?? "", removed, ByteSize.Humanize(freed)));
            ShowDone(removed, freed, failed, nothingFound: false);
        }
        catch (Exception ex)
        {
            Toast.Show(ex.Message, ToastKind.Error);
            Stage = UninstallStage.Review;
        }
        _ = EnsureLoadedAsync(true);
    }

    /// <summary>Self-test only: shows the review screen for scan results without uninstalling anything.</summary>
    internal void ShowReviewForSelfTest(InstalledProgram program, IReadOnlyList<LeftoverItem> items)
    {
        Target = program;
        StillInstalledWarning = true;
        SetLeftovers(items.OrderBy(l => l.IsRegistry).ThenBy(l => l.Confidence).ThenBy(l => l.Path, StringComparer.OrdinalIgnoreCase).ToList());
        LeftoverFilter = "all";
        Stage = UninstallStage.Review;
    }

    [RelayCommand]
    private void SkipLeftovers()
    {
        ShowDone(0, 0, 0, nothingFound: false, skipped: true);
        _ = EnsureLoadedAsync(true);
    }

    // ===================================================================== done

    [ObservableProperty] private string _doneText = "";
    [ObservableProperty] private bool _doneHasFailures;
    [ObservableProperty] private string _doneFailText = "";
    [ObservableProperty] private double _doneFreed = double.NaN;
    [ObservableProperty] private double _doneRemoved = double.NaN;

    public string DoneTitle => Target is null ? "" : F("Done_TitleFmt", Target.DisplayName);

    private void ShowDone(int removed, long freed, int failed, bool nothingFound, bool skipped = false)
    {
        OnPropertyChanged(nameof(DoneTitle));
        DoneText = nothingFound ? T("Done_NoLeftovers") : skipped ? T("Done_Skipped") : T("Done_Cleaned");
        DoneRemoved = removed;
        DoneFreed = freed;
        DoneHasFailures = failed > 0;
        DoneFailText = F("Done_FailedFmt", failed);
        Stage = UninstallStage.Done;
        if (!skipped) Toast.Show(nothingFound ? T("Done_NoLeftovers") : F("Done_ToastFmt", removed, ByteSize.Humanize(freed)), ToastKind.Success);
    }

    [RelayCommand]
    private void BackToList()
    {
        SetLeftovers(Array.Empty<LeftoverItem>());
        SelectedProgram = null;
        Stage = UninstallStage.Browsing;
    }

    // ============================================================ other commands

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void OpenInstallFolder()
    {
        var p = SelectedProgram;
        if (p is null) return;
        var folder = p.InstallLocation ?? LeftoverScanService.GuessInstallFolder(p);
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            Toast.Show(T("Cmd_NoInstallFolder"), ToastKind.Warning);
            return;
        }
        Shell.OpenFolder(folder);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void OpenRegistry()
    {
        if (SelectedProgram is { } p) Shell.OpenRegistry(p.UninstallRegistryPath);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void SearchWeb()
    {
        if (SelectedProgram is not { } p) return;
        Shell.OpenUrl("https://www.google.com/search?q=" + Uri.EscapeDataString($"{p.DisplayName} {p.Publisher}".Trim()));
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void OpenWebsite()
    {
        var url = SelectedProgram?.UrlInfoAbout;
        if (string.IsNullOrWhiteSpace(url))
        {
            Toast.Show(T("Cmd_NoWebsite"), ToastKind.Warning);
            return;
        }
        Shell.OpenUrl(url);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void CopyDetails()
    {
        var p = SelectedProgram;
        if (p is null) return;
        var text =
            $"{T("Apps_Name")}: {p.DisplayName}\r\n" +
            $"{T("Apps_Publisher")}: {p.PublisherOrUnknown}\r\n" +
            $"{T("Apps_Version")}: {p.VersionText}\r\n" +
            $"{T("Apps_Architecture")}: {p.Architecture}\r\n" +
            $"{T("Apps_Size")}: {p.DisplaySize}\r\n" +
            $"{T("Apps_InstallDate")}: {p.InstallDateText}\r\n" +
            $"{T("Apps_Location")}: {p.InstallLocation}\r\n" +
            $"{T("Apps_UninstallCommand")}: {p.UninstallString}\r\n" +
            $"{T("Apps_RegistryKey")}: {p.UninstallRegistryPath}";
        try
        {
            Clipboard.SetText(text);
            Toast.Show(T("Cmd_Copied"), ToastKind.Success);
        }
        catch
        {
            Toast.Show(T("Cmd_CopyFailed"), ToastKind.Error);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task RemoveEntryAsync()
    {
        var p = SelectedProgram;
        if (p is null) return;
        bool ok = await Dialogs.ConfirmAsync(F("Cmd_RemoveEntryTitleFmt", p.DisplayName), T("Cmd_RemoveEntryText"),
            T("Cmd_RemoveEntry"), DialogTone.Danger, new[] { p.UninstallRegistryPath });
        if (!ok) return;
        try
        {
            await _restore.BackupRegistryKeyAsync(p.UninstallRegistryPath);
            _programs.RemoveUninstallEntry(p);
            Log.Append("Log_EntryRemoved", p.DisplayName);
            Toast.Show(T("Cmd_EntryRemoved"), ToastKind.Success);
            SelectedProgram = null;
            await EnsureLoadedAsync(true);
        }
        catch (Exception ex)
        {
            Toast.Show(ex.Message, ToastKind.Error);
        }
    }
}
