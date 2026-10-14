using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Vaeron.VNCSCP.Internal.Contracts;

namespace Vaeron.VNCSCP.Dashboard;

/// <summary>狀態徽章的形狀。連線狀態逐個對應一個具名幾何資源。</summary>
public sealed class ConnectionStatusGeometryConverter : IValueConverter
{
    private static readonly Dictionary<ConnectionStatus, string> GeometryKeyByStatus = new()
    {
        [ConnectionStatus.Unknown] = "IconStatusDotGeometry",
        [ConnectionStatus.Reachable] = "IconCheckGeometry",
        [ConnectionStatus.WrongPassword] = "IconKeyGeometry",
        [ConnectionStatus.Locked] = "IconLockGeometry",
        [ConnectionStatus.Unreachable] = "IconCloseGeometry"
    };

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = new[] { value }
            .OfType<ConnectionStatus>()
            .Select(status => GeometryKeyByStatus.GetValueOrDefault(status, "IconStatusDotGeometry"))
            .DefaultIfEmpty("IconStatusDotGeometry")
            .First();

        return Application.Current.TryFindResource(key);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
