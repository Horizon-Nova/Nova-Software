using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Vaeron.HelixStudio.Features.EventLog.Contracts;

namespace Vaeron.HelixStudio.Features.EventLog;

public sealed class SeverityToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        EventSeverity.Info => "一般",
        EventSeverity.Warning => "警告",
        EventSeverity.Error => "錯誤",
        EventSeverity.None => "無（不記錄任何事件）",
        _ => "",
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}

/// <summary>複製按鈕文字：CopyFeedback 為空（尚未複製過）時顯示「複製」，複製後顯示
/// ViewModel 設的回饋文字（「已複製」）——CopyFeedback 不會自動歸零，直接顯示目前狀態即可。</summary>
public sealed class CopyFeedbackToLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string { Length: > 0 } feedback ? feedback : "複製";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}

/// <summary>Pill 篩選按鈕的選取視覺——比對按鈕自身的選項字串跟 ViewModel 目前的篩選值，
/// 兩者分屬不同 DataContext，只能用 MultiBinding；選中用 PrimaryButtonStyle，其餘 Secondary。</summary>
public sealed class FilterOptionToButtonStyleConverter : IMultiValueConverter
{
    public object? Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
        => values is [string option, string current] && option == current
            ? System.Windows.Application.Current.TryFindResource("PrimaryButtonStyle")
            : System.Windows.Application.Current.TryFindResource("SecondaryButtonStyle");

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class SeverityToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        EventSeverity.Error => new SolidColorBrush(Color.FromRgb(0xDD, 0x50, 0x3F)),
        EventSeverity.Warning => new SolidColorBrush(Color.FromRgb(0xE1, 0xA0, 0x35)),
        _ => new SolidColorBrush(Color.FromRgb(0x82, 0x87, 0x8B)),
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}
