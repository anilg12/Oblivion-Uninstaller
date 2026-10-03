using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Vanish.Helpers;
using Vanish.Services;
using Wpf.Ui.Controls;

namespace Vanish.ViewModels.Pages;

/// <summary>A file or folder queued for shredding.</summary>
public sealed class ShredItem
{
    public required string Path { get; init; }
    public bool IsFolder { get; init; }
    public long SizeBytes { get; init; }
    public string Name => System.IO.Path.GetFileName(Path.TrimEnd('\\')) is { Length: > 0 } n ? n : Path;
    public string SizeText => ByteSize.Humanize(SizeBytes);
    public SymbolRegular Symbol => IsFolder ? SymbolRegular.Folder24 : SymbolRegular.Document24;
}

/// <summary>Unrecoverable delete: overwrite, rename, delete. Protected system locations are refused.</summary>
public sealed partial class ShredderViewModel : PageViewModel
{
    private readonly ShredderService _service;
    private CancellationTokenSource? _cts;

    public ShredderViewModel(ShredderService service) => _service = service;

    public ObservableCollectionEx<ShredItem> Items { get; } = new();

    [ObservableProperty] private string _passes = "1";   // 1 | 3
    [ObservableProperty] private bool _isShredding;
    [ObservableProperty] private double _progress;
    [ObservableProperty] private string _totalText = "";

    [RelayCommand]
    private void AddFiles()
    {
        var dialog = new OpenFileDialog { Multiselect = true, Title = T("Shred_AddFiles") };
        if (dialog.ShowDialog() == true) Add(dialog.FileNames);
    }

    [RelayCommand]
    private void AddFolder()
    {
        var dialog = new OpenFolderDialog { Title = T("Shred_AddFolder") };
        if (dialog.ShowDialog() == true) Add(new[] { dialog.FolderName });
    }

    /// <summary>Also used for drag &amp; drop onto the page.</summary>
    public void Add(IEnumerable<string> paths)
    {
        int refused = 0;
        foreach (var raw in paths)
        {
            var path = raw.TrimEnd('\\');
            if (Items.Any(i => i.Path.Equals(path, StringComparison.OrdinalIgnoreCase))) continue;
            if (LeftoverScanService.IsProtectedPath(path) || path.Length <= 3)
            {
                refused++;
                continue;
            }
            if (File.Exists(path))
                Items.Add(new ShredItem { Path = path, SizeBytes = SafeLength(path) });
            else if (Directory.Exists(path))
                Items.Add(new ShredItem { Path = path, IsFolder = true, SizeBytes = FolderSize(path) });
        }
        if (refused > 0) Toast.Show(F("Shred_RefusedFmt", refused), ToastKind.Warning);
        UpdateTotal();
    }

    private static long SafeLength(string f)
    {
        try { return new FileInfo(f).Length; } catch { return 0; }
    }

    private static long FolderSize(string dir)
    {
        try
        {
            return Directory.EnumerateFiles(dir, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true })
                .Take(200_000).Sum(SafeLength);
        }
        catch
        {
            return 0;
        }
    }

    private void UpdateTotal()
    {
        TotalText = Items.Count == 0 ? "" : F("Shred_TotalFmt", Items.Count, ByteSize.Humanize(Items.Sum(i => i.SizeBytes)));
        ShredCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void RemoveItem(ShredItem? item)
    {
        if (item is null) return;
        Items.Remove(item);
        UpdateTotal();
    }

    [RelayCommand]
    private void Clear()
    {
        Items.Clear();
        UpdateTotal();
    }

    private bool CanShred() => Items.Count > 0 && !IsShredding;

    [RelayCommand(CanExecute = nameof(CanShred))]
    private async Task ShredAsync()
    {
        var list = Items.ToList();
        bool ok = await Dialogs.ConfirmAsync(F("Shred_ConfirmTitleFmt", list.Count), T("Shred_ConfirmText"), T("Act_Shred"),
            DialogTone.Danger, list.Select(i => i.Path));
        if (!ok) return;

        IsShredding = true;
        Progress = 0;
        _cts = new CancellationTokenSource();
        try
        {
            var progress = new Progress<double>(p => Progress = p);
            var (shredded, failed) = await _service.ShredAsync(list.Select(i => i.Path).ToList(), int.Parse(Passes), progress, _cts.Token);
            Log.Append("Log_Shred", F("Log_ShredDetailFmt", shredded));
            Items.Reset(list.Where(i => File.Exists(i.Path) || Directory.Exists(i.Path)));
            Toast.Show(failed == 0 ? F("Shred_DoneFmt", shredded) : F("Shred_DonePartialFmt", shredded, failed),
                failed == 0 ? ToastKind.Success : ToastKind.Warning);
        }
        catch (OperationCanceledException)
        {
            Toast.Show(T("Work_Cancelled"));
        }
        finally
        {
            IsShredding = false;
            UpdateTotal();
        }
    }

    [RelayCommand]
    private void Stop() => _cts?.Cancel();

    partial void OnIsShreddingChanged(bool value) => ShredCommand.NotifyCanExecuteChanged();
}
