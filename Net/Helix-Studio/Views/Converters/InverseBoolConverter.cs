using System;
using System.Globalization;
using System.Windows.Data;

namespace Helix_Studio.Views.Converters;

/// <summary>
/// 反轉布林值。
/// </summary>
public sealed class InverseBoolConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool flag && !flag;

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool flag && !flag;
}

