using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Vaeron.HelixStudio.Features.UsbVoltageControl.Contracts;

namespace Vaeron.HelixStudio.Features.UsbVoltageControl;

/// <summary>裝置列狀態色點：走主題 Token，不寫死顏色</summary>
public sealed class DeviceStatusToDotBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        UsbDeviceSnapshot { Connected: false } => System.Windows.Application.Current.TryFindResource("TextMutedBrush"),
        UsbDeviceSnapshot { Connected: true, IsWithinTolerance: false } => System.Windows.Application.Current.TryFindResource("SeverityErrorSolidBrush"),
        UsbDeviceSnapshot => System.Windows.Application.Current.TryFindResource("StatusSuccessSolidBrush"),
        _ => Brushes.Transparent,
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>連線狀態底色：走主題 Token，不寫死顏色</summary>
public sealed class ConnectedToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true
            ? System.Windows.Application.Current.TryFindResource("StatusSuccessBgBrush")
            : System.Windows.Application.Current.TryFindResource("BgHoverBrush");

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public sealed class ConnectedToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "已連接" : "未連接";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public sealed class NoPowerControlTooltipConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is false ? "此裝置的驅動程式未回報支援電源管理，無法調整電壓" : null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>「套用修正」按鈕顯示條件</summary>
public sealed class ShowApplyButtonConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is not [UsbDeviceSnapshot device, bool globalAutoCorrect, bool serviceRunning]) return Visibility.Collapsed;
        if (!device.Connected || !device.SupportsPowerControl || device.IsWithinTolerance) return Visibility.Collapsed;
        return !globalAutoCorrect || !serviceRunning ? Visibility.Visible : Visibility.Collapsed;
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
