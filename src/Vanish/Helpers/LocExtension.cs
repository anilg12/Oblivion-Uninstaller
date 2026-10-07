using System.Windows.Data;
using System.Windows.Markup;

namespace Vanish.Helpers;

// shorthand: Text="{h:L Nav_Dashboard}" = {Binding [Nav_Dashboard], Source={x:Static h:Loc.I}}
[MarkupExtensionReturnType(typeof(object))]
public sealed class L : MarkupExtension
{
    public L() { }

    public L(string key) => Key = key;

    [ConstructorArgument("key")]
    public string Key { get; set; } = "";

    public override object ProvideValue(IServiceProvider serviceProvider)
        => new Binding($"[{Key}]") { Source = Loc.I, Mode = BindingMode.OneWay }.ProvideValue(serviceProvider);
}
