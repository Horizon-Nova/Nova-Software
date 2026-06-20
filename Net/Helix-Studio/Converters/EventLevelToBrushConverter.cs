using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Helix_Studio.Models;

namespace Helix_Studio.Converters;

/// <summary>EventLevel → 顏色筆刷。顏色鍵查表，筆刷取自 Colors.xaml。</summary>
public sealed class EventLevelToBrushConverter : IValueConverter
{
    private static readonly Dictionary<EventLevel, string> BrushKeys = new()
    {
        [EventLevel.Info] = "BrushStatusGray",
        [EventLevel.Warning] = "BrushStatusAmber",
        [EventLevel.Error] = "BrushStatusRed",
    };

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is EventLevel l && BrushKeys.TryGetValue(l, out var key)
            && Application.Current?.TryFindResource(key) is Brush brush)
            return brush;
        return Brushes.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
