using System.Windows.Controls;
using Vanish.ViewModels.Pages;

namespace Vanish.Views.Pages;

public partial class BackupsPage : UserControl
{
    public BackupsPage(BackupsViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
