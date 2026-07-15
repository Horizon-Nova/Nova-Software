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

/// <summary>狀態色點/左側色條用的「實色」——對齊 blueprint AppsScreen.dc.html 每列左側 4px 色條：
/// 執行中 accent-solid、已停止 border、錯誤 severity-error-solid。</summary>
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

/// <summary>狀態 pill 的底色——對齊線上版 blueprint 實機比對：執行中/警告/錯誤是淺色調底，
/// 已停止是純文字、沒有底色（跟其他三種狀態不是同一視覺語言，不是「淺一點的底色」而是
/// 直接沒有底），比對 dc.html 原始碼的 bg-hover 只是文字定義，線上實際渲染沒有背景。</summary>
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

/// <summary>狀態 pill 的文字色——跟底色搭配的深色系，對齊 blueprint 每種狀態各自的 text 變數。</summary>
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

/// <summary>啟動/停止按鈕的樣式——可切換時是 PrimaryButtonStyle（實心 accent 色，對齊 blueprint
/// 「啟動/停止」按鈕 background:var(--accent-solid)），錯誤狀態（顯示「重試」）時是
/// SecondaryButtonStyle（outline，靠 IsEnabled=false 呈現停用態），不是兩種狀態共用同一個樣式。</summary>
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

/// <summary>啟動/停止按鈕的最終可用狀態要合一兩個條件：狀態不是 Error、而且這顆共用指令
/// 目前沒有其他列正在執行中（IAsyncRelayCommand.IsRunning，見 usage-patterns.md 忙碌指示器
/// 慣用寫法）。指令本身預設就會在 IsRunning 時拒絕 CanExecute，這裡明著寫出來，不依賴
/// 框架的隱含行為，維護的人一眼看得出「為什麼會被停用」。</summary>
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

/// <summary>執行中的工具無法移除的解釋文字——啟動/停止按鈕原本就有對應的 disabled 提示
/// （ToolStateToRetryTooltipConverter），這裡是「移除」按鈕的同類提示，之前漏掉。</summary>
public sealed class RunningToRemoveTooltipConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "請先停止再移除" : null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}

/// <summary>ExpandedTab 是否等於 ConverterParameter 指定的分頁鍵——摘要/設定兩個 tab
/// 內容各自的 Visibility 綁定用，兩個 tab 共用同一個字串屬性，不需要兩個布林屬性。</summary>
public sealed class StringEqualsVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Equals(value, parameter) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}

/// <summary>解鎖倒數文字——「已解鎖鎖定欄位・剩餘 mm:ss」，值來源是 UnlockExpiresAt 這個固定
/// 時間點，但每次 ExpandedTool 因 1 秒刷新重新指派時，binding 值變了會重新呼叫本轉換器，
/// 用當下 UtcNow 重算剩餘秒數即可達到「畫面上看起來在倒數」的效果，不需要另開一個 Timer。</summary>
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

/// <summary>「詳細」按鈕的選取視覺——對齊 blueprint app.selectedStyle（展開時 accent-tint 底、
/// accent-border 框、accent-text 字）。「這一列算不算展開」的判斷只在 ApplyBuildRows 算一次
/// （見 ToolRow.IsExpanded），這裡單純吃算好的布林值，不重新比對 Id。</summary>
public sealed class BoolToDetailButtonStyleConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true
            ? Application.Current.TryFindResource("SecondarySelectedButtonStyle")
            : Application.Current.TryFindResource("SecondaryButtonStyle");

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}

/// <summary>「上次啟動」欄位——資料庫沒有值（工具從未啟動過）時顯示「—」，不是空白，
/// 對齊 blueprint app.last 的顯示慣例；有值時格式化成 MM/dd HH:mm，跟藍圖範例格式一致。</summary>
public sealed class LastStartedToDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is DateTimeOffset lastStarted ? lastStarted.ToString("MM/dd HH:mm", culture) : "—";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}

/// <summary>鎖定欄位的調淡效果——對齊 blueprint autoStartOpacity/autoRepairOpacity
/// （鎖定時 0.4，其餘 1），讓使用者一眼看出這個欄位目前不能改。</summary>
public sealed class LockedToOpacityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? 0.4 : 1.0;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}

/// <summary>啟動/停止成功後該列的 flash 背景——對齊 blueprint flashStyle。同上，「這一列算不算
/// 剛閃爍過」已經在 ApplyBuildRows 算好（ToolRow.IsFlashed），這裡只吃布林值轉顏色。</summary>
public sealed class BoolToFlashBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? ToolStateToSolidBrushConverter.FindBrush("AccentFlashBrush") : Brushes.Transparent;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}
