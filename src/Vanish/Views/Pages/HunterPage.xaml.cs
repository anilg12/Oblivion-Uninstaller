using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Vanish.Helpers;
using Vanish.Models;
using Vanish.ViewModels.Pages;

namespace Vanish.Views.Pages;

// crosshair: press on the target, drag over any window, release to pick its program
public partial class HunterPage : UserControl
{
    private readonly HunterViewModel _vm;
    private readonly Stopwatch _throttle = new();
    private bool _aiming;

    public HunterPage(HunterViewModel viewModel)
    {
        _vm = viewModel;
        DataContext = viewModel;
        InitializeComponent();
    }

    private void Target_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _aiming = true;
        _vm.IsAiming = true;
        TargetArea.CaptureMouse();
        Mouse.OverrideCursor = Cursors.Cross;
        CrossIcon.Visibility = Visibility.Hidden;
        Core.Opacity = 0.35;
        _throttle.Restart();
        e.Handled = true;
    }

    private void Target_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_aiming || _throttle.ElapsedMilliseconds < 90) return;
        _throttle.Restart();
        var app = _vm.Peek();
        PreviewText.Text = app is null ? Loc.I["Hunter_DragHint"] : $"{app.ProcessName} — {app.WindowTitle}";
    }

    private void Target_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_aiming) return;
        var app = _vm.Peek();
        EndAim();
        if (app is not null) _vm.SetTarget(app);
        e.Handled = true;
    }

    private void Target_LostCapture(object sender, MouseEventArgs e)
    {
        if (_aiming) EndAim();
    }

    private void EndAim()
    {
        _aiming = false;
        _vm.IsAiming = false;
        if (TargetArea.IsMouseCaptured) TargetArea.ReleaseMouseCapture();
        Mouse.OverrideCursor = null;
        CrossIcon.Visibility = Visibility.Visible;
        Core.Opacity = 1;
        PreviewText.Text = Loc.I["Hunter_DragHint"];
    }

    private void Running_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox { SelectedItem: RunningApp app }) _vm.SetTarget(app);
    }
}
