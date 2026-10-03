namespace Vanish.Services;

/// <summary>
/// Lightweight in-app navigation. Pages (e.g. the Tools hub) request navigation by
/// tag; the main window subscribes and swaps the active page.
/// </summary>
public sealed class NavigationService
{
    public event Action<string>? Navigated;

    /// <summary>Tag of the page currently shown.</summary>
    public string Current { get; private set; } = "Dashboard";

    public void Navigate(string tag) => Navigated?.Invoke(tag);

    /// <summary>Called by the main window after it switched pages.</summary>
    internal void SetCurrent(string tag) => Current = tag;
}

/// <summary>Implemented by page view models that want to know when their page is shown/hidden.</summary>
public interface IPageAware
{
    void OnShown();
    void OnHidden() { }
}
