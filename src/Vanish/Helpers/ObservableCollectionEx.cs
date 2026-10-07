using System.Collections.ObjectModel;

namespace Vanish.Helpers;

// ObservableCollection with a Reset() that swaps all items with a single notification
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
