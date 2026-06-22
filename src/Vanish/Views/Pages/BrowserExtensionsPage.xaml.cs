using System.Windows.Controls;
using Vanish.ViewModels.Pages;

namespace Vanish.Views.Pages;

public partial class BrowserExtensionsPage : UserControl
{
    private bool _loaded;

    public BrowserExtensionsPage(BrowserExtensionsViewModel viewModel)
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
