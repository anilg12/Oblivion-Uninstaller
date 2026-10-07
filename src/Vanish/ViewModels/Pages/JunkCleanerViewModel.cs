using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vanish.Helpers;
using Vanish.Models;
using Vanish.Services;

namespace Vanish.ViewModels.Pages;

// junk cleaner. categories get scanned automatically (read only) but NOTHING is ticked,
// each category shows exactly what would be deleted and cleaning always asks first
public sealed partial class JunkCleanerViewModel : PageViewModel
{
    private readonly IJunkCleanerService _service;
    private bool _scannedOnce;
    private CancellationTokenSource? _cts;

    public JunkCleanerViewModel(IJunkCleanerService service)
    {
        _service = service;
        Categories = service.CreateCategories();
        foreach (var c in Categories)
            c.SelectionChanged += UpdateTotals;
    }

    public IReadOnlyList<JunkCategory> Categories { get; }

    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private bool _isCleaning;
    [ObservableProperty] private double _cleanProgress;
    [ObservableProperty] private double _foundBytes = double.NaN;
    [ObservableProperty] private double _selectedBytes;
    [ObservableProperty] private int _selectedCount;
    [ObservableProperty] private string _selectedText = "";
    [ObservableProperty] private bool _hasResult;
    [ObservableProperty] private string _resultText = "";
    [ObservableProperty] private double _resultFreed;

    public bool CanInteract => !IsScanning && !IsCleaning;

    partial void OnIsScanningChanged(bool value) => OnPropertyChanged(nameof(CanInteract));
    partial void OnIsCleaningChanged(bool value) => OnPropertyChanged(nameof(CanInteract));

    public override void OnShown()
    {
        base.OnShown();
        if (!_scannedOnce) _ = ScanAsync();
    }

    public override void RefreshTexts()
    {
        foreach (var c in Categories) c.RefreshTexts();
        UpdateTotals();
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        if (IsScanning || IsCleaning) return;
        _scannedOnce = true;
        IsScanning = true;
        HasResult = false;
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        try
        {
            await Task.WhenAll(Categories.Select(c => ScanOneAsync(c, ct)));
        }
        catch (OperationCanceledException) { /* stopped */ }
        finally
        {
            IsScanning = false;
            UpdateTotals();
        }
    }

    private async Task ScanOneAsync(JunkCategory c, CancellationToken ct)
    {
        c.IsScanning = true;
        try
        {
            var (items, bytes, count) = await _service.ScanAsync(c, ct);
            c.SetItems(items, bytes, count);
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            c.SetItems(Array.Empty<JunkEntry>(), 0, 0);
        }
        finally
        {
            c.IsScanning = false;
            UpdateTotals();
        }
    }

    [RelayCommand]
    private void StopScan() => _cts?.Cancel();

    private void UpdateTotals()
    {
        var scanned = Categories.Where(c => c.IsScanned).ToList();
        FoundBytes = scanned.Count == 0 ? double.NaN : scanned.Sum(c => c.TotalBytes);
        SelectedBytes = Categories.Sum(c => c.SelectedBytes);
        SelectedCount = Categories.Sum(c => c.SelectedCount);
        SelectedText = SelectedCount == 0
            ? T("Junk_NothingSelected")
            : F("Junk_SelectedTotalFmt", SelectedCount, ByteSize.Humanize((long)SelectedBytes));
        CleanCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void ExpandAll()
    {
        bool expand = Categories.Any(c => !c.IsExpanded && c.HasItems);
        foreach (var c in Categories) c.IsExpanded = expand && c.HasItems;
    }

    [RelayCommand]
    private void SelectNone()
    {
        foreach (var c in Categories) c.SelectionState = false;
    }

    private bool CanClean() => SelectedCount > 0 && !IsCleaning && !IsScanning;

    [RelayCommand(CanExecute = nameof(CanClean))]
    private async Task CleanAsync()
    {
        var picked = Categories
            .Select(c => (Category: c, Items: c.Items.Where(i => i.IsSelected).ToList()))
            .Where(x => x.Items.Count > 0)
            .ToList();
        if (picked.Count == 0) return;

        var details = picked.Select(x => $"{x.Category.Title} — {F("Junk_ItemsFmt", x.Items.Sum(i => i.Count))}, {ByteSize.Humanize(x.Items.Sum(i => i.SizeBytes))}").ToList();
        var notes = new List<string>();
        if (picked.Any(x => x.Category.Mode == JunkMode.Recycle)) notes.Add(T("Junk_ConfirmRecycleNote"));
        if (picked.Any(x => x.Category.Mode == JunkMode.EmptyRecycleBin)) notes.Add(T("Junk_ConfirmBinNote"));
        if (picked.Any(x => x.Category.Mode == JunkMode.Delete)) notes.Add(T("Junk_ConfirmDeleteNote"));

        bool ok = await Dialogs.ConfirmAsync(
            F("Junk_ConfirmTitleFmt", ByteSize.Humanize((long)SelectedBytes)),
            string.Join("\n", notes),
            T("Act_Clean"),
            picked.Any(x => x.Category.Mode == JunkMode.EmptyRecycleBin) ? DialogTone.Danger : DialogTone.Warning,
            details);
        if (!ok) return;

        IsCleaning = true;
        CleanProgress = 0;
        HasResult = false;
        try
        {
            var entries = picked.SelectMany(x => x.Items).ToList();
            var progress = new Progress<double>(p => CleanProgress = p);
            var (removed, freed, skipped) = await _service.CleanAsync(entries, progress);
            ResultFreed = freed;
            ResultText = skipped > 0
                ? F("Junk_ResultSkippedFmt", removed, skipped)
                : F("Junk_ResultFmt", removed);
            HasResult = true;
            Log.Append("Log_Junk", F("Log_JunkDetailFmt", ByteSize.Humanize(freed), removed));
            Toast.Show(F("Junk_ToastFmt", ByteSize.Humanize(freed)), ToastKind.Success);

            // show what's left in the cleaned categories (nothing re-ticked)
            foreach (var x in picked) x.Category.IsExpanded = false;
            await Task.WhenAll(picked.Select(x => ScanOneAsync(x.Category, CancellationToken.None)));
        }
        catch (Exception ex)
        {
            Toast.Show(ex.Message, ToastKind.Error);
        }
        finally
        {
            IsCleaning = false;
            UpdateTotals();
        }
    }

    [RelayCommand]
    private void Reveal(JunkEntry? entry)
    {
        if (entry is null || entry.Paths.Count == 0) return;
        var p = entry.Paths[0];
        if (p == "::recyclebin") { Shell.Start("explorer.exe", "shell:RecycleBinFolder"); return; }
        Shell.Reveal(entry.IsGroup ? entry.Location : p);
    }

    [RelayCommand]
    private void ToggleExpand(JunkCategory? category)
    {
        if (category is { HasItems: true }) category.IsExpanded = !category.IsExpanded;
    }
}
