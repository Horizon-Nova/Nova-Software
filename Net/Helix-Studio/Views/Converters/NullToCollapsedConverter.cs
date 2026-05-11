using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Helix_Studio.Views.Converters;

/// <summary>當值為 <c>null</c> 時回傳 <see cref="Visibility.Collapsed"/>，否則回傳 <see cref="Visibility.Visible"/>。</summary>
public sealed class NullToCollapsedConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is null ? Visibility.Collapsed : Visibility.Visible;

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException("不支援從 Visibility 反向推導。");
}

