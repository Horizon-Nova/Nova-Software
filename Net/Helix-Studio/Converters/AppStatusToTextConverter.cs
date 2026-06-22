using System.Globalization;
using System.Windows.Data;
using Helix_Studio.Models;
using Helix_Studio.Services;

namespace Helix_Studio.Converters;

/// <summary>AppStatus → 顯示文字。值對映以查表表達，文字依當前語言取得。</summary>
public sealed class AppStatusToTextConverter : IValueConverter
{
    private static readonly Dictionary<AppStatus, string> Keys = new()
    {
        [AppStatus.Stopped]  = "L.Status.Stopped",
        [AppStatus.Starting] = "L.Status.Starting",
        [AppStatus.Running]  = "L.Status.Running",
        [AppStatus.Error]    = "L.Status.Error",
        [AppStatus.Locked]   = "L.Status.Locked",
    };

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is AppStatus s && Keys.TryGetValue(s, out var key)
            ? LocalizationService.Instance.GetString(key)
            : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
