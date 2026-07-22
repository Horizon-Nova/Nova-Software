using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Vaeron.HelixStudio.Features.RemoteBatchTransfer;

/// <summary>複製/移動按鈕選取樣式</summary>
public sealed class BoolToActionButtonStyleConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true
            ? Application.Current.TryFindResource("PrimaryButtonStyle")
            : Application.Current.TryFindResource("SecondaryButtonStyle");

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}
