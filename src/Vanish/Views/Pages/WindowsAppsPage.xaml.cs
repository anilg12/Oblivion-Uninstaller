using System.Windows.Controls;
using Vanish.ViewModels.Pages;

namespace Vanish.Views.Pages;

public partial class WindowsAppsPage : UserControl
{
    public WindowsAppsPage(WindowsAppsViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
