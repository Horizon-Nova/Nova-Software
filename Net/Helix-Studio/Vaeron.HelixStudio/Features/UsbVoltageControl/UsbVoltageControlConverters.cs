using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Vaeron.HelixStudio.Features.UsbVoltageControl.Contracts;

namespace Vaeron.HelixStudio.Features.UsbVoltageControl;

/// <summary>裝置列狀態色點——偏離目標紅色、正常綠色、未連線灰色，比對 blueprint `barColor`。</summary>
public sealed class DeviceStatusToDotBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        UsbDeviceSnapshot { Connected: false } => new SolidColorBrush(Color.FromRgb(0x82, 0x87, 0x8B)),
        UsbDeviceSnapshot { Connected: true, IsWithinTolerance: false } => new SolidColorBrush(Color.FromRgb(0xDD, 0x50, 0x3F)),
        UsbDeviceSnapshot => new SolidColorBrush(Color.FromRgb(0x2E, 0x9E, 0x6B)),
        _ => Brushes.Transparent,
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public sealed class ConnectedToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? new SolidColorBrush(Color.FromRgb(0xD8, 0xF0, 0xE2)) : new SolidColorBrush(Color.FromRgb(0xE5, 0xE5, 0xE5));

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

/// <summary>只有「非自動模式或服務停止」且「裝置確實偏離目標」時才顯示「套用修正」主要按鈕，
/// 其餘情況（已經自動修正、或本來就在容許範圍內）不顯示，不讓一顆按鈕身兼兩種語意。</summary>
public sealed class ShowApplyButtonConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is not [UsbDeviceSnapshot device, bool globalAutoCorrect, bool serviceRunning]) return false;
        if (!device.Connected || !device.SupportsPowerControl || device.IsWithinTolerance) return false;
        return !globalAutoCorrect || !serviceRunning;
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
