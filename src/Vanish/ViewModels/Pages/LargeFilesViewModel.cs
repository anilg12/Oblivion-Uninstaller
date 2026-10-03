using System.ComponentModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Vanish.Helpers;
using Vanish.Services;

namespace Vanish.ViewModels.Pages;

/// <summary>A place to search for large files (user folder, a drive or a custom folder).</summary>
public sealed record ScanRoot(string Path, string Title, string Detail);

/// <summary>Finds the biggest files in the user's own folders. Nothing is selected; removal goes to the Recycle Bin.</summary>
public sealed partial class LargeFilesViewModel : PageViewModel
{
    private readonly LargeFilesService _service;
    private CancellationTokenSource? _cts;

    public LargeFilesViewModel(LargeFilesService service) => _service = service;

    public ObservableCollectionEx<ScanRoot> Roots { get; } = new();
    public ObservableCollectionEx<LargeFile> Files { get; } = new();

    [ObservableProperty] private ScanRoot? _selectedRoot;
    [ObservableProperty] private string _minSize = "500";   // MB: 100 | 500 | 1024
    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private bool _hasScanned;
    [ObservableProperty] private string _progressText = "";
    [ObservableProperty] private int _selectedCount;
    [ObservableProperty] private string _selectedText = "";
    [ObservableProperty] private string _resultText = "";

    public override void OnShown()
    {
        base.OnShown();
        if (Roots.Count == 0) BuildRoots();
    }

    public override void RefreshTexts()
    {
        var keep = SelectedRoot?.Path;
        BuildRoots();
        SelectedRoot = Roots.FirstOrDefault(r => r.Path == keep) ?? Roots.FirstOrDefault();
        UpdateSelection();
    }

    private void BuildRoots()
    {
        var list = new List<ScanRoot>
        {
            new(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), T("Large_UserFolder"),
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))
        };
        foreach (var d in DriveInfo.GetDrives())
        {
            try
            {
                if (d.DriveType != DriveType.Fixed || !d.IsReady) continue;
                var label = string.IsNullOrWhiteSpace(d.VolumeLabel) ? T("Large_Drive") : d.VolumeLabel;
                list.Add(new ScanRoot(d.Name, $"{label} ({d.Name.TrimEnd('\\')})",
                    F("Live_DiskFreeFmt", ByteSize.Humanize(d.AvailableFreeSpace), ByteSize.Humanize(d.TotalSize))));
            }
            catch { /* not ready */ }
        }
        Roots.Reset(list);
        SelectedRoot ??= Roots.FirstOrDefault();
    }

    [RelayCommand]
    private void PickFolder()
    {
        var dialog = new OpenFolderDialog { Title = T("Large_PickFolder") };
        if (dialog.ShowDialog() != true) return;
        var path = dialog.FolderName;
        var root = new ScanRoot(path, Path.GetFileName(path.TrimEnd('\\')) is { Length: > 0 } n ? n : path, path);
        Roots.Add(root);
        SelectedRoot = root;
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        if (SelectedRoot is null || IsScanning) return;
        long min = long.Parse(MinSize) * 1024 * 1024;
        IsScanning = true;
        ResultText = "";
        foreach (var f in Files) f.PropertyChanged -= OnFileChanged;
        Files.Reset(Array.Empty<LargeFile>());
        UpdateSelection();
        _cts = new CancellationTokenSource();
        try
        {
            var progress = new Progress<int>(n => ProgressText = F("Large_ScannedFmt", n));
            ProgressText = T("Large_Starting");
            var found = await _service.ScanAsync(SelectedRoot.Path, min, progress, _cts.Token);
            foreach (var f in found) f.PropertyChanged += OnFileChanged;
            Files.Reset(found);
            HasScanned = true;
            ResultText = found.Count == 0
                ? F("Large_NoneFmt", ByteSize.Humanize(min))
                : F("Large_FoundFmt", found.Count, ByteSize.Humanize(found.Sum(f => f.SizeBytes)));
        }
        catch (OperationCanceledException)
        {
            ResultText = T("Work_Cancelled");
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand]
    private void Stop() => _cts?.Cancel();

    private void OnFileChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LargeFile.IsSelected)) UpdateSelection();
    }

    private void UpdateSelection()
    {
        var sel = Files.Where(f => f.IsSelected).ToList();
        SelectedCount = sel.Count;
        SelectedText = sel.Count == 0 ? T("Junk_NothingSelected") : F("Large_SelectedFmt", sel.Count, ByteSize.Humanize(sel.Sum(f => f.SizeBytes)));
        RecycleCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void SelectNone()
    {
        foreach (var f in Files) f.IsSelected = false;
    }

    [RelayCommand]
    private void Reveal(LargeFile? file)
    {
        if (file is not null) Shell.Reveal(file.Path);
    }

    private bool CanRecycle() => SelectedCount > 0;

    [RelayCommand(CanExecute = nameof(CanRecycle))]
    private async Task RecycleAsync()
    {
        var sel = Files.Where(f => f.IsSelected).ToList();
        if (sel.Count == 0) return;
        bool ok = await Dialogs.ConfirmAsync(F("Large_RecycleTitleFmt", sel.Count, ByteSize.Humanize(sel.Sum(f => f.SizeBytes))),
            T("Large_RecycleText"), T("Act_MoveToRecycle"), DialogTone.Warning, sel.Select(f => $"{f.Path}  ({f.SizeText})"));
        if (!ok) return;
        var (moved, bytes) = await _service.RecycleAsync(sel);
        foreach (var f in sel.Where(f => !File.Exists(f.Path)))
        {
            f.PropertyChanged -= OnFileChanged;
            Files.Remove(f);
        }
        UpdateSelection();
        Log.Append("Log_LargeFiles", F("Log_LargeFilesDetailFmt", moved, ByteSize.Humanize(bytes)));
        Toast.Show(F("Large_RecycledFmt", moved, ByteSize.Humanize(bytes)), moved == sel.Count ? ToastKind.Success : ToastKind.Warning);
    }
}
