using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Vaeron.HelixStudio.Themes;

/// <summary>跨畫面共用轉換器</summary>
public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && !b;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && !b;
}

public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public sealed class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int count && count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public sealed class CountToEmptyVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int count && count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Visibility.Visible;
}

public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not Visibility.Visible;
}

/// <summary>兩物件參照相同時回傳 Visible，用於 Table 展開列的目前項目比對</summary>
public sealed class ReferenceEqualsToVisibilityConverter : IMultiValueConverter
{
    public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
        => values is [{ } a, { } b] && ReferenceEquals(a, b) ? Visibility.Visible : Visibility.Collapsed;

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>選項值等於目前選取值時回傳 PrimaryButtonStyle，否則 SecondaryButtonStyle，用於按鈕群組選取樣式</summary>
public sealed class ObjectEqualsToButtonStyleConverter : IMultiValueConverter
{
    public object? Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
        => values is [{ } option, { } current] && Equals(option, current)
            ? Application.Current.TryFindResource("PrimaryButtonStyle")
            : Application.Current.TryFindResource("SecondaryButtonStyle");

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>連在一起的分段按鈕群組（例如舒適/精簡、省電/平衡/高效能），搭配 SegmentedButtonGroupBorderStyle 外框使用。</summary>
public sealed class ObjectEqualsToSegmentedButtonStyleConverter : IMultiValueConverter
{
    public object? Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
        => values is [{ } option, { } current] && Equals(option, current)
            ? Application.Current.TryFindResource("SegmentedButtonSelectedStyle")
            : Application.Current.TryFindResource("SegmentedButtonStyle");

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// 分段按鈕群組動態項目（ItemsControl 產生的按鈕，如省電/平衡/高效能）：依索引/總數推導只有頭尾外側該圓角的
/// CornerRadius，餵給 SegmentedButtonStyle 的 Tag。WPF 的 Border.ClipToBounds 只裁到矩形版面邊界，不會裁成
/// CornerRadius 描的圓角形狀（跟 CSS overflow:hidden 對圓角容器的行為不同）——方角子項目會從外框圓角轉角處
/// 露出來，所以改成每個按鈕自己在正確的一側畫圓角，不靠外層裁切。
/// </summary>
public sealed class SegmentCornerRadiusConverter : IMultiValueConverter
{
    private const double Radius = 7;

    public object? Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is not [int index, int count] || count <= 0) return new CornerRadius(0);
        if (count == 1) return new CornerRadius(Radius);
        if (index == 0) return new CornerRadius(Radius, 0, 0, Radius);
        if (index == count - 1) return new CornerRadius(0, Radius, Radius, 0);
        return new CornerRadius(0);
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
