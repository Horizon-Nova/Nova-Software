using System;
using System.Globalization;
using System.Windows.Data;

namespace Helix_Studio.Views.Converters;

/// <summary>
/// 將字串相等判斷轉為布林值，並支援透過 <see cref="IValueConverter.ConvertBack"/> 回寫選取值。
/// </summary>
public sealed class StringEqualsToBooleanConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is string text
           && parameter is string expected
           && string.Equals(text, expected, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is true && parameter is string expected)
        {
            return expected;
        }

        return Binding.DoNothing;
    }
}

