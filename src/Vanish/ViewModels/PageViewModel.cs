using CommunityToolkit.Mvvm.ComponentModel;
using Vanish.Helpers;
using Vanish.Services;

namespace Vanish.ViewModels;

// base for page view models: shared services, page show/hide hooks and text refresh
public abstract class PageViewModel : ObservableObject, IPageAware, ILocalizable
{
    protected static DialogService Dialogs => Ioc.Resolve<DialogService>();
    protected static ToastService Toast => Ioc.Resolve<ToastService>();
    protected static OperationLogService Log => Ioc.Resolve<OperationLogService>();
    protected static NavigationService Navigation => Ioc.Resolve<NavigationService>();
    protected static AppSettings Settings => Ioc.Resolve<SettingsService>().Current;

    // true while the page is on screen
    public bool IsShown { get; private set; }

    public virtual void OnShown() => IsShown = true;

    public virtual void OnHidden() => IsShown = false;

    // re-raise computed, localized properties after a language switch
    public virtual void RefreshTexts() { }

    protected static string T(string key) => Loc.I[key];

    protected static string F(string key, params object?[] args) => string.Format(Loc.I[key], args);
}
