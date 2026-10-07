using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vanish.Helpers;
using Vanish.Models;
using Vanish.Services;

namespace Vanish.ViewModels.Pages;

// hunter mode: drag the crosshair onto a window (or pick a running app) to see what program it is,
// then uninstall it, end it or open its folder
public sealed partial class HunterViewModel : PageViewModel
{
    private readonly HunterService _hunter;
    private readonly UninstallerViewModel _apps;

    public HunterViewModel(HunterService hunter, UninstallerViewModel apps)
    {
        _hunter = hunter;
        _apps = apps;
    }

    public ObservableCollectionEx<RunningApp> RunningApps { get; } = new();

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isAiming;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTarget))]
    [NotifyCanExecuteChangedFor(nameof(UninstallTargetCommand))]
    [NotifyCanExecuteChangedFor(nameof(EndTargetCommand))]
    [NotifyCanExecuteChangedFor(nameof(RevealTargetCommand))]
    private RunningApp? _target;

    [ObservableProperty] private string _targetProgramText = "";

    public bool HasTarget => Target is not null;

    public override void OnShown()
    {
        base.OnShown();
        _ = RefreshAsync();
        _ = _apps.EnsureLoadedAsync(false);
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsLoading = true;
        try
        {
            RunningApps.Reset(await _hunter.GetRunningAppsAsync());
        }
        finally
        {
            IsLoading = false;
        }
    }

    // called by the page while the crosshair is dragged / when it is dropped
    public RunningApp? Peek() => _hunter.AppUnderCursor();

    public void SetTarget(RunningApp? app)
    {
        Target = app;
        if (app is null)
        {
            TargetProgramText = "";
            return;
        }
        var program = _apps.FindByExecutable(app.FilePath);
        TargetProgramText = program is null ? T("Hunter_NotInstalledProgram") : F("Hunter_BelongsToFmt", program.DisplayName);
    }

    private bool CanAct() => Target is not null;

    [RelayCommand(CanExecute = nameof(CanAct))]
    private void UninstallTarget()
    {
        if (Target is null) return;
        var program = _apps.FindByExecutable(Target.FilePath);
        if (program is null)
        {
            Toast.Show(T("Hunter_NotInstalledProgram"), ToastKind.Warning);
            return;
        }
        _apps.Reveal(program);
        if (_apps.UninstallCommand.CanExecute(null)) _apps.UninstallCommand.Execute(null);
    }

    [RelayCommand(CanExecute = nameof(CanAct))]
    private async Task EndTargetAsync()
    {
        var app = Target;
        if (app is null) return;
        bool ok = await Dialogs.ConfirmAsync(F("Mon_EndTitleFmt", app.ProcessName), T("Hunter_EndText"), T("Act_EndTask"), DialogTone.Danger,
            new[] { app.WindowTitle, app.FilePath ?? app.PidText });
        if (!ok) return;
        try
        {
            _hunter.EndTask(app);
            Log.Append("Log_EndTask", app.ProcessName);
            Toast.Show(F("Mon_EndedFmt", app.ProcessName), ToastKind.Success);
            SetTarget(null);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            Toast.Show(ex.Message, ToastKind.Error);
        }
    }

    [RelayCommand(CanExecute = nameof(CanAct))]
    private void RevealTarget()
    {
        if (Target is not null) _hunter.OpenLocation(Target);
    }

    [RelayCommand]
    private void Pick(RunningApp? app) => SetTarget(app);
}
