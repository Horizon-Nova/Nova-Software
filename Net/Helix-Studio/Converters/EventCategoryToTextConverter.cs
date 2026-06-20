using System.Globalization;
using System.Windows.Data;
using Helix_Studio.Models;

namespace Helix_Studio.Converters;

/// <summary>EventCategory → 顯示文字。</summary>
public sealed class EventCategoryToTextConverter : IValueConverter
{
    private static readonly Dictionary<EventCategory, string> Texts = new()
    {
        [EventCategory.System] = "系統",
        [EventCategory.Install] = "安裝",
        [EventCategory.Runtime] = "執行",
    };

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is EventCategory c && Texts.TryGetValue(c, out var t) ? t : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
