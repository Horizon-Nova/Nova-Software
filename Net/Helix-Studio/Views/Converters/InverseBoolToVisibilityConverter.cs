using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Helix_Studio.Views.Converters;

/// <summary>
/// 將布林值反向轉成 WPF <see cref="Visibility"/>。
/// </summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility visibility && visibility != Visibility.Visible;
}

