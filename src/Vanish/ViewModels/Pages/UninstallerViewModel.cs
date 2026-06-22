using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Vanish.Helpers;
using Vanish.Models;
using Vanish.Services;

namespace Vanish.ViewModels.Pages;

/// <summary>Drives the main uninstall + leftover-cleanup workflow and item commands.</summary>
public sealed partial class UninstallerViewModel : ObservableObject
{
    private readonly IInstalledProgramsService _programs;
    private readonly IUninstallService _uninstall;
    private readonly ILeftoverScanService _scan;
    private readonly ISystemRestoreService _restore;
    private readonly OperationLogService _log;

    private List<InstalledProgram> _allPrograms = new();
    private CancellationTokenSource? _cts;

    public UninstallerViewModel(
        IInstalledProgramsService programs,
        IUninstallService uninstall,
        ILeftoverScanService scan,
        ISystemRestoreService restore,
        OperationLogService log)
    {
        _programs = programs;
        _uninstall = uninstall;
        _scan = scan;
        _restore = restore;
        _log = log;

        Programs = CollectionViewSource.GetDefaultView(_view);
        Programs.Filter = FilterProgram;
    }

    public Loc Loc => Loc.I;

    // ---- collections --------------------------------------------------------

    private readonly ObservableCollectionEx<InstalledProgram> _view = new();
    public ICollectionView Programs { get; }

    public ObservableCollectionEx<LeftoverItem> Leftovers { get; } = new();

    // ---- state --------------------------------------------------------------

