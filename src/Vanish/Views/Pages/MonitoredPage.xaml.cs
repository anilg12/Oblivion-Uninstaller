using System.Windows.Controls;
using Vanish.ViewModels.Pages;

namespace Vanish.Views.Pages;

public partial class MonitoredPage : UserControl
{
    public MonitoredPage(MonitoredViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
