using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Vanish.ViewModels.Pages;

namespace Vanish.Views.Pages;

public partial class UninstallerPage : UserControl
{
    private readonly UninstallerViewModel _vm;

    public UninstallerPage(UninstallerViewModel viewModel)
    {
        _vm = viewModel;
        DataContext = viewModel;
        InitializeComponent();

        // Ctrl+F jumps to the search box; Delete starts the uninstall.
        PreviewKeyDown += (_, e) =>
        {
            if (!_vm.IsBrowsing) return;
            if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
            {
                SearchBox.Focus();
                e.Handled = true;
            }
            else if (e.Key == Key.Delete && ProgramList.IsKeyboardFocusWithin && _vm.UninstallCommand.CanExecute(null))
            {
                _vm.UninstallCommand.Execute(null);
                e.Handled = true;
            }
        };
    }

    private void Sort_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string tag }) _vm.Sort = tag;
    }

    private void ProgramList_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject src && ItemsControl.ContainerFromElement(ProgramList, src) is ListBoxItem
            && _vm.UninstallCommand.CanExecute(null))
            _vm.UninstallCommand.Execute(null);
    }
}
