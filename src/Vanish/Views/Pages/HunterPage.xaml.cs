using System.Windows.Controls;
using Vanish.ViewModels.Pages;

namespace Vanish.Views.Pages;

public partial class HunterPage : UserControl
{
    private bool _loaded;

    public HunterPage(HunterViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (_loaded) return;
            _loaded = true;
            await viewModel.LoadCommand.ExecuteAsync(null);
        };
    }
}
