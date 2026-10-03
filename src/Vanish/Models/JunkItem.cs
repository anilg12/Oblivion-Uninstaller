using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Vanish.Helpers;
using Wpf.Ui.Controls;

namespace Vanish.Models;

/// <summary>How a junk category gets cleaned.</summary>
public enum JunkMode
{
    /// <summary>Regenerable data (temp files, caches): deleted permanently, locked files skipped.</summary>
    Delete,
    /// <summary>Personal files (old installers in Downloads): moved to the Recycle Bin.</summary>
    Recycle,
    /// <summary>The Recycle Bin itself: emptied.</summary>
    EmptyRecycleBin
}

/// <summary>
/// A category of removable junk. Nothing is selected by default; the user expands a
/// category to see exactly which files/folders will be removed and ticks them.
/// </summary>
public sealed partial class JunkCategory : ObservableObject
{
    public required string Id { get; init; }
    public required string TitleKey { get; init; }
    public required string DetailKey { get; init; }
    public string? NoteKey { get; init; }
    public required SymbolRegular Symbol { get; init; }
    public required Color From { get; init; }
    public required Color To { get; init; }
    public required JunkMode Mode { get; init; }

    public string Title => Loc.I[TitleKey];
    public string Detail => Loc.I[DetailKey];
    public string? Note => NoteKey is null ? null : Loc.I[NoteKey];
    public bool HasNote => NoteKey is not null;

    public ObservableCollection<JunkEntry> Items { get; } = new();

    [ObservableProperty] private bool _isExpanded;
    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private bool _isScanned;
    [ObservableProperty] private long _totalBytes;
    [ObservableProperty] private int _totalCount;

    public bool HasItems => Items.Count > 0;
    public bool IsEmpty => IsScanned && Items.Count == 0;

    public long SelectedBytes => Items.Where(i => i.IsSelected).Sum(i => i.SizeBytes);
    public int SelectedCount => Items.Where(i => i.IsSelected).Sum(i => i.Count);

    public string SizeText => IsScanned ? ByteSize.Humanize(TotalBytes) : "—";
    public string CountText => IsScanned
        ? (TotalCount == 0 ? Loc.I["Junk_Nothing"] : string.Format(Loc.I["Junk_ItemsFmt"], TotalCount))
        : (IsScanning ? Loc.I["Junk_Scanning"] : "");
    public string SelectedText => SelectedCount == 0 ? "" : string.Format(Loc.I["Junk_SelectedFmt"], ByteSize.Humanize(SelectedBytes));

    /// <summary>Tri-state: true = all, false = none, null = some.</summary>
    public bool? SelectionState
    {
        get
        {
            if (Items.Count == 0) return false;
            int n = Items.Count(i => i.IsSelected);
            return n == 0 ? false : n == Items.Count ? true : null;
        }
        set
        {
            bool select = value != false;
            _bulk = true;
            foreach (var i in Items) i.IsSelected = select;
            _bulk = false;
            RaiseSelection();
        }
    }

    private bool _bulk;

    internal void OnItemSelectionChanged()
    {
        if (!_bulk) RaiseSelection();
    }

    public event Action? SelectionChanged;

    private void RaiseSelection()
    {
        OnPropertyChanged(nameof(SelectionState));
        OnPropertyChanged(nameof(SelectedBytes));
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(SelectedText));
        SelectionChanged?.Invoke();
    }

    public void SetItems(IReadOnlyList<JunkEntry> items, long totalBytes, int totalCount)
    {
        Items.Clear();
        foreach (var i in items)
        {
            i.Category = this;
            Items.Add(i);
        }
        TotalBytes = totalBytes;
        TotalCount = totalCount;
        IsScanned = true;
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(SizeText));
        OnPropertyChanged(nameof(CountText));
        RaiseSelection();
    }

    partial void OnIsScanningChanged(bool value) => OnPropertyChanged(nameof(CountText));

    public void RefreshTexts()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Detail));
        OnPropertyChanged(nameof(Note));
        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(SelectedText));
    }
}

/// <summary>One removable file/folder (or a group of many small ones) inside a category.</summary>
public sealed partial class JunkEntry : ObservableObject
{
    public required string Name { get; init; }
    public required string Location { get; init; }

    /// <summary>Paths removed when this entry is cleaned (one, or many for a group).</summary>
    public required IReadOnlyList<string> Paths { get; init; }

    public long SizeBytes { get; init; }

    /// <summary>Number of files this entry stands for (shown in totals).</summary>
    public int Count { get; init; } = 1;

    public DateTime? Modified { get; init; }
    public bool IsFolder { get; init; }
    public bool IsGroup { get; init; }

    internal JunkCategory? Category { get; set; }

    [ObservableProperty] private bool _isSelected;

    partial void OnIsSelectedChanged(bool value) => Category?.OnItemSelectionChanged();

    public string SizeText => ByteSize.Humanize(SizeBytes);
    public string DateText => Modified?.ToString("dd.MM.yyyy") ?? "";
    public SymbolRegular Symbol => IsGroup ? SymbolRegular.BoxMultiple24 : IsFolder ? SymbolRegular.Folder24 : SymbolRegular.Document24;
}
