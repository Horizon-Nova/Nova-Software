using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Helix_Studio.Models;

namespace Helix_Studio.Converters;

/// <summary>
/// AppStatus → 顏色筆刷。狀態對映顏色鍵以查表表達，
/// 實際筆刷仍從 Colors.xaml 取得，顏色定義維持單一來源。
/// </summary>
public sealed class AppStatusToBrushConverter : IValueConverter
{
    private static readonly Dictionary<AppStatus, string> BrushKeys = new()
    {
        [AppStatus.Running] = "BrushStatusGreen",
        [AppStatus.Stopped] = "BrushStatusGray",
        [AppStatus.Error] = "BrushStatusRed",
    };

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is AppStatus s && BrushKeys.TryGetValue(s, out var key)
            && Application.Current?.TryFindResource(key) is Brush brush)
            return brush;
        return Brushes.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
