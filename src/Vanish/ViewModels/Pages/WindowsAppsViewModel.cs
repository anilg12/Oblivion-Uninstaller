using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vanish.Helpers;
using Vanish.Models;
using Vanish.Services;

namespace Vanish.ViewModels.Pages;

public sealed partial class WindowsAppsViewModel : ObservableObject
{
    private readonly IWindowsAppsService _service;

    public WindowsAppsViewModel(IWindowsAppsService service) => _service = service;

    public ObservableCollectionEx<WindowsApp> Apps { get; } = new();

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusMessage = "";

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        StatusMessage = Loc.I.T("Status_Reading");
        try
        {
            var apps = await _service.GetWindowsAppsAsync();
            Apps.Reset(apps);
            StatusMessage = $"{apps.Count} {Loc.I.T("Status_Apps")}";
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
    private async Task RemoveAsync(WindowsApp? app)
    {
        if (app is null) return;
        var confirm = MessageBox.Show(
            $"{Loc.I.T("Act_Uninstall")} \"{app.Name}\"?",
            Loc.I.T("Confirm"), MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.OK) return;

        IsLoading = true;
        try
        {
            var ok = await _service.RemoveAsync(app);
            if (ok)
            {
                Apps.Remove(app);
                StatusMessage = $"\"{app.Name}\" removed.";
            }
            else
            {
                StatusMessage = $"Could not remove \"{app.Name}\".";
            }
        }
        finally
        {
            IsLoading = false;
        }
    }
}
