using CommunityToolkit.Mvvm.ComponentModel;
using Vanish.Helpers;
using Vanish.Services;

namespace Vanish.ViewModels;

/// <summary>Base for page view models: shared services, page show/hide hooks and text refresh.</summary>
public abstract class PageViewModel : ObservableObject, IPageAware, ILocalizable
{
    protected static DialogService Dialogs => Ioc.Resolve<DialogService>();
    protected static ToastService Toast => Ioc.Resolve<ToastService>();
    protected static OperationLogService Log => Ioc.Resolve<OperationLogService>();
    protected static NavigationService Navigation => Ioc.Resolve<NavigationService>();
    protected static AppSettings Settings => Ioc.Resolve<SettingsService>().Current;

    /// <summary>True while the page is on screen.</summary>
    public bool IsShown { get; private set; }

    public virtual void OnShown() => IsShown = true;

    public virtual void OnHidden() => IsShown = false;

    /// <summary>Re-raise computed, localized properties after a language switch.</summary>
    public virtual void RefreshTexts() { }

    protected static string T(string key) => Loc.I[key];

    protected static string F(string key, params object?[] args) => string.Format(Loc.I[key], args);
}
