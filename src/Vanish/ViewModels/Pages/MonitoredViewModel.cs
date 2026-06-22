using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vanish.Helpers;
using Vanish.Models;
using Vanish.Services;

namespace Vanish.ViewModels.Pages;

public sealed partial class MonitoredViewModel : ObservableObject
{
    private readonly MonitorService _service;

    public MonitoredViewModel(MonitorService service)
    {
        _service = service;
        _hasBaseline = service.HasBaseline;
    }

    public ObservableCollectionEx<MonitorChange> Changes { get; } = new();

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _hasBaseline;
    [ObservableProperty] private string _statusMessage = "";

    [RelayCommand]
    private async Task TakeBaselineAsync()
    {
        IsBusy = true;
        StatusMessage = "…";
        try
        {
            var (programs, folders) = await _service.TakeBaselineAsync();
            HasBaseline = true;
            Changes.Clear();
            StatusMessage = string.Format(Loc.I.T("Mon_BaselineTaken"), programs, folders);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task CompareAsync()
    {
        IsBusy = true;
        StatusMessage = "…";
        try
        {
            var changes = await _service.CompareAsync();
            Changes.Reset(changes);
            StatusMessage = $"{changes.Count}";
        }
        finally { IsBusy = false; }
    }
}
