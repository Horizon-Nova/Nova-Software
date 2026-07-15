using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Vaeron.HelixStudio.Features.RemoteBatchTransfer.Contracts;

namespace Vaeron.HelixStudio.Features.RemoteBatchTransfer;

public sealed class TaskLogToggleLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "收合" : "展開";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>把「這一列所屬的任務」跟「這一列本身」打包成 (task, target) tuple，供
/// ApplyRetryTargetCommand 使用——單一目標重試需要任務層級的來源路徑/動作，兩者分屬不同
/// DataContext，只能用 MultiBinding 打包。</summary>
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

public sealed class ConnectionStatusToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        ConnectionStatus.Connected => new SolidColorBrush(Color.FromRgb(0xD8, 0xF0, 0xE2)),
        ConnectionStatus.Testing => new SolidColorBrush(Color.FromRgb(0xFA, 0xE1, 0xB8)),
        _ => new SolidColorBrush(Color.FromRgb(0xE5, 0xE5, 0xE5)),
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

/// <summary>覆寫路徑徽章文字——「已覆寫」或「使用預設」，比對 blueprint 逐台目的路徑覆寫面板。</summary>
public sealed class OverrideBadgeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string { Length: > 0 } ? "已覆寫" : "使用預設";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
