using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Vanish.Helpers;
using Wpf.Ui.Controls;

namespace Vanish.Controls;

/// <summary>Arc geometry helper (angles in degrees, 0 = 3 o'clock, clockwise).</summary>
internal static class Arcs
{
    public static Geometry Make(Point center, double radius, double startDeg, double sweepDeg)
    {
        sweepDeg = Math.Clamp(sweepDeg, 0.01, 359.99);
        double a0 = startDeg * Math.PI / 180, a1 = (startDeg + sweepDeg) * Math.PI / 180;
        var p0 = new Point(center.X + radius * Math.Cos(a0), center.Y + radius * Math.Sin(a0));
        var p1 = new Point(center.X + radius * Math.Cos(a1), center.Y + radius * Math.Sin(a1));
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(p0, false, false);
            ctx.ArcTo(p1, new Size(radius, radius), 0, sweepDeg > 180, SweepDirection.Clockwise, true, true);
        }
        geo.Freeze();
        return geo;
    }
}

/// <summary>The single accent colour used by the hand-drawn controls.</summary>
internal static class Accent
{
    public static readonly Color Color = Color.FromRgb(0x4F, 0x6B, 0xED);
    public static readonly Brush Brush = Frozen(new SolidColorBrush(Color));
    public static readonly Brush Faded = Frozen(new SolidColorBrush(Color.FromArgb(0x33, 0x4F, 0x6B, 0xED)));

    /// <summary>A caller-chosen colour, except the legacy violets which map to the accent.</summary>
    public static Brush For(Color c) =>
        c == Color || (c.R == 0x6E && c.G == 0x5B) || (c.R == 0x7C && c.G == 0x6C) ? Brush : Frozen(new SolidColorBrush(c));

    private static T Frozen<T>(T f) where T : Freezable { f.Freeze(); return f; }
}

/// <summary>Attached "active" flag used by the rail and navigation button styles.</summary>
public static class Nav
{
    public static readonly DependencyProperty IsActiveProperty = DependencyProperty.RegisterAttached(
        "IsActive", typeof(bool), typeof(Nav), new FrameworkPropertyMetadata(false));

    public static bool GetIsActive(DependencyObject d) => (bool)d.GetValue(IsActiveProperty);
    public static void SetIsActive(DependencyObject d, bool value) => d.SetValue(IsActiveProperty, value);
}

/// <summary>Small rounded label tinted with a colour ("Kesin", "App Store", …).</summary>
public sealed class Chip : Border
{
    private readonly System.Windows.Controls.TextBlock _text = new()
    {
        FontSize = 11,
        FontWeight = FontWeights.SemiBold,
        VerticalAlignment = VerticalAlignment.Center,
        TextTrimming = TextTrimming.CharacterEllipsis
    };

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(Chip), new PropertyMetadata("", (d, e) => ((Chip)d)._text.Text = e.NewValue as string ?? ""));

    public static readonly DependencyProperty TintProperty = DependencyProperty.Register(
        nameof(Tint), typeof(Color), typeof(Chip), new PropertyMetadata(Color.FromRgb(0x7C, 0x6C, 0xF6), (d, _) => ((Chip)d).Apply()));

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public Color Tint { get => (Color)GetValue(TintProperty); set => SetValue(TintProperty, value); }

    public Chip()
    {
        CornerRadius = new CornerRadius(6);
        Padding = new Thickness(7, 2, 7, 2.5);
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Center;
        Child = _text;
        Apply();
    }

    private void Apply()
    {
        var c = Tint;
        Background = Freeze(new SolidColorBrush(Color.FromArgb(0x1F, c.R, c.G, c.B)));
        _text.Foreground = Freeze(new SolidColorBrush(c));
    }

    private static T Freeze<T>(T f) where T : Freezable { f.Freeze(); return f; }
}

