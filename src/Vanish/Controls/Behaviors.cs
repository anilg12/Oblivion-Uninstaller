using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Vanish.Helpers;

namespace Vanish.Controls;

/// <summary>
/// Fade + slide-up entrance. With <c>Stagger</c> on, items inside an ItemsControl
/// enter one after another (only the first few, so scrolling never animates).
/// Animations finish and release themselves; nothing keeps ticking afterwards.
/// </summary>
public static class Reveal
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled", typeof(bool), typeof(Reveal), new PropertyMetadata(false, OnEnabledChanged));

    public static readonly DependencyProperty DelayProperty = DependencyProperty.RegisterAttached(
        "Delay", typeof(double), typeof(Reveal), new PropertyMetadata(0.0));

    public static readonly DependencyProperty StaggerProperty = DependencyProperty.RegisterAttached(
        "Stagger", typeof(bool), typeof(Reveal), new PropertyMetadata(false));

    private static readonly DependencyProperty DoneProperty = DependencyProperty.RegisterAttached(
        "Done", typeof(bool), typeof(Reveal), new PropertyMetadata(false));

    public static bool GetEnabled(DependencyObject d) => (bool)d.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject d, bool v) => d.SetValue(EnabledProperty, v);
    public static double GetDelay(DependencyObject d) => (double)d.GetValue(DelayProperty);
    public static void SetDelay(DependencyObject d, double v) => d.SetValue(DelayProperty, v);
    public static bool GetStagger(DependencyObject d) => (bool)d.GetValue(StaggerProperty);
    public static void SetStagger(DependencyObject d, bool v) => d.SetValue(StaggerProperty, v);

    /// <summary>Set to false (e.g. by the snapshot runner) to show everything instantly.</summary>
    public static bool AnimationsEnabled { get; set; } = true;

    private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FrameworkElement fe && e.NewValue is true)
            fe.Loaded += OnLoaded;
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        var fe = (FrameworkElement)sender;
        fe.Loaded -= OnLoaded;
        if ((bool)fe.GetValue(DoneProperty)) return;
        fe.SetValue(DoneProperty, true);
        if (!AnimationsEnabled || SystemParameters.ClientAreaAnimation == false) return;

        double delay = GetDelay(fe);
        if (GetStagger(fe))
        {
            int index = IndexInItemsControl(fe);
            if (index < 0 || index > 14) return; // only the first visible rows
            delay += index * 32;
        }

        var tt = new TranslateTransform(0, 8);
        fe.RenderTransform = tt;
        fe.Opacity = 0;

        var begin = TimeSpan.FromMilliseconds(delay);
        var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)) { BeginTime = begin };
        var slide = new DoubleAnimation(8, 0, TimeSpan.FromMilliseconds(280))
        {
            BeginTime = begin,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        slide.Completed += (_, _) =>
        {
            fe.BeginAnimation(UIElement.OpacityProperty, null);
            fe.Opacity = 1;
            fe.RenderTransform = Transform.Identity;
        };
        fe.BeginAnimation(UIElement.OpacityProperty, fade);
        tt.BeginAnimation(TranslateTransform.YProperty, slide);
    }

    private static int IndexInItemsControl(FrameworkElement fe)
    {
        DependencyObject? cur = fe;
        while (cur is not null)
        {
            var parent = VisualTreeHelper.GetParent(cur);
            if (parent is ItemsControl ic)
            {
                var container = ic.ContainerFromElement(fe);
                return container is null ? -1 : ic.ItemContainerGenerator.IndexFromContainer(container);
            }
            if (parent is ItemsPresenter or Panel or ContentPresenter or Border or Grid)
            {
                cur = parent;
                continue;
            }
            cur = parent;
        }
        return -1;
    }
}

/// <summary>
/// Loads an icon (exe/dll "path,index" or an image file) on a background thread and
/// fades it in. Keeps the UI thread free while long app lists scroll.
/// </summary>
public static class IconLoader
{
    public static readonly DependencyProperty PathProperty = DependencyProperty.RegisterAttached(
        "Path", typeof(string), typeof(IconLoader), new PropertyMetadata(null, OnPathChanged));

    public static string? GetPath(DependencyObject d) => (string?)d.GetValue(PathProperty);
    public static void SetPath(DependencyObject d, string? v) => d.SetValue(PathProperty, v);

    private static void OnPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Image img) return;
        var path = e.NewValue as string;
        if (string.IsNullOrWhiteSpace(path))
        {
            img.Source = null;
            return;
        }

        if (IconExtractor.TryGetCached(path, out var cached))
        {
            img.BeginAnimation(UIElement.OpacityProperty, null);
            img.Opacity = 1;
            img.Source = cached;
            return;
        }

        img.Source = null;
        _ = LoadAsync(img, path);
    }

    private static async Task LoadAsync(Image img, string path)
    {
        BitmapSource? source;
        try { source = await IconExtractor.LoadAsync(path); }
        catch { source = null; }

        await img.Dispatcher.InvokeAsync(() =>
        {
            if (!string.Equals(GetPath(img), path, StringComparison.OrdinalIgnoreCase)) return;
            img.Source = source;
            if (source is null) return;
            img.Opacity = 0;
            img.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
        }, DispatcherPriority.Background);
    }
}

