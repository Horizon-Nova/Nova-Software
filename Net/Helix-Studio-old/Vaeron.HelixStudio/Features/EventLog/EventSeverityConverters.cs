using System.Globalization;
using System.Windows.Data;
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

/// <summary>嚴重程度色點：走主題 Token，不寫死顏色，暗色模式才會跟著換色</summary>
public sealed class SeverityToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        EventSeverity.Error => System.Windows.Application.Current.TryFindResource("SeverityErrorSolidBrush"),
        EventSeverity.Warning => System.Windows.Application.Current.TryFindResource("SeverityWarningSolidBrush"),
        _ => System.Windows.Application.Current.TryFindResource("TextMutedBrush"),
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}
