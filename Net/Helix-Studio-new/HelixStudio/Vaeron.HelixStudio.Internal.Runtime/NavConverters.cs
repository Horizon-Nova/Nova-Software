using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Vaeron.HelixStudio.Internal.Runtime;

/// <summary>導覽項目 Key 轉圖示資源鍵</summary>
public sealed class NavKeyToIconConverter : IValueConverter
{
    private static readonly Dictionary<string, string> Map = new()
    {
        ["apps"] = "IconLayoutGridGeometry",
        ["library"] = "IconGrid3x3Geometry",
        ["events"] = "IconBellGeometry",
        ["settings"] = "IconSlidersHorizontalGeometry",
    };

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string key && Map.TryGetValue(key, out var resourceKey)
            ? Application.Current.TryFindResource(resourceKey)
            : null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>字串取最後一個字元</summary>
public sealed class LastCharConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string { Length: > 0 } s ? s[^1..] : "";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>導覽項目 Key 與目前作用中 Key 比對</summary>
public sealed class NavKeyEqualsConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
        => values is [string itemKey, string currentKey] && itemKey == currentKey;

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
