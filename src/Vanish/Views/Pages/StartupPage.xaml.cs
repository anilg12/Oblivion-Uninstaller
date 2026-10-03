using System.Windows.Controls;
using Vanish.ViewModels.Pages;

namespace Vanish.Views.Pages;

public partial class StartupPage : UserControl
{
    public StartupPage(StartupViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
