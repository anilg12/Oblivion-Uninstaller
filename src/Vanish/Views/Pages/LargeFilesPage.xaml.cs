using System.Windows.Controls;
using Vanish.ViewModels.Pages;

namespace Vanish.Views.Pages;

public partial class LargeFilesPage : UserControl
{
    public LargeFilesPage(LargeFilesViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
