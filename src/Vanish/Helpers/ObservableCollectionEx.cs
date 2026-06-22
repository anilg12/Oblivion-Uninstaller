using System.Collections.ObjectModel;

namespace Vanish.Helpers;

/// <summary>
/// An <see cref="ObservableCollection{T}"/> with a bulk <see cref="Reset"/> that
/// replaces all items, raising a single reset notification.
/// </summary>
public sealed class ObservableCollectionEx<T> : ObservableCollection<T>
{
    public ObservableCollectionEx() { }
    public ObservableCollectionEx(IEnumerable<T> items) : base(items) { }

    public void Reset(IEnumerable<T> items)
    {
        Items.Clear();
        foreach (var item in items)
            Items.Add(item);

        OnCollectionChanged(new(System.Collections.Specialized.NotifyCollectionChangedAction.Reset));
        OnPropertyChanged(new(nameof(Count)));
        OnPropertyChanged(new("Item[]"));
    }
}
