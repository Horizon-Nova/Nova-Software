using System.Globalization;
using System.Windows.Data;
using Helix_Studio.Models;
using Helix_Studio.Services;

namespace Helix_Studio.Converters;

/// <summary>EventCategory → 顯示文字（依當前語言）。</summary>
public sealed class EventCategoryToTextConverter : IValueConverter
{
    private static readonly Dictionary<EventCategory, string> Keys = new()
    {
        [EventCategory.System] = "L.Event.Category.System",
        [EventCategory.Install] = "L.Event.Category.Install",
        [EventCategory.Runtime] = "L.Event.Category.Runtime",
    };

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is EventCategory c && Keys.TryGetValue(c, out var key)
            ? LocalizationService.Instance.GetString(key)
            : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
