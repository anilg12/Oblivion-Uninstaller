using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vanish.Helpers;
using Wpf.Ui.Controls;

namespace Vanish.ViewModels.Pages;

// one tile on the Tools hub
public sealed class ToolTile
{
    public required string Tag { get; init; }
    public required string TitleKey { get; init; }
    public required string TextKey { get; init; }
    public required SymbolRegular Symbol { get; init; }
    public required Color From { get; init; }
    public required Color To { get; init; }
    public bool IsNew { get; init; }

    public string Title => Loc.I[TitleKey];
    public string Text => Loc.I[TextKey];
}

public sealed class ToolGroup
{
    public required string TitleKey { get; init; }
    public required IReadOnlyList<ToolTile> Tiles { get; init; }

    public string Title => Loc.I[TitleKey];
}

public sealed partial class ToolsViewModel : PageViewModel
{
    private static Color C(uint rgb) => Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);

    [ObservableProperty] private IReadOnlyList<ToolGroup> _groups = Build();

    public override void RefreshTexts() => Groups = Build();

    private static IReadOnlyList<ToolGroup> Build() => new List<ToolGroup>
    {
        new()
        {
            TitleKey = "Tools_GroupClean",
            Tiles = new List<ToolTile>
            {
                new() { Tag = "Junk", TitleKey = "Tool_Junk", TextKey = "Tool_Junk_D", Symbol = SymbolRegular.Broom24, From = C(0x3A8DFF), To = C(0x6F5BFF) },
                new() { Tag = "LargeFiles", TitleKey = "Tool_LargeFiles", TextKey = "Tool_LargeFiles_D", Symbol = SymbolRegular.DocumentSearch24, From = C(0xF5A524), To = C(0xFF7A45), IsNew = true },
                new() { Tag = "History", TitleKey = "Tool_History", TextKey = "Tool_History_D", Symbol = SymbolRegular.EyeOff24, From = C(0x14B8A6), To = C(0x22C55E), IsNew = true },
                new() { Tag = "Evidence", TitleKey = "Tool_Evidence", TextKey = "Tool_Evidence_D", Symbol = SymbolRegular.EraserTool24, From = C(0xEC4899), To = C(0x8B5CF6), IsNew = true },
            }
        },
        new()
        {
            TitleKey = "Tools_GroupSystem",
            Tiles = new List<ToolTile>
            {
                new() { Tag = "Startup", TitleKey = "Tool_Startup", TextKey = "Tool_Startup_D", Symbol = SymbolRegular.Power24, From = C(0x6E5BFF), To = C(0xB45BFF) },
                new() { Tag = "SystemMonitor", TitleKey = "Tool_SystemMonitor", TextKey = "Tool_SystemMonitor_D", Symbol = SymbolRegular.Pulse24, From = C(0x18C29C), To = C(0x2E8BFF), IsNew = true },
                new() { Tag = "Monitored", TitleKey = "Tool_Monitor", TextKey = "Tool_Monitor_D", Symbol = SymbolRegular.EyeTracking24, From = C(0x3A8DFF), To = C(0x18C29C) },
                new() { Tag = "Backups", TitleKey = "Tool_Backups", TextKey = "Tool_Backups_D", Symbol = SymbolRegular.ArchiveArrowBack24, From = C(0x8B5CF6), To = C(0x3A8DFF), IsNew = true },
            }
        },
        new()
        {
            TitleKey = "Tools_GroupPrivacy",
            Tiles = new List<ToolTile>
            {
                new() { Tag = "Shredder", TitleKey = "Tool_Shredder", TextKey = "Tool_Shredder_D", Symbol = SymbolRegular.Fire24, From = C(0xFF6B6B), To = C(0xE5484D), IsNew = true },
                new() { Tag = "Hunter", TitleKey = "Tool_Hunter", TextKey = "Tool_Hunter_D", Symbol = SymbolRegular.Target24, From = C(0xFF9F45), To = C(0xFF6B6B) },
                new() { Tag = "BrowserExt", TitleKey = "Tool_BrowserExt", TextKey = "Tool_BrowserExt_D", Symbol = SymbolRegular.PuzzlePiece24, From = C(0x2E8BFF), To = C(0x6E5BFF) },
                new() { Tag = "WindowsApps", TitleKey = "Tool_WindowsApps", TextKey = "Tool_WindowsApps_D", Symbol = SymbolRegular.StoreMicrosoft24, From = C(0x22C55E), To = C(0x14B8A6) },
            }
        },
    };

    [RelayCommand]
    private void Open(string tag) => Navigation.Navigate(tag);
}
