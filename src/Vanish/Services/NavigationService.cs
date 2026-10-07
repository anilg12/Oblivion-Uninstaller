namespace Vanish.Services;

// simple in-app navigation. pages (e.g. the tools hub) ask by tag, the main window listens and swaps the page
public sealed class NavigationService
{
    public event Action<string>? Navigated;

    // tag of the page currently shown
    public string Current { get; private set; } = "Dashboard";

    public void Navigate(string tag) => Navigated?.Invoke(tag);

    // called by the main window after it switched pages
    internal void SetCurrent(string tag) => Current = tag;
}

// implemented by page view models that want to know when their page is shown/hidden
public interface IPageAware
{
    void OnShown();
    void OnHidden() { }
}
