using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Vanish.Models;

namespace Vanish.Helpers.Converters;

/// <summary>Resolves a DisplayIcon registry string to an icon image for binding.</summary>
public sealed class DisplayIconToImageConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => IconExtractor.FromDisplayIcon(value as string);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility.Visible;
}

public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is not true;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is not true;
}

public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool invert = parameter is string s && s.Equals("invert", StringComparison.OrdinalIgnoreCase);
        bool hasValue = value is not null;
        if (invert) hasValue = !hasValue;
        return hasValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Shows an element only when a count is zero (e.g. an "empty" message).</summary>
public sealed class ZeroCountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is int n && n == 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>"tr"/"en" -> "TR"/"EN" for the language toggle button.</summary>
public sealed class LangCodeUpperConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => (value as string)?.ToUpperInvariant() ?? "EN";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Colours a leftover row by how confident the match is.</summary>
public sealed class ConfidenceToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush High = new(Color.FromRgb(0x4C, 0xAF, 0x50));
    private static readonly SolidColorBrush Medium = new(Color.FromRgb(0xFF, 0xB7, 0x4D));
    private static readonly SolidColorBrush Low = new(Color.FromRgb(0x9E, 0x9E, 0x9E));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value switch
        {
            MatchConfidence.High => High,
            MatchConfidence.Medium => Medium,
            _ => Low
        };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
