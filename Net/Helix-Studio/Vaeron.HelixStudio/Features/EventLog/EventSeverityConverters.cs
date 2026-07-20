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

/// <summary>複製按鈕文字</summary>
public sealed class CopyFeedbackToLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string { Length: > 0 } feedback ? feedback : "複製";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
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
