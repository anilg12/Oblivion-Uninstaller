using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vanish.Helpers;
using Vanish.Models;
using Vanish.Services;

namespace Vanish.ViewModels.Pages;

// install monitor: snapshot before installing, compare after to see what got added,
// remove it if you want (nothing pre-selected)
public sealed partial class MonitoredViewModel : PageViewModel
{
    private readonly MonitorService _monitor;
    private readonly ISystemRestoreService _restore;
    private readonly UninstallerViewModel _apps;

    public MonitoredViewModel(MonitorService monitor, ISystemRestoreService restore, UninstallerViewModel apps)
    {
        _monitor = monitor;
        _restore = restore;
        _apps = apps;
    }

    public ObservableCollectionEx<MonitorChange> Changes { get; } = new();

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _hasBaseline;
    [ObservableProperty] private string _baselineText = "";
    [ObservableProperty] private bool _hasCompared;
    [ObservableProperty] private string _resultText = "";
    [ObservableProperty] private int _selectedCount;

    public override void OnShown()
    {
        base.OnShown();
        UpdateBaseline();
    }

    public override void RefreshTexts()
    {
        UpdateBaseline();
        Changes.Reset(Changes.ToList());
    }

    private void UpdateBaseline()
    {
        HasBaseline = _monitor.HasBaseline;
        BaselineText = _monitor.BaselineTime is { } t ? F("Mon_BaselineFmt", t.ToString("dd.MM.yyyy HH:mm")) : T("Mon_NoBaseline");
    }

    [RelayCommand]
    private async Task TakeBaselineAsync()
    {
        if (HasBaseline)
        {
            bool ok = await Dialogs.ConfirmAsync(T("Mon_RetakeTitle"), T("Mon_RetakeText"), T("Mon_TakeBaseline"));
            if (!ok) return;
        }
        IsBusy = true;
        try
        {
            var (programs, folders) = await _monitor.TakeBaselineAsync();
            Log.Append("Log_Baseline", F("Mon_BaselineDetailFmt", programs, folders));
            Toast.Show(T("Mon_BaselineSaved"), ToastKind.Success);
            SetChanges(Array.Empty<MonitorChange>());
            HasCompared = false;
            UpdateBaseline();
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

    [RelayCommand]
    private async Task CompareAsync()
    {
        IsBusy = true;
        try
        {
            var changes = await _monitor.CompareAsync();
            SetChanges(changes);
            HasCompared = true;
            ResultText = changes.Count == 0 ? T("Mon_NoChanges") : F("Mon_ChangesFmt", changes.Count);
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

    private void SetChanges(IReadOnlyList<MonitorChange> list)
    {
        foreach (var c in Changes) c.PropertyChanged -= OnChanged;
        Changes.Reset(list);
        foreach (var c in list) c.PropertyChanged += OnChanged;
        UpdateSelection();
    }

    private void OnChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MonitorChange.IsSelected)) UpdateSelection();
    }

    private void UpdateSelection()
    {
        SelectedCount = Changes.Count(c => c.IsSelected && c.Kind != "Program");
        RemoveSelectedCommand.NotifyCanExecuteChanged();
    }

    private bool CanRemove() => SelectedCount > 0;

    [RelayCommand(CanExecute = nameof(CanRemove))]
    private async Task RemoveSelectedAsync()
    {
        var picked = Changes.Where(c => c.IsSelected && c.Kind != "Program").ToList();
        bool ok = await Dialogs.ConfirmAsync(F("Mon_RemoveTitleFmt", picked.Count), T("Mon_RemoveText"), T("Act_Remove"),
            DialogTone.Danger, picked.Select(c => $"{c.KindText}: {c.Value}"));
        if (!ok) return;
        IsBusy = true;
        try
        {
            int removed = await _monitor.RemoveAsync(picked, _restore);
            Log.Append("Log_MonitorRemoved", F("Mon_RemovedDetailFmt", removed));
            Toast.Show(F("Mon_RemovedFmt", removed), ToastKind.Success);
            await CompareAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    // programs can't just be deleted: open them in All applications to uninstall properly
    [RelayCommand]
    private async Task UninstallProgramAsync(MonitorChange? change)
    {
        if (change is null) return;
        await _apps.EnsureLoadedAsync(false);
        if (_apps.FindByName(change.Value) is { } program) _apps.Reveal(program);
        else Toast.Show(T("Mon_ProgramNotFound"), ToastKind.Warning);
    }

    [RelayCommand]
    private void Reveal(MonitorChange? change)
    {
        if (change is null) return;
        if (change.Kind == "Folder") Shell.Reveal(change.Value);
        else if (change.Kind == "Registry" && change.RegistryKey is { } k) Shell.OpenRegistry(k);
    }
}
