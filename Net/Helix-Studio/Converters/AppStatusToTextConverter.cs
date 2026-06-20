using System.Globalization;
using System.Windows.Data;
using Helix_Studio.Models;

namespace Helix_Studio.Converters;

/// <summary>AppStatus → 顯示文字。值對映以查表表達。</summary>
public sealed class AppStatusToTextConverter : IValueConverter
{
    private static readonly Dictionary<AppStatus, string> Texts = new()
    {
        [AppStatus.Running] = "執行中",
        [AppStatus.Stopped] = "已停止",
        [AppStatus.Error] = "錯誤",
    };

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is AppStatus s && Texts.TryGetValue(s, out var t) ? t : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
