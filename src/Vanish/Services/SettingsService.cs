using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Vanish.Services;

// user preferences, persisted to %LOCALAPPDATA%\Oblivion\settings.json
public sealed partial class AppSettings : ObservableObject
{
    // "dark", "light" or "system"
    [ObservableProperty] private string _theme = "dark";
    // "tr" or "en"
    [ObservableProperty] private string _language = "tr";
    [ObservableProperty] private bool _createRestorePoint = true;
    [ObservableProperty] private bool _silentUninstall;
    [ObservableProperty] private bool _useRecycleBin = true;
    [ObservableProperty] private bool _confirmBeforeUninstall = true;
    [ObservableProperty] private bool _showLivePanel = true;
    [ObservableProperty] private bool _reduceAnimations;
}

[JsonSerializable(typeof(AppSettingsDto))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext { }

internal sealed class AppSettingsDto
{
    public string Theme { get; set; } = "dark";
    public string Language { get; set; } = "tr";
    public bool CreateRestorePoint { get; set; } = true;
    public bool SilentUninstall { get; set; }
    public bool UseRecycleBin { get; set; } = true;
    public bool ConfirmBeforeUninstall { get; set; } = true;
    public bool ShowLivePanel { get; set; } = true;
    public bool ReduceAnimations { get; set; }
}

public sealed class SettingsService
{
    public static readonly string DataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Oblivion");

    private static readonly string FilePath = Path.Combine(DataDir, "settings.json");

    public AppSettings Current { get; }

    public SettingsService()
    {
        Current = Load();
        Current.PropertyChanged += (_, _) => Save();
    }

    private static AppSettings Load()
    {
        var s = new AppSettings();
        try
        {
            if (File.Exists(FilePath))
            {
                var dto = JsonSerializer.Deserialize(File.ReadAllText(FilePath), SettingsJsonContext.Default.AppSettingsDto);
                if (dto is not null)
                {
                    s.Theme = dto.Theme is "dark" or "light" or "system" ? dto.Theme : "dark";
                    s.Language = dto.Language is "tr" or "en" ? dto.Language : "tr";
                    s.CreateRestorePoint = dto.CreateRestorePoint;
                    s.SilentUninstall = dto.SilentUninstall;
                    s.UseRecycleBin = dto.UseRecycleBin;
                    s.ConfirmBeforeUninstall = dto.ConfirmBeforeUninstall;
                    s.ShowLivePanel = dto.ShowLivePanel;
                    s.ReduceAnimations = dto.ReduceAnimations;
                }
            }
        }
        catch { /* corrupt settings -> defaults */ }
        return s;
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(DataDir);
            var dto = new AppSettingsDto
            {
                Theme = Current.Theme,
                Language = Current.Language,
                CreateRestorePoint = Current.CreateRestorePoint,
                SilentUninstall = Current.SilentUninstall,
                UseRecycleBin = Current.UseRecycleBin,
                ConfirmBeforeUninstall = Current.ConfirmBeforeUninstall,
                ShowLivePanel = Current.ShowLivePanel,
                ReduceAnimations = Current.ReduceAnimations
            };
            File.WriteAllText(FilePath, JsonSerializer.Serialize(dto, SettingsJsonContext.Default.AppSettingsDto));
        }
        catch { /* best effort */ }
    }
}
