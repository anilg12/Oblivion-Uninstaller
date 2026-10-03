using System.Windows;
using System.Windows.Controls;
using Vanish.Services;
using Vanish.ViewModels.Pages;

namespace Vanish.Views.Pages;

public partial class EvidencePage : UserControl
{
    private readonly EvidenceViewModel _vm;

    public EvidencePage(EvidenceViewModel viewModel)
    {
        _vm = viewModel;
        DataContext = viewModel;
        InitializeComponent();
    }

    private void Drive_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: DriveChoice d }) _vm.SelectedDrive = d;
    }

    private void Drive_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: DriveChoice d } rb) rb.IsChecked = Equals(d, _vm.SelectedDrive);
    }
}
