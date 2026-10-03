using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Vanish.Controls;
using Vanish.Helpers;
using Wpf.Ui.Controls;
using TextBlock = System.Windows.Controls.TextBlock;

namespace Vanish.Services;

public enum DialogTone { Normal, Warning, Danger }

/// <summary>
/// In-window dialogs (confirmations, About) drawn on a dimmed overlay inside the main
/// window, so they match the app instead of the grey Win32 MessageBox.
/// </summary>
public sealed class DialogService
{
    private Grid? _root;
    private Border? _backdrop;
    private ContentControl? _host;
    private TaskCompletionSource<bool>? _tcs;
    private Func<bool>? _onEnter;
    private bool _dismissOnBackdrop;

    public bool IsOpen => _root is { Visibility: Visibility.Visible };

    /// <summary>Called by the main window once its overlay elements exist.</summary>
    public void Attach(Window window, Grid root, Border backdrop, ContentControl host)
    {
        _root = root;
        _backdrop = backdrop;
        _host = host;
        backdrop.MouseLeftButtonDown += (_, _) => { if (_dismissOnBackdrop) Close(false); };
        window.PreviewKeyDown += (_, e) =>
        {
            if (!IsOpen) return;
            if (e.Key == Key.Escape) { Close(false); e.Handled = true; }
            else if (e.Key == Key.Enter && _onEnter is not null) { _onEnter(); e.Handled = true; }
        };
    }

    /// <summary>Shows a confirmation and returns true when the user confirms.</summary>
    public Task<bool> ConfirmAsync(string title, string message, string confirmText,
        DialogTone tone = DialogTone.Normal, IEnumerable<string>? details = null)
    {
        var card = new Border { Style = (Style)Application.Current.FindResource("OB.Dialog"), Width = 460 };
        var stack = new StackPanel();
        card.Child = stack;

        var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 14) };
        var badge = new GradientBadge { Size = 42, Margin = new Thickness(0, 0, 14, 0) };
        switch (tone)
        {
            case DialogTone.Danger:
                badge.Symbol = SymbolRegular.Delete24; badge.From = Color.FromRgb(0xFF, 0x6B, 0x6B); badge.To = Color.FromRgb(0xE5, 0x48, 0x4D); break;
            case DialogTone.Warning:
                badge.Symbol = SymbolRegular.Warning24; badge.From = Color.FromRgb(0xF5, 0xA5, 0x24); badge.To = Color.FromRgb(0xFF, 0x7A, 0x45); break;
            default:
                badge.Symbol = SymbolRegular.QuestionCircle24; break;
        }
        head.Children.Add(badge);
        head.Children.Add(new TextBlock
        {
            Text = title,
            Style = (Style)Application.Current.FindResource("OB.Section"),
            FontSize = 17,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 340,
            VerticalAlignment = VerticalAlignment.Center
        });
        stack.Children.Add(head);

        stack.Children.Add(new TextBlock
        {
            Text = message,
            Style = (Style)Application.Current.FindResource("OB.Subtitle"),
            FontSize = 13,
            Margin = new Thickness(0)
        });

        var list = details?.ToList();
        if (list is { Count: > 0 })
        {
            var items = new StackPanel();
            foreach (var d in list.Take(200))
                items.Children.Add(new TextBlock
                {
                    Text = d,
                    Style = (Style)Application.Current.FindResource("OB.Caption"),
                    Margin = new Thickness(0, 2, 0, 2)
                });
            if (list.Count > 200)
                items.Children.Add(new TextBlock { Text = $"+ {list.Count - 200}", Style = (Style)Application.Current.FindResource("OB.FaintText") });
            stack.Children.Add(new Border
            {
                Margin = new Thickness(0, 12, 0, 0),
                Padding = new Thickness(12, 8, 12, 8),
                CornerRadius = new CornerRadius(10),
                Background = (Brush)Application.Current.FindResource("OB.Card"),
                Child = new ScrollViewer { MaxHeight = 170, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = items }
            });
        }

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
        var cancel = new System.Windows.Controls.Button
        {
            Content = Loc.I["Act_Cancel"],
            Style = (Style)Application.Current.FindResource("OB.Btn"),
            Margin = new Thickness(0, 0, 8, 0),
            MinWidth = 90
        };
        var ok = new System.Windows.Controls.Button
        {
            Content = confirmText,
            Style = (Style)Application.Current.FindResource(tone == DialogTone.Danger ? "OB.BtnDanger" : "OB.BtnPrimary"),
            MinWidth = 110
        };
        cancel.Click += (_, _) => Close(false);
        ok.Click += (_, _) => Close(true);
        buttons.Children.Add(cancel);
        buttons.Children.Add(ok);
        stack.Children.Add(buttons);

        return Open(card, dismissOnBackdrop: true, onEnter: () => { Close(true); return true; });
    }

    /// <summary>Shows arbitrary content (e.g. About); completes when it is closed.</summary>
    public Task<bool> ShowAsync(FrameworkElement content) =>
        Open(content, dismissOnBackdrop: true, onEnter: null);

    private Task<bool> Open(FrameworkElement content, bool dismissOnBackdrop, Func<bool>? onEnter)
    {
        if (_root is null || _host is null || _backdrop is null)
            return Task.FromResult(false);

        // Only one dialog at a time: a new one cancels the previous.
        _tcs?.TrySetResult(false);
        _tcs = new TaskCompletionSource<bool>();
        _dismissOnBackdrop = dismissOnBackdrop;
        _onEnter = onEnter;

        _host.Content = content;
        _root.Visibility = Visibility.Visible;

        var scale = new ScaleTransform(0.94, 0.94);
        _host.RenderTransformOrigin = new Point(0.5, 0.5);
        _host.RenderTransform = scale;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        _backdrop.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(170)));
        _host.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)));
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.94, 1, TimeSpan.FromMilliseconds(240)) { EasingFunction = ease });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.94, 1, TimeSpan.FromMilliseconds(240)) { EasingFunction = ease });
        content.Focus();
        return _tcs.Task;
    }

    public void Close(bool result)
    {
        if (_root is null || _host is null || _backdrop is null || !IsOpen) return;
        var tcs = _tcs;
        _tcs = null;
        _onEnter = null;

        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(140));
        fade.Completed += (_, _) =>
        {
            if (_tcs is not null) return; // a new dialog opened meanwhile
            _root.Visibility = Visibility.Collapsed;
            _host.Content = null;
            _backdrop.BeginAnimation(UIElement.OpacityProperty, null);
            _host.BeginAnimation(UIElement.OpacityProperty, null);
        };
        _backdrop.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(140)));
        _host.BeginAnimation(UIElement.OpacityProperty, fade);
        tcs?.TrySetResult(result);
    }
}
