using System.Globalization;
using System.Windows.Data;

namespace Helix_Studio.Converters;

/// <summary>字串等於參數 → true（不分大小寫）；供導覽高亮使用。</summary>
public sealed class StringEqualsBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => string.Equals(value as string, parameter as string, StringComparison.OrdinalIgnoreCase);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
