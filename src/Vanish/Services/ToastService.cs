using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Wpf.Ui.Controls;

namespace Vanish.Services;

public enum ToastKind { Info, Success, Warning, Error }

// small toast that slides up at the bottom and fades out ("3 items removed · 1.2 GB freed").
// used instead of status text / message boxes for results that need no answer
public sealed class ToastService
{
    private Border? _host;
    private System.Windows.Controls.TextBlock? _text;
    private SymbolIcon? _icon;
    private Border? _badge;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(3.4) };

    public ToastService() => _timer.Tick += (_, _) => Hide();

    // last message shown (used by the self-test report)
    public string? LastMessage { get; private set; }

    public void Attach(Border host, System.Windows.Controls.TextBlock text, SymbolIcon icon, Border badge)
    {
        _host = host;
        _text = text;
        _icon = icon;
        _badge = badge;
        host.MouseLeftButtonUp += (_, _) => Hide();
    }

    public void Show(string message, ToastKind kind = ToastKind.Info)
    {
        LastMessage = message;
        if (_host is null || _text is null || _icon is null || _badge is null) return;
        if (!_host.Dispatcher.CheckAccess())
        {
            _host.Dispatcher.InvokeAsync(() => Show(message, kind));
            return;
        }

        _text.Text = message;
        (_icon.Symbol, var color) = kind switch
        {
            ToastKind.Success => (SymbolRegular.CheckmarkCircle24, Color.FromRgb(0x22, 0xC5, 0x5E)),
            ToastKind.Warning => (SymbolRegular.Warning24, Color.FromRgb(0xF5, 0xA5, 0x24)),
            ToastKind.Error => (SymbolRegular.ErrorCircle24, Color.FromRgb(0xE5, 0x48, 0x4D)),
            _ => (SymbolRegular.Info24, Color.FromRgb(0x7C, 0x6C, 0xF6))
        };
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        _badge.Background = brush;

        _timer.Stop();
        _host.Visibility = Visibility.Visible;
        var slide = new TranslateTransform(0, 18);
        _host.RenderTransform = slide;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        _host.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)));
        slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(18, 0, TimeSpan.FromMilliseconds(320)) { EasingFunction = ease });
        _timer.Interval = TimeSpan.FromSeconds(kind is ToastKind.Error or ToastKind.Warning ? 5 : 3.4);
        _timer.Start();
    }

    private void Hide()
    {
        _timer.Stop();
        if (_host is null || _host.Visibility != Visibility.Visible) return;
        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(220));
        fade.Completed += (_, _) =>
        {
            if (_timer.IsEnabled) return; // a new toast arrived meanwhile
            _host.Visibility = Visibility.Collapsed;
            _host.BeginAnimation(UIElement.OpacityProperty, null);
        };
        _host.BeginAnimation(UIElement.OpacityProperty, fade);
    }
}
