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

// logs database: everything Oblivion removed or changed, newest first
public sealed partial class LogsViewModel : PageViewModel
{
    private readonly ObservableCollectionEx<LogEntry> _items = new();
    private readonly ListCollectionView _view;
    private bool _dirty = true;

    public LogsViewModel()
    {
        _view = new ListCollectionView(_items) { Filter = Matches };
        Log.Changed += () =>
        {
            _dirty = true;
            if (IsShown) Application.Current?.Dispatcher.InvokeAsync(Reload);
        };
    }

    public ICollectionView Entries => _view;

    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private int _count;

    partial void OnSearchTextChanged(string value) => _view.Refresh();

    private bool Matches(object o)
    {
        if (o is not LogEntry e) return false;
        var q = SearchText?.Trim();
        return string.IsNullOrEmpty(q) || e.Title.Contains(q, StringComparison.CurrentCultureIgnoreCase) ||
               e.Detail.Contains(q, StringComparison.CurrentCultureIgnoreCase);
    }

    public override void OnShown()
    {
        base.OnShown();
        if (_dirty) Reload();
    }

    public override void RefreshTexts() => Reload();

    private async void Reload()
    {
        await Log.WarmUpAsync();
        _dirty = false;
        _items.Reset(Log.GetAll());
        Count = _items.Count;
    }

    [RelayCommand]
    private async Task ClearAsync()
    {
        if (Count == 0) return;
        bool ok = await Dialogs.ConfirmAsync(T("Logs_ClearTitle"), T("Logs_ClearText"), T("Act_Clear"), DialogTone.Danger);
        if (!ok) return;
        Log.Clear();
        Reload();
    }

    [RelayCommand]
    private void OpenFolder()
    {
        Directory.CreateDirectory(SettingsService.DataDir);
        Shell.OpenFolder(SettingsService.DataDir);
    }
}
