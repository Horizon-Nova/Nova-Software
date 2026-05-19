using System;
using System.Globalization;
using System.Windows.Data;

namespace Helix_Studio.Views.Converters;

/// <summary>
/// 比對兩個字串是否相等（不區分大小寫）並轉為布林值。
/// </summary>
public sealed class StringEqualsMultiBooleanConverter : IMultiValueConverter
{
    /// <inheritdoc />
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        => values.Length >= 2
           && values[0] is string left
           && values[1] is string right
           && string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => [Binding.DoNothing, Binding.DoNothing];
}
