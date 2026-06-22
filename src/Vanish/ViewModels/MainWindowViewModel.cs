using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vanish.Helpers;
using Vanish.ViewModels.Pages;

namespace Vanish.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
    public MainWindowViewModel(UninstallerViewModel uninstaller, SettingsViewModel settings)
    {
        Uninstaller = uninstaller;
        Settings = settings;
    }

    /// <summary>Shared singleton so the sidebar action buttons act on the All-apps selection.</summary>
    public UninstallerViewModel Uninstaller { get; }

    public SettingsViewModel Settings { get; }

    public Loc Loc => Loc.I;

    [RelayCommand]
    private void ToggleTheme() => Settings.ToggleThemeCommand.Execute(null);

    [RelayCommand]
    private void ToggleLanguage() => Settings.ToggleLanguageCommand.Execute(null);
}