/// <summary>
/// Quiet rounded-square icon tile (tools, stats, quick actions): a neutral surface with a
/// monochrome icon. <see cref="From"/>/<see cref="To"/> are kept for compatibility; a red
/// <see cref="From"/> marks a destructive tool and tints the icon.
/// </summary>
public sealed class GradientBadge : Border
{
    private readonly SymbolIcon _icon = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };

    public static readonly DependencyProperty SymbolProperty = DependencyProperty.Register(
        nameof(Symbol), typeof(SymbolRegular), typeof(GradientBadge), new PropertyMetadata(SymbolRegular.Apps24, (d, e) => ((GradientBadge)d)._icon.Symbol = (SymbolRegular)e.NewValue));

    public static readonly DependencyProperty FromProperty = DependencyProperty.Register(
        nameof(From), typeof(Color), typeof(GradientBadge), new PropertyMetadata(Color.FromRgb(0x4F, 0x6B, 0xED), (d, _) => ((GradientBadge)d).ApplyColors()));

    public static readonly DependencyProperty ToProperty = DependencyProperty.Register(
        nameof(To), typeof(Color), typeof(GradientBadge), new PropertyMetadata(Color.FromRgb(0x4F, 0x6B, 0xED)));

    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size), typeof(double), typeof(GradientBadge), new PropertyMetadata(48.0, (d, _) => ((GradientBadge)d).ApplySize()));

    public SymbolRegular Symbol { get => (SymbolRegular)GetValue(SymbolProperty); set => SetValue(SymbolProperty, value); }
    public Color From { get => (Color)GetValue(FromProperty); set => SetValue(FromProperty, value); }
    public Color To { get => (Color)GetValue(ToProperty); set => SetValue(ToProperty, value); }
    public double Size { get => (double)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    public GradientBadge()
    {
        Child = _icon;
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Center;
        SnapsToDevicePixels = true;
        BorderThickness = new Thickness(1);
        SetResourceReference(BackgroundProperty, "OB.CardStrong");
        SetResourceReference(BorderBrushProperty, "OB.Stroke");
        ApplySize();
        ApplyColors();
    }

    private void ApplySize()
    {
        var s = Size;
        Width = s;
        Height = s;
        CornerRadius = new CornerRadius(Math.Round(s * 0.26));
        _icon.FontSize = Math.Round(s * 0.44);
    }

    private void ApplyColors()
    {
        var c = From;
        bool destructive = c.R > 0xD0 && c.G < 0x80 && c.B < 0x80;
        if (destructive) _icon.SetResourceReference(SymbolIcon.ForegroundProperty, "OB.Danger");
        else _icon.SetResourceReference(SymbolIcon.ForegroundProperty, "OB.Text");
    }
}

/// <summary>
/// Indeterminate spinner that only animates while visible. (The WPF-UI ProgressRing
/// keeps an endless storyboard running even when hidden, which slowly piles up and
/// makes every other animation stutter.)
/// </summary>
public sealed class Spinner : FrameworkElement
{
    private readonly RotateTransform _rotate = new();
    private bool _running;

