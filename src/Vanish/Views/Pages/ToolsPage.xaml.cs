using System.Windows.Controls;
using Vanish.ViewModels.Pages;

namespace Vanish.Views.Pages;

public partial class ToolsPage : UserControl
{
    public ToolsPage(ToolsViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
