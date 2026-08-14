using System.Globalization;
using System.Windows.Data;

namespace Vaeron.HelixStudio.App.Themes;

/// <summary>視窗寬度換算為版面尺寸，對應設計稿的 clamp(0.62px, 0.40px + 0.015vw, 0.72px) 乘上介面縮放。</summary>
/// <remarks>
/// Base 留預設 1 時回傳的是縮放係數本身，供根容器的 LayoutTransform 使用；
/// 填入設計稿基準值時回傳該項目換算後的實際尺寸，供不受 LayoutTransform 影響的視窗層屬性使用。
/// UiScale 對應設計稿 state.uiScale（設計稿預設 100），「設定」畫面接上後改由該畫面驅動。
/// </remarks>
public sealed class UiFluidConverter : IValueConverter
{
    private const double UiScale = 1.25;

    public double Base { get; init; } = 1.0;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => Base * UiScale * Math.Clamp(0.40 + (0.015 * (double)value / 100.0), 0.62, 0.72);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
