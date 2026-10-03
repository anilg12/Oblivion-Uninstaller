using System.IO;
using System.Windows.Media;
using CommunityToolkit.Mvvm.Input;
using Vanish.Controls;
using Vanish.Helpers;
using Vanish.Services;
using Vanish.Views;

namespace Vanish.ViewModels.Pages;

public sealed partial class SettingsViewModel : PageViewModel
{
    private readonly SettingsService _settings;

    public SettingsViewModel(SettingsService settings)
    {
        _settings = settings;
        _settings.Current.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppSettings.ReduceAnimations)) ApplyAnimationSetting(_settings.Current);
        };
    }

    public AppSettings Current => _settings.Current;

    public string VersionText => "v" + AppInfo.Version;

    /// <summary>Entrance animations are off when the user asks for it or there is no GPU acceleration.</summary>
    public static void ApplyAnimationSetting(AppSettings s) =>
        Reveal.AnimationsEnabled = !s.ReduceAnimations && (RenderCapability.Tier >> 16) > 0;

    [RelayCommand]
    private void SetLanguage(string code)
    {
        if (Loc.I.Language == code) return;
        Loc.I.Language = code;
        Current.Language = code;
        Ioc.Resolve<MainWindowViewModel>().RefreshAllTexts();
    }

    [RelayCommand]
    private void OpenDataFolder()
    {
        Directory.CreateDirectory(SettingsService.DataDir);
        Shell.OpenFolder(SettingsService.DataDir);
    }

    [RelayCommand]
    private Task ShowAboutAsync() => Dialogs.ShowAsync(new AboutView());

    [RelayCommand]
    private void OpenGitHub() => Shell.OpenUrl(AppInfo.Repository);
}