    public enum WorkflowStage { Browsing, Working, ReviewLeftovers }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBrowsing))]
    [NotifyPropertyChangedFor(nameof(IsWorking))]
    [NotifyPropertyChangedFor(nameof(IsReviewing))]
    private WorkflowStage _stage = WorkflowStage.Browsing;

    public bool IsBrowsing => Stage == WorkflowStage.Browsing;
    public bool IsWorking => Stage == WorkflowStage.Working;
    public bool IsReviewing => Stage == WorkflowStage.ReviewLeftovers;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusMessage = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private InstalledProgram? _selectedProgram;

    public bool HasSelection => SelectedProgram is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResultCountText))]
    private string _searchText = "";

    // ---- options ------------------------------------------------------------

    [ObservableProperty] private bool _createRestorePoint = true;
    [ObservableProperty] private bool _silentUninstall;
    [ObservableProperty] private bool _useRecycleBin = true;

    public string ResultCountText => $"{_view.Count}";

    partial void OnSearchTextChanged(string value) => Programs.Refresh();

    private bool FilterProgram(object obj)
    {
        if (obj is not InstalledProgram p) return false;
        if (string.IsNullOrWhiteSpace(SearchText)) return true;
        return p.DisplayName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
               (p.Publisher?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    // ---- load ---------------------------------------------------------------

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        StatusMessage = Loc.I.T("Status_Reading");
        try
        {
            _allPrograms = (await _programs.GetInstalledProgramsAsync()).ToList();
            _view.Reset(_allPrograms);
            Programs.Refresh();
            OnPropertyChanged(nameof(ResultCountText));
            StatusMessage = $"{_allPrograms.Count} {Loc.I.T("Status_Apps")}";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ---- uninstall ----------------------------------------------------------

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private Task UninstallAsync() => RunUninstallAsync(force: false);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private Task ForceUninstallAsync() => RunUninstallAsync(force: true);

    private async Task RunUninstallAsync(bool force)
    {
        var program = SelectedProgram;
        if (program is null) return;

        var title = force ? Loc.I.T("Act_ForceUninstall") : Loc.I.T("Act_Uninstall");
        var confirm = MessageBox.Show(
            $"{title}: \"{program.DisplayName}\"?",
            Loc.I.T("Confirm"), MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.OK) return;

        _cts = new CancellationTokenSource();
        Stage = WorkflowStage.Working;
        var progress = new Progress<string>(m => StatusMessage = m);

        try
        {
            if (CreateRestorePoint)
            {
                StatusMessage = "Creating a System Restore point…";
                await _restore.CreateRestorePointAsync($"Before uninstalling {program.DisplayName}", _cts.Token);
            }

            // Force uninstall: if the entry is broken we may skip running the native
            // uninstaller, but normally we still run it first.
            if (!force || !string.IsNullOrWhiteSpace(program.UninstallString))
            {
                StatusMessage = "Launching the uninstaller…";
                var result = await _uninstall.RunUninstallerAsync(program, SilentUninstall, progress, _cts.Token);
                if (!result.Succeeded)
                    StatusMessage = result.Message ?? "The uninstaller reported a problem.";
            }

            _log.Append(force ? "Force uninstall" : "Uninstall", program.DisplayName);

            StatusMessage = "Scanning for leftovers…";
            var leftovers = await _scan.ScanAsync(program, progress, _cts.Token);

            // Force mode also pre-selects medium-confidence remnants for a deeper clean.
            if (force)
                foreach (var l in leftovers.Where(l => l.Confidence == MatchConfidence.Medium))
                    l.IsSelected = true;

            Leftovers.Reset(leftovers);

            if (Leftovers.Count == 0)
            {
                StatusMessage = Loc.I.T("Leftovers_None");
                await BackToListAsync();
            }
            else
            {
                Stage = WorkflowStage.ReviewLeftovers;
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = Loc.I.T("Act_Cancel");
            Stage = WorkflowStage.Browsing;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            Stage = WorkflowStage.Browsing;
        }
    }

    partial void OnSelectedProgramChanged(InstalledProgram? value)
    {
        UninstallCommand.NotifyCanExecuteChanged();
        ForceUninstallCommand.NotifyCanExecuteChanged();
        OpenInstallFolderCommand.NotifyCanExecuteChanged();
        OpenRegistryCommand.NotifyCanExecuteChanged();
        SearchWebCommand.NotifyCanExecuteChanged();
        OpenWebsiteCommand.NotifyCanExecuteChanged();
        CopyDetailsCommand.NotifyCanExecuteChanged();
        RemoveEntryCommand.NotifyCanExecuteChanged();
    }

    // ---- "other commands" ---------------------------------------------------

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void OpenInstallFolder()
    {
        var path = SelectedProgram?.InstallLocation;
        if (string.IsNullOrWhiteSpace(path) || !System.IO.Directory.Exists(path))
        {
            StatusMessage = "Install folder is not available.";
            return;
        }
        TryStart("explorer.exe", $"\"{path}\"");
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void OpenRegistry()
    {
        var program = SelectedProgram;
        if (program is null) return;

        // regedit reopens at the key stored in its LastKey value.
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Applets\Regedit");
            key?.SetValue("LastKey", program.UninstallRegistryPath);
        }
        catch { /* best effort */ }

        TryStart("regedit.exe", "");
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void SearchWeb()
    {
        var program = SelectedProgram;
        if (program is null) return;
        var query = Uri.EscapeDataString($"{program.DisplayName} {program.Publisher}");
        TryStart($"https://www.bing.com/search?q={query}", "");
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void OpenWebsite()
    {
        var url = SelectedProgram?.UrlInfoAbout;
        if (string.IsNullOrWhiteSpace(url))
        {
            StatusMessage = "No publisher website on record.";
            return;
        }
        TryStart(url, "");
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void CopyDetails()
    {
        var p = SelectedProgram;
        if (p is null) return;
        var details =
            $"Name: {p.DisplayName}\r\n" +
            $"Publisher: {p.PublisherOrUnknown}\r\n" +
            $"Version: {p.DisplayVersion}\r\n" +
            $"Architecture: {p.Architecture}\r\n" +
            $"Size: {p.DisplaySize}\r\n" +
            $"Install date: {p.InstallDate}\r\n" +
            $"Install location: {p.InstallLocation}\r\n" +
            $"Uninstall: {p.UninstallString}\r\n" +
            $"Registry: {p.UninstallRegistryPath}";
        try { Clipboard.SetText(details); StatusMessage = "Details copied."; }
        catch { StatusMessage = "Could not copy to clipboard."; }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void RemoveEntry()
    {
        var program = SelectedProgram;
        if (program is null) return;

        var confirm = MessageBox.Show(
            $"{Loc.I.T("Cmd_RemoveEntry")}: \"{program.DisplayName}\"?",
            Loc.I.T("Confirm"), MessageBoxButton.OKCancel, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.OK) return;

        try
        {
            _programs.RemoveUninstallEntry(program);
            _view.Remove(program);
            _allPrograms.Remove(program);
            Programs.Refresh();
            OnPropertyChanged(nameof(ResultCountText));
            StatusMessage = "Entry removed from the list.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not remove entry: {ex.Message}";
        }
    }

    private void TryStart(string fileName, string args)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = args,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    // ---- leftover review ----------------------------------------------------

    [RelayCommand]
    private void SelectAllLeftovers()
    {
        foreach (var l in Leftovers) l.IsSelected = true;
    }

    [RelayCommand]
    private void SelectNoneLeftovers()
    {
        foreach (var l in Leftovers) l.IsSelected = false;
    }

    [RelayCommand]
    private async Task DeleteLeftoversAsync()
    {
        var selected = Leftovers.Where(l => l.IsSelected).ToList();
        if (selected.Count == 0)
        {
            StatusMessage = "Nothing selected.";
            return;
        }

        var confirm = MessageBox.Show(
            $"{Loc.I.T("Act_DeleteSelected")} ({selected.Count})?",
            Loc.I.T("Confirm"), MessageBoxButton.OKCancel, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.OK) return;

        _cts = new CancellationTokenSource();
        Stage = WorkflowStage.Working;
        var progress = new Progress<string>(m => StatusMessage = m);

        try
        {
            foreach (var key in selected.Where(l => l.Kind == LeftoverKind.RegistryKey))
                await _restore.BackupRegistryKeyAsync(key.Path, _cts.Token);

            int removed = await _scan.DeleteAsync(selected, UseRecycleBin, progress, _cts.Token);
            _log.Append("Leftovers cleaned", $"{removed} item(s) removed");
            StatusMessage = $"Removed {removed} leftover item(s).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Cleanup error: {ex.Message}";
        }
        finally
        {
            await BackToListAsync();
        }
    }

    [RelayCommand]
    private async Task BackToListAsync()
    {
        Leftovers.Clear();
        Stage = WorkflowStage.Browsing;
        await LoadAsync();
    }

    [RelayCommand]
    private void Cancel() => _cts?.Cancel();
}
