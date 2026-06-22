using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vanish.Helpers;
using Vanish.Services;

namespace Vanish.ViewModels.Pages;

public sealed partial class ToolsViewModel : ObservableObject
{
    private readonly NavigationService _nav;

    public ToolsViewModel(NavigationService nav) => _nav = nav;

    public Loc Loc => Loc.I;

    /// <summary>Navigates to a sub-tool page by tag (e.g. "Startup", "Junk").</summary>
    [RelayCommand]
    private void Open(string? tag)
    {
        if (!string.IsNullOrWhiteSpace(tag))
            _nav.Navigate(tag!);
    }
}