    public static readonly DependencyProperty ThicknessProperty = DependencyProperty.Register(
        nameof(Thickness), typeof(double), typeof(Spinner), new FrameworkPropertyMetadata(3.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Thickness { get => (double)GetValue(ThicknessProperty); set => SetValue(ThicknessProperty, value); }

    public Spinner()
    {
        Width = 22;
        Height = 22;
        RenderTransformOrigin = new Point(0.5, 0.5);
        RenderTransform = _rotate;
        IsHitTestVisible = false;
        Loaded += (_, _) => Sync();
        Unloaded += (_, _) => Stop();
        IsVisibleChanged += (_, _) => Sync();
    }

    private void Sync()
    {
        if (IsVisible && IsLoaded) Start(); else Stop();
    }

    private void Start()
    {
        if (_running) return;
        _running = true;
        var anim = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(0.9)) { RepeatBehavior = RepeatBehavior.Forever };
        Timeline.SetDesiredFrameRate(anim, 60);
        _rotate.BeginAnimation(RotateTransform.AngleProperty, anim);
    }

    private void Stop()
    {
        if (!_running) return;
        _running = false;
        _rotate.BeginAnimation(RotateTransform.AngleProperty, null);
    }

    private static readonly Brush Arc = Accent.Brush;

    protected override void OnRender(DrawingContext dc)
    {
        double size = Math.Min(ActualWidth, ActualHeight);
        if (size <= 0) return;
        double t = Thickness;
        double r = (size - t) / 2;
        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        var track = new Pen(Accent.Faded, t);
        dc.DrawEllipse(null, track, center, r, r);
        var pen = new Pen(Arc, t) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        dc.DrawGeometry(null, pen, Arcs.Make(center, r, -90, 110));
    }
}

/// <summary>
/// Circular gauge (0..1). The first value eases in; live updates after that are drawn directly,
/// so a once-a-second sample costs one frame instead of a continuous animation.
/// </summary>
public sealed class RingGauge : FrameworkElement
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(RingGauge), new PropertyMetadata(0.0, OnValueChanged));

    private static readonly DependencyProperty ShownProperty = DependencyProperty.Register(
        "Shown", typeof(double), typeof(RingGauge), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ThicknessProperty = DependencyProperty.Register(
        nameof(Thickness), typeof(double), typeof(RingGauge), new FrameworkPropertyMetadata(8.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FromProperty = DependencyProperty.Register(
        nameof(From), typeof(Color), typeof(RingGauge), new FrameworkPropertyMetadata(Color.FromRgb(0x4F, 0x6B, 0xED), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ToProperty = DependencyProperty.Register(
        nameof(To), typeof(Color), typeof(RingGauge), new FrameworkPropertyMetadata(Color.FromRgb(0x4F, 0x6B, 0xED), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TrackProperty = DependencyProperty.Register(
        nameof(Track), typeof(Brush), typeof(RingGauge), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public double Thickness { get => (double)GetValue(ThicknessProperty); set => SetValue(ThicknessProperty, value); }
    public Color From { get => (Color)GetValue(FromProperty); set => SetValue(FromProperty, value); }
    public Color To { get => (Color)GetValue(ToProperty); set => SetValue(ToProperty, value); }
    public Brush? Track { get => (Brush?)GetValue(TrackProperty); set => SetValue(TrackProperty, value); }

    private bool _revealed;

    public RingGauge() => SetResourceReference(TrackProperty, "OB.Track");

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var g = (RingGauge)d;
        double target = (double)e.NewValue;
        target = double.IsNaN(target) ? 0 : Math.Clamp(target, 0, 1);
        if (g._revealed || !Reveal.AnimationsEnabled)
        {
            g.BeginAnimation(ShownProperty, null);
            g.SetValue(ShownProperty, target);
            return;
        }
        g._revealed = true;
        var anim = new DoubleAnimation(target, TimeSpan.FromMilliseconds(500))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        g.BeginAnimation(ShownProperty, anim, HandoffBehavior.SnapshotAndReplace);
    }

    protected override void OnRender(DrawingContext dc)
    {
        double size = Math.Min(ActualWidth, ActualHeight);
        if (size <= 0) return;
        double t = Thickness;
        double r = (size - t) / 2;
        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        var track = Track ?? new SolidColorBrush(Color.FromArgb(0x26, 0x80, 0x80, 0xA0));
        dc.DrawEllipse(null, new Pen(track, t), center, r, r);

        double v = Math.Clamp((double)GetValue(ShownProperty), 0, 1);
        if (v <= 0.002) return;
        var pen = new Pen(Accent.For(From), t) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        if (v >= 0.999)
            dc.DrawEllipse(null, pen, center, r, r);
        else
            dc.DrawGeometry(null, pen, Arcs.Make(center, r, -90, 360 * v));
    }
}

/// <summary>Tiny area chart for a rolling history of values.</summary>
public sealed class Sparkline : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values), typeof(IReadOnlyList<double>), typeof(Sparkline), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(double), typeof(Sparkline), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ColorProperty = DependencyProperty.Register(
        nameof(Color), typeof(Color), typeof(Sparkline), new FrameworkPropertyMetadata(Color.FromRgb(0x4F, 0x6B, 0xED), FrameworkPropertyMetadataOptions.AffectsRender));

    public IReadOnlyList<double>? Values { get => (IReadOnlyList<double>?)GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }

    /// <summary>Fixed maximum (e.g. 100 for percentages); 0 = auto scale.</summary>
    public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public Color Color { get => (Color)GetValue(ColorProperty); set => SetValue(ColorProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        var values = Values;
        double w = ActualWidth, h = ActualHeight;
        if (values is null || values.Count < 2 || w <= 0 || h <= 0) return;

        double max = Maximum > 0 ? Maximum : Math.Max(values.Max(), 1e-9) * 1.15;
        double step = w / (values.Count - 1);
        var line = new StreamGeometry();
        var area = new StreamGeometry();
        using (var lc = line.Open())
        using (var ac = area.Open())
        {
            for (int i = 0; i < values.Count; i++)
            {
                double x = i * step;
                double y = h - Math.Clamp(values[i] / max, 0, 1) * (h - 2) - 1;
                if (i == 0)
                {
                    lc.BeginFigure(new Point(x, y), false, false);
                    ac.BeginFigure(new Point(0, h), true, true);
                    ac.LineTo(new Point(x, y), false, false);
                }
                else
                {
                    lc.LineTo(new Point(x, y), true, true);
                    ac.LineTo(new Point(x, y), false, false);
                }
            }
            ac.LineTo(new Point(w, h), false, false);
        }
        line.Freeze();
        area.Freeze();
        var c = Color;
        var fill = new LinearGradientBrush(Color.FromArgb(0x30, c.R, c.G, c.B), Color.FromArgb(0x00, c.R, c.G, c.B), 90);
        dc.DrawGeometry(fill, null, area);
        dc.DrawGeometry(null, new Pen(new SolidColorBrush(c), 1.5) { LineJoin = PenLineJoin.Round }, line);
    }
}

/// <summary>
/// Progress bar (solid accent on a quiet track). Value in 0..1; a negative value shows an indeterminate sweep
/// that only animates while the bar is visible.
/// </summary>
public sealed class GradientBar : FrameworkElement
{
    private static readonly DependencyProperty PhaseProperty = DependencyProperty.Register(
        "Phase", typeof(double), typeof(GradientBar), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(GradientBar), new FrameworkPropertyMetadata(-1.0, FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => ((GradientBar)d).Sync()));

    public static readonly DependencyProperty TrackProperty = DependencyProperty.Register(
        nameof(Track), typeof(Brush), typeof(GradientBar), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public Brush? Track { get => (Brush?)GetValue(TrackProperty); set => SetValue(TrackProperty, value); }

    private bool _running;

    private static readonly Brush Fill = Accent.Brush;

    public GradientBar()
    {
        Height = 6;
        SetResourceReference(TrackProperty, "OB.Track");
        Loaded += (_, _) => Sync();
        Unloaded += (_, _) => Stop();
        IsVisibleChanged += (_, _) => Sync();
    }

    private void Sync()
    {
        if (Value < 0 && IsVisible && IsLoaded) Start(); else Stop();
    }

    private void Start()
    {
        if (_running) return;
        _running = true;
        var anim = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(1.3)) { RepeatBehavior = RepeatBehavior.Forever };
        Timeline.SetDesiredFrameRate(anim, 60);
        BeginAnimation(PhaseProperty, anim);
    }

    private void Stop()
    {
        if (!_running) return;
        _running = false;
        BeginAnimation(PhaseProperty, null);
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;
        double r = h / 2;
        var track = Track ?? new SolidColorBrush(Color.FromArgb(0x26, 0x80, 0x80, 0xA0));
        dc.DrawRoundedRectangle(track, null, new Rect(0, 0, w, h), r, r);

        if (Value >= 0)
        {
            double fw = Math.Max(h, w * Math.Clamp(Value, 0, 1));
            if (Value > 0)
                dc.DrawRoundedRectangle(Fill, null, new Rect(0, 0, fw, h), r, r);
            return;
        }

        double phase = (double)GetValue(PhaseProperty);
        double segment = w * 0.32;
        double x = -segment + phase * (w + segment);
        dc.PushClip(new RectangleGeometry(new Rect(0, 0, w, h), r, r));
        dc.DrawRoundedRectangle(Fill, null, new Rect(x, 0, segment, h), r, r);
        dc.Pop();
    }
}

/// <summary>Display modes for <see cref="CountUpText"/>.</summary>
public enum CountMode { Number, Bytes, Percent }

/// <summary>
/// TextBlock whose number rolls up the first time it gets a value; later updates (live data)
/// are written directly so they cost a single frame.
/// </summary>
public sealed class CountUpText : System.Windows.Controls.TextBlock
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(CountUpText), new PropertyMetadata(double.NaN, OnValueChanged));

    private static readonly DependencyProperty ShownProperty = DependencyProperty.Register(
        "Shown", typeof(double), typeof(CountUpText), new PropertyMetadata(0.0, (d, _) => ((CountUpText)d).Render()));

    public static readonly DependencyProperty ModeProperty = DependencyProperty.Register(
        nameof(Mode), typeof(CountMode), typeof(CountUpText), new PropertyMetadata(CountMode.Number, (d, _) => ((CountUpText)d).Render()));

    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(
        nameof(Placeholder), typeof(string), typeof(CountUpText), new PropertyMetadata("—", (d, _) => ((CountUpText)d).Render()));

    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public CountMode Mode { get => (CountMode)GetValue(ModeProperty); set => SetValue(ModeProperty, value); }
    public string Placeholder { get => (string)GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }

    private bool _revealed;

    public CountUpText() => Render();

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var t = (CountUpText)d;
        var v = (double)e.NewValue;
        if (double.IsNaN(v)) { t.BeginAnimation(ShownProperty, null); t.Render(); return; }
        if (t._revealed || !Reveal.AnimationsEnabled)
        {
            t.BeginAnimation(ShownProperty, null);
            t.SetValue(ShownProperty, v);
            t.Render();
            return;
        }
        t._revealed = true;
        var anim = new DoubleAnimation(v, TimeSpan.FromMilliseconds(600)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        t.BeginAnimation(ShownProperty, anim, HandoffBehavior.SnapshotAndReplace);
    }

    private void Render()
    {
        if (double.IsNaN(Value)) { Text = Placeholder; return; }
        double shown = (double)GetValue(ShownProperty);
        Text = Mode switch
        {
            CountMode.Bytes => ByteSize.Humanize((long)Math.Max(0, shown)),
            CountMode.Percent => Loc.I.IsTurkish
                ? $"%{Math.Round(shown).ToString(CultureInfo.CurrentCulture)}"
                : $"{Math.Round(shown).ToString(CultureInfo.CurrentCulture)}%",
            _ => Math.Round(shown).ToString("N0", CultureInfo.CurrentCulture)
        };
    }
}

/// <summary>Page title row: title and subtitle, with optional actions on the right. (Symbol/From/To are kept for compatibility.)</summary>
[System.Windows.Markup.ContentProperty(nameof(Actions))]
public sealed class PageHeader : Grid
{
    private readonly GradientBadge _badge = new() { Size = 46, Margin = new Thickness(0, 0, 14, 0), Visibility = Visibility.Collapsed };
    private readonly System.Windows.Controls.TextBlock _title = new();
    private readonly System.Windows.Controls.TextBlock _subtitle = new();
    private readonly ContentPresenter _actions = new() { VerticalAlignment = VerticalAlignment.Center };

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(PageHeader), new PropertyMetadata("", (d, e) => ((PageHeader)d)._title.Text = e.NewValue as string ?? ""));

    public static readonly DependencyProperty SubtitleProperty = DependencyProperty.Register(
        nameof(Subtitle), typeof(string), typeof(PageHeader), new PropertyMetadata("", (d, e) =>
        {
            var h = (PageHeader)d;
            h._subtitle.Text = e.NewValue as string ?? "";
            h._subtitle.Visibility = string.IsNullOrEmpty(h._subtitle.Text) ? Visibility.Collapsed : Visibility.Visible;
        }));

    public static readonly DependencyProperty SymbolProperty = DependencyProperty.Register(
        nameof(Symbol), typeof(SymbolRegular), typeof(PageHeader), new PropertyMetadata(SymbolRegular.Apps24, (d, e) => ((PageHeader)d)._badge.Symbol = (SymbolRegular)e.NewValue));

    public static readonly DependencyProperty FromProperty = DependencyProperty.Register(
        nameof(From), typeof(Color), typeof(PageHeader), new PropertyMetadata(Color.FromRgb(0x6E, 0x5B, 0xFF), (d, e) => ((PageHeader)d)._badge.From = (Color)e.NewValue));

    public static readonly DependencyProperty ToProperty = DependencyProperty.Register(
        nameof(To), typeof(Color), typeof(PageHeader), new PropertyMetadata(Color.FromRgb(0xB4, 0x5B, 0xFF), (d, e) => ((PageHeader)d)._badge.To = (Color)e.NewValue));

    public static readonly DependencyProperty ActionsProperty = DependencyProperty.Register(
        nameof(Actions), typeof(object), typeof(PageHeader), new PropertyMetadata(null, (d, e) => ((PageHeader)d)._actions.Content = e.NewValue));

    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Subtitle { get => (string)GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }
    public SymbolRegular Symbol { get => (SymbolRegular)GetValue(SymbolProperty); set => SetValue(SymbolProperty, value); }
    public Color From { get => (Color)GetValue(FromProperty); set => SetValue(FromProperty, value); }
    public Color To { get => (Color)GetValue(ToProperty); set => SetValue(ToProperty, value); }
    public object? Actions { get => GetValue(ActionsProperty); set => SetValue(ActionsProperty, value); }

    public PageHeader()
    {
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Margin = new Thickness(0, 4, 0, 24);

        _title.SetResourceReference(StyleProperty, "OB.Title");
        _subtitle.SetResourceReference(StyleProperty, "OB.Subtitle");
        _subtitle.Visibility = Visibility.Collapsed;
        _badge.Symbol = Symbol;

        var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        texts.Children.Add(_title);
        texts.Children.Add(_subtitle);

        SetColumn(_badge, 0);
        SetColumn(texts, 1);
        SetColumn(_actions, 2);
        Children.Add(_badge);
        Children.Add(texts);
        Children.Add(_actions);
    }
}

/// <summary>Centered "nothing here" placeholder with an icon and a message.</summary>
public sealed class EmptyState : StackPanel
{
    private readonly SymbolIcon _icon = new() { FontSize = 24, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly System.Windows.Controls.TextBlock _title = new() { HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 12, 0, 0) };
    private readonly System.Windows.Controls.TextBlock _text = new() { HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center, MaxWidth = 380 };

    public static readonly DependencyProperty SymbolProperty = DependencyProperty.Register(
        nameof(Symbol), typeof(SymbolRegular), typeof(EmptyState), new PropertyMetadata(SymbolRegular.Info24, (d, e) => ((EmptyState)d)._icon.Symbol = (SymbolRegular)e.NewValue));

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(EmptyState), new PropertyMetadata("", (d, e) => ((EmptyState)d)._title.Text = e.NewValue as string ?? ""));

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(EmptyState), new PropertyMetadata("", (d, e) => ((EmptyState)d)._text.Text = e.NewValue as string ?? ""));

    public SymbolRegular Symbol { get => (SymbolRegular)GetValue(SymbolProperty); set => SetValue(SymbolProperty, value); }
    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public EmptyState()
    {
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        Margin = new Thickness(24);
        var circle = new Border
        {
            Width = 56,
            Height = 56,
            CornerRadius = new CornerRadius(28),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = _icon
        };
        circle.SetResourceReference(Border.BackgroundProperty, "OB.CardStrong");
        _icon.SetResourceReference(SymbolIcon.ForegroundProperty, "OB.Subtext");
        _title.SetResourceReference(StyleProperty, "OB.Section");
        _text.SetResourceReference(StyleProperty, "OB.Subtitle");
        _text.TextWrapping = TextWrapping.Wrap;
        _icon.Symbol = Symbol;
        Children.Add(circle);
        Children.Add(_title);
        Children.Add(_text);
    }
}

