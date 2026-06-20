using System.Globalization;
using System.Windows.Data;
using Helix_Studio.Models;

namespace Helix_Studio.Converters;

/// <summary>EventLevel → 顯示文字。</summary>
public sealed class EventLevelToTextConverter : IValueConverter
{
    private static readonly Dictionary<EventLevel, string> Texts = new()
    {
        [EventLevel.Info] = "資訊",
        [EventLevel.Warning] = "警告",
        [EventLevel.Error] = "錯誤",
    };

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is EventLevel l && Texts.TryGetValue(l, out var t) ? t : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
