using System.Globalization;
using System.Windows.Data;
using Helix_Studio.Models;
using Helix_Studio.Services;

namespace Helix_Studio.Converters;

/// <summary>EventLevel → 顯示文字（依當前語言）。</summary>
public sealed class EventLevelToTextConverter : IValueConverter
{
    private static readonly Dictionary<EventLevel, string> Keys = new()
    {
        [EventLevel.Info] = "L.Event.Level.Info",
        [EventLevel.Warning] = "L.Event.Level.Warning",
        [EventLevel.Error] = "L.Event.Level.Error",
    };

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is EventLevel l && Keys.TryGetValue(l, out var key)
            ? LocalizationService.Instance.GetString(key)
            : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
