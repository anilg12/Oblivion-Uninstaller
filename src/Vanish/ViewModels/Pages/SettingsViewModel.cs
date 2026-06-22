using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vanish.Helpers;
using Wpf.Ui.Appearance;

namespace Vanish.ViewModels.Pages;

public sealed partial class SettingsViewModel : ObservableObject
{
    public Loc Loc => Loc.I;

    [ObservableProperty] private bool _isDarkTheme = true;

    /// <summary>True when the UI language is Turkish (bound to the language switch).</summary>
    [ObservableProperty] private bool _isTurkish = true;

    public string AppVersion =>
        $"Vanish {Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.1.0"}";

    partial void OnIsDarkThemeChanged(bool value)
    {
        ApplicationThemeManager.Apply(value ? ApplicationTheme.Dark : ApplicationTheme.Light);
    }

    partial void OnIsTurkishChanged(bool value)
    {
        Loc.I.Language = value ? "tr" : "en";
    }

    [RelayCommand]
    private void ToggleTheme() => IsDarkTheme = !IsDarkTheme;

    [RelayCommand]
    private void ToggleLanguage() => IsTurkish = !IsTurkish;
}
