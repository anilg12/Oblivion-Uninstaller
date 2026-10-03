using System.Windows;
using System.Windows.Controls;
using Vanish.ViewModels.Pages;

namespace Vanish.Views.Pages;

public partial class BrowserExtensionsPage : UserControl
{
    private readonly BrowserExtensionsViewModel _vm;

    public BrowserExtensionsPage(BrowserExtensionsViewModel viewModel)
    {
        _vm = viewModel;
        DataContext = viewModel;
        InitializeComponent();
    }

    private void Browser_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string key }) _vm.BrowserFilter = key;
    }

    private void Browser_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string key } rb) rb.IsChecked = key == _vm.BrowserFilter;
    }
}
