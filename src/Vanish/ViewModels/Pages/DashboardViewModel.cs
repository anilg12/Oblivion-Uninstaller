using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vanish.Helpers;
using Vanish.Models;
using Vanish.Services;

namespace Vanish.ViewModels.Pages;

public sealed partial class DashboardViewModel : ObservableObject
{
    private readonly IInstalledProgramsService _programs;
    private readonly IStartupService _startup;
    private readonly OperationLogService _log;
    private readonly NavigationService _nav;

    public DashboardViewModel(
        IInstalledProgramsService programs,
        IStartupService startup,
        OperationLogService log,
        NavigationService nav)
    {
        _programs = programs;
        _startup = startup;
        _log = log;
        _nav = nav;
    }

    [ObservableProperty] private int _installedCount;
    [ObservableProperty] private string _totalSize = "—";
    [ObservableProperty] private int _startupCount;
    [ObservableProperty] private string _freeSpace = "—";
    [ObservableProperty] private string _windowsVersion = "Windows 11";
    [ObservableProperty] private bool _isLoading;

    public ObservableCollectionEx<InstalledProgram> LargestApps { get; } = new();
    public ObservableCollectionEx<LogEntry> RecentActivity { get; } = new();

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var all = await _programs.GetInstalledProgramsAsync();
            InstalledCount = all.Count;
            TotalSize = ByteSize.Humanize(all.Sum(p => p.EstimatedSizeBytes));

            LargestApps.Reset(all
                .Where(p => p.EstimatedSizeBytes > 0)
                .OrderByDescending(p => p.EstimatedSizeBytes)
                .Take(5));

            var startup = await _startup.GetStartupEntriesAsync();
            StartupCount = startup.Count;

            FreeSpace = GetFreeSpace();
            WindowsVersion = GetWindowsVersion();

            RecentActivity.Reset(_log.GetAll().Take(6));
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void Go(string? tag)
    {
        if (!string.IsNullOrWhiteSpace(tag))
            _nav.Navigate(tag!);
    }

    private static string GetFreeSpace()
    {
        try
        {
            var sys = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)) ?? "C:\\";
            var drive = new DriveInfo(sys);
            return $"{ByteSize.Humanize(drive.AvailableFreeSpace)} / {ByteSize.Humanize(drive.TotalSize)}";
        }
        catch
        {
            return "—";
        }
    }

    private static string GetWindowsVersion()
    {
        try
        {
            var v = Environment.OSVersion.Version;
            // Windows 11 reports build >= 22000.
            var name = v.Build >= 22000 ? "Windows 11" : "Windows 10";
            return $"{name} (build {v.Build})";
        }
        catch
        {
            return "Windows 11";
        }
    }
}
