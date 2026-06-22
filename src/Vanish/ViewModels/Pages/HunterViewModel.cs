using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vanish.Helpers;
using Vanish.Models;
using Vanish.Services;

namespace Vanish.ViewModels.Pages;

public sealed partial class HunterViewModel : ObservableObject
{
    private readonly HunterService _service;

    public HunterViewModel(HunterService service) => _service = service;

    public ObservableCollectionEx<RunningApp> Apps { get; } = new();

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusMessage = "";

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var apps = await _service.GetRunningAppsAsync();
            Apps.Reset(apps);
            StatusMessage = $"{apps.Count}";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task EndTaskAsync(RunningApp? app)
    {
        if (app is null) return;
        var confirm = MessageBox.Show(
            $"{Loc.I.T("Hunter_End")}: \"{app.WindowTitle}\" ({app.ProcessName})?",
            Loc.I.T("Confirm"), MessageBoxButton.OKCancel, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.OK) return;

        try
        {
            _service.EndTask(app);
            Apps.Remove(app);
            StatusMessage = $"\"{app.ProcessName}\" ended.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        await Task.CompletedTask;
    }

    [RelayCommand]
    private void OpenLocation(RunningApp? app)
    {
        if (app is null) return;
        try { _service.OpenLocation(app); }
        catch (Exception ex) { StatusMessage = ex.Message; }
    }
}
