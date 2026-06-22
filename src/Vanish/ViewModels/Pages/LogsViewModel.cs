using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vanish.Helpers;
using Vanish.Models;
using Vanish.Services;

namespace Vanish.ViewModels.Pages;

public sealed partial class LogsViewModel : ObservableObject
{
    private readonly OperationLogService _service;

    public LogsViewModel(OperationLogService service) => _service = service;

    public ObservableCollectionEx<LogEntry> Entries { get; } = new();

    [ObservableProperty] private string _statusMessage = "";

    [RelayCommand]
    private void Load()
    {
        var entries = _service.GetAll();
        Entries.Reset(entries);
        StatusMessage = entries.Count == 0 ? Loc.I.T("Logs_Empty") : $"{entries.Count}";
    }

    [RelayCommand]
    private void Clear()
    {
        _service.Clear();
        Entries.Clear();
        StatusMessage = Loc.I.T("Logs_Empty");
    }
}
