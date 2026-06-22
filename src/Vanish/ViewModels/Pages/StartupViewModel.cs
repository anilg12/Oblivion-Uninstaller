using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vanish.Helpers;
using Vanish.Models;
using Vanish.Services;

namespace Vanish.ViewModels.Pages;

public sealed partial class StartupViewModel : ObservableObject
{
    private readonly IStartupService _startup;

    public StartupViewModel(IStartupService startup) => _startup = startup;

    public ObservableCollectionEx<StartupEntry> Entries { get; } = new();

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusMessage = "";

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var entries = await _startup.GetStartupEntriesAsync();
            Entries.Reset(entries);
            StatusMessage = $"{entries.Count} startup entries.";
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
    private async Task DeleteAsync(StartupEntry? entry)
    {
        if (entry is null) return;
        var confirm = MessageBox.Show(
            $"Remove \"{entry.Name}\" from startup?",
            "Confirm", MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.OK) return;

        try
        {
            await _startup.DeleteAsync(entry);
            Entries.Remove(entry);
            StatusMessage = $"Removed \"{entry.Name}\".";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not remove: {ex.Message}";
        }
    }
}
