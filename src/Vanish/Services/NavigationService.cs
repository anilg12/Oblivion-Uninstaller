namespace Vanish.Services;

/// <summary>
/// Lightweight in-app navigation. Pages (e.g. the Tools hub) request navigation by
/// tag; the main window subscribes and swaps the active page.
/// </summary>
public sealed class NavigationService
{
    public event Action<string>? Navigated;

    public void Navigate(string tag) => Navigated?.Invoke(tag);
}
