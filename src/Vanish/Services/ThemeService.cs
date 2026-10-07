using System.Windows;
using System.Windows.Media;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace Vanish.Services;

// applies the Oblivion palette (dark / light / follow Windows) plus WPF-UI's theme
public sealed class ThemeService
{
    private const string PaletteMarker = "/Themes/Palette.";
    private readonly SettingsService _settings;

    public ThemeService(SettingsService settings)
    {
        _settings = settings;
        _settings.Current.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppSettings.Theme)) Apply();
        };
    }

    public bool IsDark { get; private set; } = true;

    public event Action? Changed;

    public void Apply()
    {
        bool dark = _settings.Current.Theme switch
        {
            "light" => false,
            "system" => ApplicationThemeManager.GetSystemTheme() is SystemTheme.Dark or SystemTheme.Glow or SystemTheme.CapturedMotion
                        or SystemTheme.HCBlack or SystemTheme.HC1 or SystemTheme.HC2,
            _ => true
        };
        IsDark = dark;

        var dicts = Application.Current.Resources.MergedDictionaries;
        var palette = new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/Oblivion;component/Themes/Palette.{(dark ? "Dark" : "Light")}.xaml", UriKind.Absolute)
        };
        int index = -1;
        for (int i = 0; i < dicts.Count; i++)
        {
            if (dicts[i].Source?.OriginalString.Contains(PaletteMarker, StringComparison.OrdinalIgnoreCase) == true)
            {
                index = i;
                break;
            }
        }
        if (index >= 0) dicts[index] = palette; else dicts.Add(palette);

        var theme = dark ? ApplicationTheme.Dark : ApplicationTheme.Light;
        ApplicationThemeManager.Apply(theme, WindowBackdropType.None, updateAccent: false);
        ApplicationAccentColorManager.Apply(Color.FromRgb(0x4F, 0x6B, 0xED), theme);
        Changed?.Invoke();
    }
}
