using System.Windows.Controls;
using Vanish.ViewModels.Pages;

namespace Vanish.Views.Pages;

public partial class JunkCleanerPage : UserControl
{
    public JunkCleanerPage(JunkCleanerViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
