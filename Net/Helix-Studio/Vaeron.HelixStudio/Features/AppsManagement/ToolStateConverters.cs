using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Vaeron.HelixStudio.Features.AppsManagement.Contracts;

namespace Vaeron.HelixStudio.Features.AppsManagement;

public sealed class ToolStateToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        ToolState.Running => "執行中",
        ToolState.Starting => "啟動中",
        ToolState.Error => "錯誤",
        _ => "已停止",
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}

/// <summary>狀態色點／左側色條實色</summary>
public sealed class ToolStateToSolidBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        ToolState.Running or ToolState.Starting => FindBrush("AccentSolidBrush"),
        ToolState.Error => FindBrush("StatusErrorTextBrush"),
        _ => FindBrush("BorderBrush2"),
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;

    internal static Brush FindBrush(string key)
        => (Brush?)Application.Current.TryFindResource(key) ?? Brushes.Gray;
}

/// <summary>狀態 pill 底色</summary>
public sealed class ToolStateToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        ToolState.Running => ToolStateToSolidBrushConverter.FindBrush("AccentFlashSoftBrush"),
        ToolState.Starting => ToolStateToSolidBrushConverter.FindBrush("StatusWarningBgBrush"),
        ToolState.Error => ToolStateToSolidBrushConverter.FindBrush("StatusErrorBgBrush"),
        _ => Brushes.Transparent,
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}

/// <summary>狀態 pill 文字色</summary>
public sealed class ToolStateToTextBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        ToolState.Running => ToolStateToSolidBrushConverter.FindBrush("AccentTextBrush"),
        ToolState.Starting => ToolStateToSolidBrushConverter.FindBrush("StatusWarningTextBrush"),
        ToolState.Error => ToolStateToSolidBrushConverter.FindBrush("StatusErrorTextBrush"),
        _ => ToolStateToSolidBrushConverter.FindBrush("Text2Brush"),
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}

/// <summary>啟動/停止按鈕樣式</summary>
public sealed class ToolStateToActionButtonStyleConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is ToolState.Error
            ? Application.Current.TryFindResource("SecondaryButtonStyle")
            : Application.Current.TryFindResource("PrimaryButtonStyle");

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}

public sealed class RunningToStartStopLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        InstalledToolSnapshot { State: ToolState.Error } => "重試",
        InstalledToolSnapshot { IsRunning: true } => "停止",
        _ => "啟動",
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}

/// <summary>啟動/停止按鈕可用狀態（非錯誤且指令未執行中）</summary>
public sealed class ToolStateToggleEnabledMultiConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
        => values is [ToolState state, bool isCommandRunning] && state is not ToolState.Error && !isCommandRunning;

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class ToolStateToRetryTooltipConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is ToolState.Error ? "需人工排除錯誤後才能重試" : null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}

/// <summary>執行中工具的移除按鈕停用提示</summary>
public sealed class RunningToRemoveTooltipConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "請先停止再移除" : null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}

/// <summary>字串相等判斷（分頁顯示切換）</summary>
public sealed class StringEqualsVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Equals(value, parameter) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}

/// <summary>解鎖倒數文字</summary>
public sealed class UnlockCountdownConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DateTimeOffset expiresAt) return null;
        var remaining = expiresAt - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero) return null;
        return $"已解鎖鎖定欄位・剩餘 {(int)remaining.TotalMinutes:D2}:{remaining.Seconds:D2}";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}

/// <summary>「詳細」按鈕選取樣式</summary>
public sealed class BoolToDetailButtonStyleConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true
            ? Application.Current.TryFindResource("SecondarySelectedButtonStyle")
            : Application.Current.TryFindResource("SecondaryButtonStyle");

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}

/// <summary>「上次啟動」欄位顯示格式</summary>
public sealed class LastStartedToDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is DateTimeOffset lastStarted ? lastStarted.ToString("MM/dd HH:mm", culture) : "—";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}

/// <summary>鎖定欄位調淡效果</summary>
public sealed class LockedToOpacityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? 0.4 : 1.0;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}

/// <summary>操作成功列 flash 背景色</summary>
public sealed class BoolToFlashBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? ToolStateToSolidBrushConverter.FindBrush("AccentFlashBrush") : Brushes.Transparent;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}
