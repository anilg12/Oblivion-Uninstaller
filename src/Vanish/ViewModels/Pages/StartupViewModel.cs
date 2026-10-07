using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vanish.Helpers;
using Vanish.Models;
using Vanish.Services;

namespace Vanish.ViewModels.Pages;

// startup manager. disabling works like task manager (reversible), deleting removes the entry
// after backing it up
public sealed partial class StartupViewModel : PageViewModel
{
    private readonly IStartupService _service;
    private readonly ObservableCollectionEx<StartupEntry> _items = new();
    private readonly ListCollectionView _view;
    private Task? _loadTask;
    private bool _applying;

    public StartupViewModel(IStartupService service)
    {
        _service = service;
        _view = new ListCollectionView(_items) { Filter = Matches };
    }

    public ICollectionView Entries => _view;

    public bool IsLoaded { get; private set; }

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _filter = "all";   // all | on | off
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private int _enabledCount;
    [ObservableProperty] private int _disabledCount;

    partial void OnFilterChanged(string value) => _view.Refresh();
    partial void OnSearchTextChanged(string value) => _view.Refresh();

    private bool Matches(object o)
    {
        if (o is not StartupEntry e) return false;
        if (Filter == "on" && !e.IsEnabled) return false;
        if (Filter == "off" && e.IsEnabled) return false;
        var q = SearchText?.Trim();
        return string.IsNullOrEmpty(q) || e.Name.Contains(q, StringComparison.CurrentCultureIgnoreCase) ||
               e.Command.Contains(q, StringComparison.OrdinalIgnoreCase);
    }

    public override void OnShown()
    {
        base.OnShown();
        _ = EnsureLoadedAsync(false);
    }

    public override void RefreshTexts() => _items.Reset(_items.ToList());

    public Task EnsureLoadedAsync(bool force)
    {
        if (_loadTask is null || (force && _loadTask.IsCompleted)) _loadTask = LoadAsync();
        return _loadTask;
    }

    [RelayCommand]
    private Task RefreshAsync() => EnsureLoadedAsync(true);

    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            foreach (var old in _items) old.PropertyChanged -= OnEntryChanged;
            var list = await _service.GetStartupEntriesAsync();
            foreach (var e in list) e.PropertyChanged += OnEntryChanged;
            _items.Reset(list);
            IsLoaded = true;
            UpdateCounts();
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

    private void UpdateCounts()
    {
        TotalCount = _items.Count;
        EnabledCount = _items.Count(e => e.IsEnabled);
        DisabledCount = TotalCount - EnabledCount;
    }

    // the toggle in each row writes straight through to Windows (and reverts if that fails)
    private async void OnEntryChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_applying || e.PropertyName != nameof(StartupEntry.IsEnabled) || sender is not StartupEntry entry) return;
        try
        {
            if (!entry.CanToggle) throw new InvalidOperationException(T("Startup_CannotToggle"));
            await _service.SetEnabledAsync(entry, entry.IsEnabled);
            Log.Append("Log_Startup", $"{entry.Name}: {T(entry.IsEnabled ? "Startup_On" : "Startup_Off")}");
            Toast.Show(F(entry.IsEnabled ? "Startup_EnabledFmt" : "Startup_DisabledFmt", entry.Name), ToastKind.Success);
        }
        catch (Exception ex)
        {
            _applying = true;
            entry.IsEnabled = !entry.IsEnabled;
            _applying = false;
            Toast.Show(ex.Message, ToastKind.Error);
        }
        UpdateCounts();
        if (Filter != "all") _view.Refresh();
    }

    [RelayCommand]
    private async Task DeleteAsync(StartupEntry? entry)
    {
        if (entry is null) return;
        bool ok = await Dialogs.ConfirmAsync(F("Startup_DeleteTitleFmt", entry.Name), T("Startup_DeleteText"), T("Act_Delete"),
            DialogTone.Danger, new[] { entry.Command, entry.Source });
        if (!ok) return;
        try
        {
            await _service.DeleteAsync(entry);
            entry.PropertyChanged -= OnEntryChanged;
            _items.Remove(entry);
            UpdateCounts();
            Log.Append("Log_Startup", $"{entry.Name}: {T("Startup_Deleted")}");
            Toast.Show(F("Startup_DeletedFmt", entry.Name), ToastKind.Success);
        }
        catch (Exception ex)
        {
            Toast.Show(ex.Message, ToastKind.Error);
        }
    }

    [RelayCommand]
    private void Reveal(StartupEntry? entry)
    {
        if (entry is null) return;
        if (entry.ExecutablePath is { } exe && System.IO.File.Exists(exe)) Shell.Reveal(exe);
        else if (entry.Source.StartsWith("HK", StringComparison.OrdinalIgnoreCase)) Shell.OpenRegistry(entry.Source);
        else Shell.OpenFolder(entry.Source);
    }
}