/// <summary>Progress step marker: ring (to do), spinner (now), green check (done).</summary>
public sealed class StepDot : Grid
{
    private readonly Ellipse _ring = new() { StrokeThickness = 2 };
    private readonly Spinner _spinner = new() { Width = 22, Height = 22, Thickness = 2.6 };
    private readonly Border _done = new() { CornerRadius = new CornerRadius(12) };

    public static readonly DependencyProperty StepProperty = DependencyProperty.Register(
        nameof(Step), typeof(int), typeof(StepDot), new PropertyMetadata(0, (d, _) => ((StepDot)d).Sync()));

    public static readonly DependencyProperty CurrentProperty = DependencyProperty.Register(
        nameof(Current), typeof(int), typeof(StepDot), new PropertyMetadata(0, (d, _) => ((StepDot)d).Sync()));

    public int Step { get => (int)GetValue(StepProperty); set => SetValue(StepProperty, value); }
    public int Current { get => (int)GetValue(CurrentProperty); set => SetValue(CurrentProperty, value); }

    public StepDot()
    {
        Width = 24;
        Height = 24;
        _ring.SetResourceReference(Shape.StrokeProperty, "OB.Stroke");
        _done.SetResourceReference(Border.BackgroundProperty, "OB.Success");
        _done.Child = new SymbolIcon { Symbol = SymbolRegular.Checkmark24, FontSize = 13, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        Children.Add(_ring);
        Children.Add(_spinner);
        Children.Add(_done);
        Sync();
    }

    private void Sync()
    {
        bool done = Current > Step, active = Current == Step;
        _ring.Visibility = !done && !active ? Visibility.Visible : Visibility.Collapsed;
        _spinner.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
        _done.Visibility = done ? Visibility.Visible : Visibility.Collapsed;
    }
}
