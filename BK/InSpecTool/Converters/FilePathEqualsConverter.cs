using System.Globalization;
using System.Windows.Data;

namespace ExternalModules.InSpecTool.Converters;

/// <summary>
/// 對齊 JavaFX HomePage.FileCell 以路徑比對套用 hn-file-clicked 高亮。
/// </summary>
public sealed class FilePathEqualsConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2)
        {
            return false;
        }

        var filePath = values[0] as string;
        var lastClicked = values[1] as string;
        if (string.IsNullOrEmpty(filePath) || string.IsNullOrEmpty(lastClicked))
        {
            return false;
        }

        return string.Equals(filePath, lastClicked, StringComparison.OrdinalIgnoreCase);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
