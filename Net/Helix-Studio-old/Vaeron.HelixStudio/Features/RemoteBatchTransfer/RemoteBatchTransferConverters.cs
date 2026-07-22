using System.Globalization;
using System.Windows.Data;
using Vaeron.HelixStudio.Features.RemoteBatchTransfer.Contracts;

namespace Vaeron.HelixStudio.Features.RemoteBatchTransfer;

public sealed class TaskLogToggleLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "收合" : "展開";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>(task, target) 配對打包，供單一目標重試使用</summary>
public sealed class TaskTargetPairConverter : IMultiValueConverter
{
    public object? Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
        => values is [TransferTaskSnapshot task, TransferTargetSnapshot target] ? (task, target) : null;

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class ConnectionStatusToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        ConnectionStatus.Connected => "已連線",
        ConnectionStatus.Testing => "測試中…",
        _ => "未連線",
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>連線狀態底色：走主題 Token，不寫死顏色</summary>
public sealed class ConnectionStatusToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        ConnectionStatus.Connected => System.Windows.Application.Current.TryFindResource("StatusSuccessBgBrush"),
        ConnectionStatus.Testing => System.Windows.Application.Current.TryFindResource("StatusWarningBgBrush"),
        _ => System.Windows.Application.Current.TryFindResource("BgHoverBrush"),
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public sealed class ConnectionStatusConnectedConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is ConnectionStatus.Connected;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public sealed class AuthMethodToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is AuthMethod.Key ? "金鑰驗證" : "密碼驗證";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>覆寫路徑徽章文字</summary>
public sealed class OverrideBadgeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string { Length: > 0 } ? "已覆寫" : "使用預設";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
