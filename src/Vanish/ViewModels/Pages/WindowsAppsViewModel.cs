using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vanish.Helpers;
using Vanish.Models;
using Vanish.Services;

namespace Vanish.ViewModels.Pages;

// Microsoft Store / MSIX apps. System frameworks are hidden unless asked for
public sealed partial class WindowsAppsViewModel : PageViewModel
{
    private readonly IWindowsAppsService _service;
    private readonly ObservableCollectionEx<WindowsApp> _items = new();
    private readonly ListCollectionView _view;
    private Task? _loadTask;

    public WindowsAppsViewModel(IWindowsAppsService service)
    {
        _service = service;
        _view = new ListCollectionView(_items) { Filter = Matches };
    }

    public ICollectionView Apps => _view;

    public bool IsLoaded { get; private set; }

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private bool _showFrameworks;
    [ObservableProperty] private int _userAppCount;
    [ObservableProperty] private int _visibleCount;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveCommand))]
    private WindowsApp? _selectedApp;

    partial void OnSearchTextChanged(string value) => Refresh();
    partial void OnShowFrameworksChanged(bool value) => Refresh();

    private void Refresh()
    {
        _view.Refresh();
        VisibleCount = _view.Count;
    }

    private bool Matches(object o)
    {
        if (o is not WindowsApp a) return false;
        if (!ShowFrameworks && a.IsFramework) return false;
        var q = SearchText?.Trim();
        return string.IsNullOrEmpty(q) ||
               a.Name.Contains(q, StringComparison.CurrentCultureIgnoreCase) ||
               (a.Publisher?.Contains(q, StringComparison.CurrentCultureIgnoreCase) ?? false);
    }

    public override void OnShown()
    {
        base.OnShown();
        _ = EnsureLoadedAsync(false);
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
            var apps = await _service.GetWindowsAppsAsync();
            _items.Reset(apps);
            UserAppCount = apps.Count(a => !a.IsFramework);
            IsLoaded = true;
            Refresh();
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

    private bool CanRemove() => SelectedApp is not null;

    [RelayCommand(CanExecute = nameof(CanRemove))]
    private async Task RemoveAsync()
    {
        var app = SelectedApp;
        if (app is null) return;
        var text = T(app.IsFramework ? "Store_RemoveFrameworkText" : "Store_RemoveText");
        bool ok = await Dialogs.ConfirmAsync(F("Store_RemoveTitleFmt", app.Name), text, T("Act_Remove"),
            app.IsFramework ? DialogTone.Danger : DialogTone.Warning,
            new[] { app.PackageFullName, $"{T("Apps_Publisher")}: {app.PublisherOrUnknown}" });
        if (!ok) return;

        IsLoading = true;
        try
        {
            if (await _service.RemoveAsync(app))
            {
                Log.Append("Log_StoreApp", app.Name);
                Toast.Show(F("Store_RemovedFmt", app.Name), ToastKind.Success);
                _items.Remove(app);
                UserAppCount = _items.Count(a => !a.IsFramework);
                SelectedApp = null;
                Refresh();
            }
            else
            {
                Toast.Show(F("Store_RemoveFailedFmt", app.Name), ToastKind.Error);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void Reveal(WindowsApp? app)
    {
        if (app?.InstallLocation is { } loc) Shell.OpenFolder(loc);
    }
}
