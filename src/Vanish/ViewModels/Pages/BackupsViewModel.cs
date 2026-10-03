using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vanish.Helpers;
using Vanish.Services;

namespace Vanish.ViewModels.Pages;

/// <summary>Backup manager: the .reg backups Oblivion made before deleting registry items, and System Restore points.</summary>
public sealed partial class BackupsViewModel : PageViewModel
{
    private readonly ISystemRestoreService _restore;

    public BackupsViewModel(ISystemRestoreService restore) => _restore = restore;

    public ObservableCollectionEx<RegistryBackupInfo> RegistryBackups { get; } = new();
    public ObservableCollectionEx<RestorePointInfo> RestorePoints { get; } = new();

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isCreating;
    [ObservableProperty] private string _tab = "registry";   // registry | restore

    public override void OnShown()
    {
        base.OnShown();
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var reg = _restore.ListRegistryBackupsAsync();
            var points = _restore.ListRestorePointsAsync();
            RegistryBackups.Reset(await reg);
            RestorePoints.Reset(await points);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RestoreBackupAsync(RegistryBackupInfo? backup)
    {
        if (backup is null) return;
        bool ok = await Dialogs.ConfirmAsync(T("Backup_RestoreTitle"), T("Backup_RestoreText"), T("Act_Restore"), DialogTone.Warning,
            new[] { backup.KeyPath, backup.CreatedText });
        if (!ok) return;
        if (await _restore.RestoreRegistryBackupAsync(backup.FilePath))
        {
            Log.Append("Log_Restore", backup.KeyPath);
            Toast.Show(T("Backup_Restored"), ToastKind.Success);
        }
        else
        {
            Toast.Show(T("Backup_RestoreFailed"), ToastKind.Error);
        }
    }

    [RelayCommand]
    private async Task DeleteBackupAsync(RegistryBackupInfo? backup)
    {
        if (backup is null) return;
        bool ok = await Dialogs.ConfirmAsync(T("Backup_DeleteTitle"), T("Backup_DeleteText"), T("Act_Delete"), DialogTone.Danger,
            new[] { backup.KeyPath });
        if (!ok) return;
        try
        {
            File.Delete(backup.FilePath);
            RegistryBackups.Remove(backup);
        }
        catch (Exception ex)
        {
            Toast.Show(ex.Message, ToastKind.Error);
        }
    }

    [RelayCommand]
    private void RevealBackup(RegistryBackupInfo? backup)
    {
        if (backup is not null) Shell.Reveal(backup.FilePath);
    }

    [RelayCommand]
    private void OpenBackupFolder()
    {
        Directory.CreateDirectory(SystemRestoreService.BackupDir);
        Shell.OpenFolder(SystemRestoreService.BackupDir);
    }

    [RelayCommand]
    private async Task CreateRestorePointAsync()
    {
        IsCreating = true;
        try
        {
            if (await _restore.CreateRestorePointAsync(T("Backup_ManualPointName")))
            {
                Log.Append("Log_Backup", T("Backup_ManualPointName"));
                Toast.Show(T("Backup_PointCreated"), ToastKind.Success);
                RestorePoints.Reset(await _restore.ListRestorePointsAsync());
            }
            else
            {
                Toast.Show(T("Backup_PointFailed"), ToastKind.Error);
            }
        }
        finally
        {
            IsCreating = false;
        }
    }

    [RelayCommand]
    private void OpenSystemRestore() => Shell.Start("rstrui.exe");
}
