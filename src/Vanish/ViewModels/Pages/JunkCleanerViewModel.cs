using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vanish.Helpers;
using Vanish.Models;
using Vanish.Services;

namespace Vanish.ViewModels.Pages;

public sealed partial class JunkCleanerViewModel : ObservableObject
{
    private readonly IJunkCleanerService _cleaner;
    private readonly OperationLogService _log;

    public JunkCleanerViewModel(IJunkCleanerService cleaner, OperationLogService log)
    {
        _cleaner = cleaner;
        _log = log;
        Categories = new ObservableCollectionEx<JunkCategory>(_cleaner.GetCategories());
    }

    public ObservableCollectionEx<JunkCategory> Categories { get; }

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = "Click Analyze to measure reclaimable space.";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalText))]
    private long _totalBytes;

    public string TotalText => ByteSize.Humanize(TotalBytes);

    [RelayCommand]
    private async Task AnalyzeAsync()
    {
        IsBusy = true;
        var progress = new Progress<string>(m => StatusMessage = m);
        try
        {
            await _cleaner.ScanAsync(Categories.Where(c => c.IsSelected), progress);
            TotalBytes = Categories.Where(c => c.IsSelected).Sum(c => c.SizeBytes);
            StatusMessage = $"Found {TotalText} of reclaimable space.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CleanAsync()
    {
        IsBusy = true;
        var progress = new Progress<string>(m => StatusMessage = m);
        try
        {
            long freed = await _cleaner.CleanAsync(Categories.Where(c => c.IsSelected), progress);
            _log.Append("Junk cleaned", ByteSize.Humanize(freed) + " freed");
            StatusMessage = $"Cleaned {ByteSize.Humanize(freed)}.";
            // reset sizes after cleaning
            foreach (var c in Categories) { c.SizeBytes = 0; c.FileCount = 0; }
            TotalBytes = 0;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
