using System.Windows.Controls;
using Vanish.ViewModels.Pages;

namespace Vanish.Views.Pages;

public partial class LogsPage : UserControl
{
    private bool _loaded;

    public LogsPage(LogsViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (_loaded) return;
            _loaded = true;
            viewModel.LoadCommand.Execute(null);
        };
    }
}