/// <summary>
/// For a scrollable list nested inside a scrolling page: once the inner list reaches
/// its top/bottom, the mouse wheel scrolls the page instead of getting stuck.
/// </summary>
public static class ScrollBubble
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled", typeof(bool), typeof(ScrollBubble), new PropertyMetadata(false, OnChanged));

    public static bool GetEnabled(DependencyObject d) => (bool)d.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject d, bool v) => d.SetValue(EnabledProperty, v);

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement el) return;
        el.PreviewMouseWheel -= OnWheel;
        if (e.NewValue is true) el.PreviewMouseWheel += OnWheel;
    }

    private static void OnWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        var el = (DependencyObject)sender;
        var sv = el as ScrollViewer ?? FindChild<ScrollViewer>(el);
        if (sv is null) return;
        bool up = e.Delta > 0;
        bool atEdge = sv.ScrollableHeight <= 0 ||
                      (up && sv.VerticalOffset <= 0) ||
                      (!up && sv.VerticalOffset >= sv.ScrollableHeight - 0.5);
        if (!atEdge) return;
        e.Handled = true;
        if (VisualTreeHelper.GetParent(el) is UIElement parent)
        {
            parent.RaiseEvent(new System.Windows.Input.MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = UIElement.MouseWheelEvent,
                Source = sender
            });
        }
    }

    private static T? FindChild<T>(DependencyObject root) where T : DependencyObject
    {
        int n = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < n; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T t) return t;
            if (FindChild<T>(child) is { } found) return found;
        }
        return null;
    }
}

/// <summary>
/// "Live" indicator: a small dot with a soft halo. Deliberately static — an endlessly
/// repeating animation keeps the window repainting, which on variable-refresh displays
/// can make the whole screen flicker while Oblivion has focus.
/// </summary>
public sealed class PulseDot : FrameworkElement
{
    public static readonly DependencyProperty ColorProperty = DependencyProperty.Register(
        nameof(Color), typeof(Color), typeof(PulseDot), new FrameworkPropertyMetadata(Color.FromRgb(0x30, 0xA4, 0x6C), FrameworkPropertyMetadataOptions.AffectsRender));

    public Color Color { get => (Color)GetValue(ColorProperty); set => SetValue(ColorProperty, value); }

    public PulseDot()
    {
        Width = 12;
        Height = 12;
        IsHitTestVisible = false;
    }

    protected override void OnRender(DrawingContext dc)
    {
        var c = Color;
        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        double outer = Math.Min(ActualWidth, ActualHeight) / 2;
        dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(0x38, c.R, c.G, c.B)), null, center, outer, outer);
        dc.DrawEllipse(new SolidColorBrush(c), null, center, outer * 0.5, outer * 0.5);
    }
}

/// <summary>Springy scale-in each time the element becomes visible (result badges, empty states).</summary>
public static class PopIn
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled", typeof(bool), typeof(PopIn), new PropertyMetadata(false, OnChanged));

    public static bool GetEnabled(DependencyObject d) => (bool)d.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject d, bool v) => d.SetValue(EnabledProperty, v);

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement fe) return;
        fe.IsVisibleChanged -= OnVisibleChanged;
        if (e.NewValue is true) fe.IsVisibleChanged += OnVisibleChanged;
    }

    private static void OnVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is not true || !Reveal.AnimationsEnabled) return;
        var fe = (FrameworkElement)sender;
        var scale = new ScaleTransform(0.6, 0.6);
        fe.RenderTransformOrigin = new Point(0.5, 0.5);
        fe.RenderTransform = scale;
        var ease = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.45 };
        var grow = new DoubleAnimation(0.6, 1, TimeSpan.FromMilliseconds(460)) { EasingFunction = ease };
        grow.Completed += (_, _) => fe.RenderTransform = Transform.Identity;
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.6, 1, TimeSpan.FromMilliseconds(460)) { EasingFunction = ease });
        fe.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)));
    }
}

/// <summary>Short fade + slide-down each time the element becomes visible (expanding sections).</summary>
public static class SlideIn
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled", typeof(bool), typeof(SlideIn), new PropertyMetadata(false, OnChanged));

    public static bool GetEnabled(DependencyObject d) => (bool)d.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject d, bool v) => d.SetValue(EnabledProperty, v);

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement fe) return;
        fe.IsVisibleChanged -= OnVisibleChanged;
        if (e.NewValue is true) fe.IsVisibleChanged += OnVisibleChanged;
    }

    private static void OnVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is not true || !Reveal.AnimationsEnabled) return;
        var fe = (FrameworkElement)sender;
        var tt = new TranslateTransform(0, -8);
        fe.RenderTransform = tt;
        var slide = new DoubleAnimation(-8, 0, TimeSpan.FromMilliseconds(260)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        slide.Completed += (_, _) => fe.RenderTransform = Transform.Identity;
        tt.BeginAnimation(TranslateTransform.YProperty, slide);
        fe.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)));
    }
}
