using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vanish.Helpers;
using Vanish.Models;
using Vanish.Services;

namespace Vanish.ViewModels.Pages;

public sealed partial class BrowserExtensionsViewModel : ObservableObject
{
    private readonly BrowserExtensionsService _service;

    public BrowserExtensionsViewModel(BrowserExtensionsService service) => _service = service;

    public ObservableCollectionEx<BrowserExtension> Extensions { get; } = new();

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusMessage = "";

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var items = await _service.GetExtensionsAsync();
            Extensions.Reset(items);
            StatusMessage = items.Count == 0
                ? Loc.I.T("Browser_Empty")
                : $"{items.Count}";
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
    private async Task RemoveAsync(BrowserExtension? ext)
    {
        if (ext is null) return;
        var confirm = MessageBox.Show(
            $"{Loc.I.T("Act_Remove")} \"{ext.Name}\"?\n{Loc.I.T("Browser_RemoveWarn")}",
            Loc.I.T("Confirm"), MessageBoxButton.OKCancel, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.OK) return;

        try
        {
            await _service.RemoveAsync(ext);
            Extensions.Remove(ext);
            StatusMessage = $"\"{ext.Name}\" removed.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not remove: {ex.Message}";
        }
    }
}
