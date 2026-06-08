using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace Helix_Studio.Views.Converters;

/// <summary>
/// 格式化字串 Converter。
///
/// 用途：
/// - 讓 XAML 可以使用 <see cref="MultiBinding"/>，以第一個值作為格式字串，其餘值作為參數。
/// - 配合 <c>DynamicResource</c> 的語系字典切換，避免 ViewModel 參與在地化字串的組裝與刷新線路。
/// </summary>
public sealed class FormatStringConverter : IMultiValueConverter
{
    /// <summary>
    /// 將第一個值作為格式字串，後續值作為格式化參數。
    /// </summary>
    /// <param name="values">MultiBinding 傳入的值陣列。</param>
    /// <param name="targetType">目標型別。</param>
    /// <param name="parameter">轉換參數。</param>
    /// <param name="culture">目前文化資訊。</param>
    /// <returns>格式化後的字串。</returns>
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values is null || values.Length == 0)
        {
            return string.Empty;
        }

        var format = values[0] as string;
        if (string.IsNullOrWhiteSpace(format))
        {
            return string.Empty;
        }

        var args = values.Skip(1).ToArray();
        return args.Length == 0
            ? format
            : string.Format(culture, format, args);
    }

    /// <summary>
    /// 不支援反向轉換。
    /// </summary>
    /// <param name="value">來源值。</param>
    /// <param name="targetTypes">目標型別集合。</param>
    /// <param name="parameter">轉換參數。</param>
    /// <param name="culture">目前文化資訊。</param>
    /// <returns>此方法一律拋出例外。</returns>
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
