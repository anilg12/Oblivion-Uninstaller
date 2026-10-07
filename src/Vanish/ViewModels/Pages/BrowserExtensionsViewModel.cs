using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vanish.Helpers;
using Vanish.Models;
using Vanish.Services;

namespace Vanish.ViewModels.Pages;

// browser filter chip ("All", "Google Chrome", ...)
public sealed partial class BrowserChoice : ObservableObject
{
    public required string Key { get; init; }
    public required string Title { get; set; }
    public int Count { get; init; }
    public string? Icon { get; init; }
}

// extensions of every installed browser. nothing is selected by default, removal asks first
public sealed partial class BrowserExtensionsViewModel : PageViewModel
{
    private readonly BrowserExtensionsService _service;
    private readonly ObservableCollectionEx<BrowserExtension> _items = new();
    private readonly ListCollectionView _view;
    private Task? _loadTask;

    private static readonly Dictionary<string, string> BrowserProcess = new()
    {
        ["Microsoft Edge"] = "msedge",
        ["Google Chrome"] = "chrome",
        ["Brave"] = "brave",
        ["Vivaldi"] = "vivaldi",
        ["Opera"] = "opera",
        ["Opera GX"] = "opera",
        ["Firefox"] = "firefox",
    };

    public BrowserExtensionsViewModel(BrowserExtensionsService service)
    {
        _service = service;
        _view = new ListCollectionView(_items) { Filter = Matches };
    }

    public ICollectionView Extensions => _view;
    public ObservableCollectionEx<BrowserChoice> Browsers { get; } = new();

    public bool IsLoaded { get; private set; }

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _browserFilter = "all";
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private int _browserCount;
    [ObservableProperty] private int _visibleCount;
    [ObservableProperty] private int _selectedCount;
    [ObservableProperty] private string _runningWarning = "";

    partial void OnSearchTextChanged(string value) => Refresh();
    partial void OnBrowserFilterChanged(string value) => Refresh();

    public bool ShowEmpty => IsLoaded && !IsLoading && TotalCount == 0;

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(ShowEmpty));
    partial void OnTotalCountChanged(int value) => OnPropertyChanged(nameof(ShowEmpty));

    private void Refresh()
    {
        _view.Refresh();
        VisibleCount = _view.Count;
    }

    private bool Matches(object o)
    {
        if (o is not BrowserExtension e) return false;
        if (BrowserFilter != "all" && e.Browser != BrowserFilter) return false;
        var q = SearchText?.Trim();
        return string.IsNullOrEmpty(q) || e.Name.Contains(q, StringComparison.CurrentCultureIgnoreCase) ||
               e.Id.Contains(q, StringComparison.OrdinalIgnoreCase);
    }

    public override void OnShown()
    {
        base.OnShown();
        _ = EnsureLoadedAsync(false);
        UpdateRunningWarning();
    }

    public override void RefreshTexts()
    {
        if (Browsers.FirstOrDefault(b => b.Key == "all") is { } all) all.Title = T("Ext_AllBrowsers");
        Browsers.Reset(Browsers.ToList());
        UpdateRunningWarning();
    }

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
            foreach (var old in _items) old.PropertyChanged -= OnItemChanged;
            var list = await _service.GetExtensionsAsync();
            foreach (var e in list) e.PropertyChanged += OnItemChanged;
            _items.Reset(list);
            TotalCount = list.Count;
            var groups = list.GroupBy(e => e.Browser).ToList();
            BrowserCount = groups.Count;
            var chips = new List<BrowserChoice> { new() { Key = "all", Title = T("Ext_AllBrowsers"), Count = list.Count } };
            chips.AddRange(groups.Select(g => new BrowserChoice { Key = g.Key, Title = g.Key, Count = g.Count(), Icon = g.First().BrowserIcon }));
            Browsers.Reset(chips);
            if (BrowserFilter != "all" && groups.All(g => g.Key != BrowserFilter)) BrowserFilter = "all";
            IsLoaded = true;
            Refresh();
            UpdateSelection();
            UpdateRunningWarning();
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

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BrowserExtension.IsSelected)) UpdateSelection();
    }

    private void UpdateSelection()
    {
        SelectedCount = _items.Count(e => e.IsSelected);
        RemoveSelectedCommand.NotifyCanExecuteChanged();
    }

    private void UpdateRunningWarning()
    {
        var running = _items.Select(e => e.Browser).Distinct()
            .Where(b => BrowserProcess.TryGetValue(b, out var proc) && IsRunning(proc))
            .ToList();
        RunningWarning = running.Count == 0 ? "" : F("Ext_RunningFmt", string.Join(", ", running));
    }

    private static bool IsRunning(string processName)
    {
        var procs = Process.GetProcessesByName(processName);
        foreach (var p in procs) p.Dispose();
        return procs.Length > 0;
    }

    [RelayCommand]
    private Task RemoveOneAsync(BrowserExtension? ext) => ext is null ? Task.CompletedTask : RemoveAsync(new[] { ext });

    private bool CanRemoveSelected() => SelectedCount > 0;

    [RelayCommand(CanExecute = nameof(CanRemoveSelected))]
    private Task RemoveSelectedAsync() => RemoveAsync(_items.Where(e => e.IsSelected).ToList());

    [RelayCommand]
    private void SelectNone()
    {
        foreach (var e in _items) e.IsSelected = false;
    }

    private async Task RemoveAsync(IReadOnlyList<BrowserExtension> list)
    {
        if (list.Count == 0) return;
        bool ok = await Dialogs.ConfirmAsync(
            list.Count == 1 ? F("Ext_RemoveOneTitleFmt", list[0].Name) : F("Ext_RemoveManyTitleFmt", list.Count),
            T("Ext_RemoveText"),
            T("Act_Remove"),
            DialogTone.Warning,
            list.Select(e => $"{e.Name}  ·  {e.Browser}{(e.Profile is null ? "" : " · " + e.Profile)}"));
        if (!ok) return;

        int removed = 0;
        foreach (var e in list)
        {
            try
            {
                await _service.RemoveAsync(e);
                Log.Append("Log_Extension", $"{e.Name} ({e.Browser})");
                removed++;
            }
            catch (Exception ex)
            {
                Toast.Show($"{e.Name}: {ex.Message}", ToastKind.Error);
            }
        }
        if (removed > 0) Toast.Show(F("Ext_RemovedFmt", removed), ToastKind.Success);
        await EnsureLoadedAsync(true);
    }

    [RelayCommand]
    private void Reveal(BrowserExtension? ext)
    {
        if (ext is not null) Shell.Reveal(ext.Path);
    }
}
