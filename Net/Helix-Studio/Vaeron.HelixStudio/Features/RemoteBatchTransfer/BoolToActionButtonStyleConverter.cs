using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Vaeron.HelixStudio.Features.RemoteBatchTransfer;

/// <summary>複製/移動兩顆動作按鈕的選取視覺——選中的用 PrimaryButtonStyle，未選中的用
/// SecondaryButtonStyle，比照 blueprint `selectCopy`/`selectMove` 的 active-state 樣式概念。</summary>
public sealed class BoolToActionButtonStyleConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true
            ? Application.Current.TryFindResource("PrimaryButtonStyle")
            : Application.Current.TryFindResource("SecondaryButtonStyle");

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}
