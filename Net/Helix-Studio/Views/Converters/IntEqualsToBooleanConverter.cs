using System;
using System.Globalization;
using System.Windows.Data;

namespace Helix_Studio.Views.Converters;

/// <summary>將整數值與參數比對，轉為布林值，支援 TwoWay。</summary>
public sealed class IntEqualsToBooleanConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var current = value is int number ? number : 0;
        var target = ParseParameter(parameter);
        return current == target;
    }

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not bool isChecked || !isChecked)
        {
            return Binding.DoNothing;
        }

        return ParseParameter(parameter);
    }

    private static int ParseParameter(object parameter)
        => parameter switch
        {
            int number => number,
            string text when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) => number,
            _ => 0,
        };
}

