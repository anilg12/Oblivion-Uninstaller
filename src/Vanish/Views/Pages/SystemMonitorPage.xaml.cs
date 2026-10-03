using System.Windows.Controls;
using Vanish.ViewModels.Pages;

namespace Vanish.Views.Pages;

public partial class SystemMonitorPage : UserControl
{
    public SystemMonitorPage(SystemMonitorViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
