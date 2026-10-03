using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vanish.Helpers;
using Vanish.Services;

namespace Vanish.ViewModels.Pages;

/// <summary>
/// Evidence remover: wipes a drive's free space so files deleted in the past can't be
/// recovered. Existing files are never touched.
/// </summary>
public sealed partial class EvidenceViewModel : PageViewModel
{
    private readonly EvidenceService _service;
    private CancellationTokenSource? _cts;
    private readonly Stopwatch _clock = new();
    private readonly System.Windows.Threading.DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };

    public EvidenceViewModel(EvidenceService service)
    {
        _service = service;
        _timer.Tick += (_, _) => ElapsedText = _clock.Elapsed.ToString(@"hh\:mm\:ss");
    }

    public ObservableCollectionEx<DriveChoice> Drives { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SsdWarning))]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    private DriveChoice? _selectedDrive;

    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private int _pass;
    [ObservableProperty] private string _elapsedText = "00:00:00";
    [ObservableProperty] private string _statusText = "";

    public bool SsdWarning => SelectedDrive?.IsSsd == true;

    public override void OnShown()
    {
        base.OnShown();
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (IsRunning) return;
        var keep = SelectedDrive?.Root;
        Drives.Reset(await _service.GetDrivesAsync());
        SelectedDrive = Drives.FirstOrDefault(d => d.Root == keep) ?? Drives.FirstOrDefault();
    }

    private bool CanStart() => SelectedDrive is not null && !IsRunning;

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        var drive = SelectedDrive;
        if (drive is null) return;
        var text = T("Evid_ConfirmText") + (drive.IsSsd ? "\n\n" + T("Evid_SsdNote") : "");
        bool ok = await Dialogs.ConfirmAsync(F("Evid_ConfirmTitleFmt", drive.Title), text, T("Evid_Start"), DialogTone.Warning);
        if (!ok) return;

        IsRunning = true;
        Pass = 0;
        StatusText = T("Evid_Preparing");
        _clock.Restart();
        _timer.Start();
        _cts = new CancellationTokenSource();
        try
        {
            var progress = new Progress<int>(p =>
            {
                Pass = p;
                StatusText = F("Evid_PassFmt", p);
            });
            bool success = await _service.WipeFreeSpaceAsync(drive.Root, progress, _cts.Token);
            if (success)
            {
                Pass = 4;
                Log.Append("Log_Evidence", drive.Title);
                Toast.Show(F("Evid_DoneFmt", drive.Title), ToastKind.Success);
                StatusText = T("Evid_Done");
            }
            else
            {
                Toast.Show(T("Evid_Failed"), ToastKind.Error);
                StatusText = T("Evid_Failed");
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = T("Work_Cancelled");
            Toast.Show(T("Work_Cancelled"));
        }
        finally
        {
            _timer.Stop();
            _clock.Stop();
            IsRunning = false;
            StartCommand.NotifyCanExecuteChanged();
            _ = LoadAsync();
        }
    }

    [RelayCommand]
    private void Stop() => _cts?.Cancel();
}
