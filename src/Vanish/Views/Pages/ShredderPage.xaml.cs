using System.Windows.Controls;
using Vanish.ViewModels.Pages;

namespace Vanish.Views.Pages;

// files dropped from Explorer arrive through the main window (WM_DROPFILES, see MainWindow)
public partial class ShredderPage : UserControl
{
    public ShredderPage(ShredderViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
