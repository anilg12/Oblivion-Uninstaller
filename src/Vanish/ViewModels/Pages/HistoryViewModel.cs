using System.ComponentModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vanish.Services;

namespace Vanish.ViewModels.Pages;

// history & privacy: windows usage traces, nothing ticked by default
public sealed partial class HistoryViewModel : PageViewModel
{
    private readonly HistoryCleanerService _service;

    public HistoryViewModel(HistoryCleanerService service)
    {
        _service = service;
        Items = service.CreateItems();
        foreach (var i in Items) i.PropertyChanged += OnItemChanged;
    }

    public IReadOnlyList<PrivacyItem> Items { get; private set; }

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private int _selectedCount;

    public override void OnShown()
    {
        base.OnShown();
        _ = CountAsync();
    }

    public override void RefreshTexts()
    {
        var selected = Items.Where(i => i.IsSelected).Select(i => i.Id).ToHashSet();
        var counts = Items.ToDictionary(i => i.Id, i => i.Count);
        foreach (var i in Items) i.PropertyChanged -= OnItemChanged;
        Items = _service.CreateItems();
        foreach (var i in Items)
        {
            i.IsSelected = selected.Contains(i.Id);
            i.Count = counts.TryGetValue(i.Id, out var c) ? c : -1;
            i.PropertyChanged += OnItemChanged;
        }
        OnPropertyChanged(nameof(Items));
    }

    private Task CountAsync() => _service.CountAsync(Items);

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PrivacyItem.IsSelected)) return;
        SelectedCount = Items.Count(i => i.IsSelected);
        CleanCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void SelectNone()
    {
        foreach (var i in Items) i.IsSelected = false;
    }

    private bool CanClean() => SelectedCount > 0 && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanClean))]
    private async Task CleanAsync()
    {
        var picked = Items.Where(i => i.IsSelected).ToList();
        bool ok = await Dialogs.ConfirmAsync(F("Priv_ConfirmTitleFmt", picked.Count), T("Priv_ConfirmText"), T("Act_Clean"),
            DialogTone.Warning, picked.Select(i => i.Title));
        if (!ok) return;

        IsBusy = true;
        try
        {
            int cleared = await _service.CleanAsync(picked);
            if (picked.Any(i => i.Id == "clipboard"))
            {
                try { Clipboard.Clear(); cleared++; } catch { /* clipboard busy */ }
            }
            Log.Append("Log_Privacy", string.Join(", ", picked.Select(i => i.Title)));
            Toast.Show(F("Priv_DoneFmt", picked.Count), ToastKind.Success);
            foreach (var i in picked) i.IsSelected = false;
            await CountAsync();
        }
        catch (Exception ex)
        {
            Toast.Show(ex.Message, ToastKind.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
