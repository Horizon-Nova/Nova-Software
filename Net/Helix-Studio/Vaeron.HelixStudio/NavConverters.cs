using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Vaeron.HelixStudio;

/// <summary>導覽項目 Key 對應 Icons.xaml 的 PathGeometry 資源鍵——只有 MainNav 四項有圖示
/// （對齊 blueprint seed/nav.js：ToolsNav 沒有圖示），查不到對應鍵時回傳 null，
/// 綁定端（IconGlyph.IconData）留空，不丟例外。</summary>
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

/// <summary>側邊欄摺疊成圖示列時，工作區切換器只顯示名稱最後一個字——對齊 blueprint
/// ws.workspaceInitial 的算法：(wsRaw.workspaceName || '?').slice(-1)，取的是最後一個字元
/// 不是第一個（之前這裡誤植成第一個字，跟 blueprint 對不上），不是硬塞整個名稱擠壞版面。</summary>
public sealed class LastCharConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string { Length: > 0 } s ? s[^1..] : "";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>比對導覽項目自己的 Key 與殼層目前作用中的 CurrentNavKey——兩者分屬不同
/// DataContext（項目本身 vs. ItemsControl 的祖先 DataContext），只能用 MultiBinding。</summary>
public sealed class NavKeyEqualsConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
        => values is [string itemKey, string currentKey] && itemKey == currentKey;

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
