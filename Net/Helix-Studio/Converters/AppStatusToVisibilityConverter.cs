using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Helix_Studio.Models;

namespace Helix_Studio.Converters;

/// <summary>AppStatus.Running → Visible；其他狀態 → Collapsed。</summary>
public sealed class AppStatusToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is AppStatus s && s == AppStatus.Running ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
