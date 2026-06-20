using System.Globalization;
using System.Windows.Data;

namespace Helix_Studio.Converters;

/// <summary>
/// DateTime → 相對時間文字（13 秒前 / 5 分鐘前 / 1 小時前 / 2 天前）。
/// 區間門檻以資料表驅動，超過 30 天顯示絕對日期。
/// </summary>
public sealed class RelativeTimeConverter : IValueConverter
{
    private sealed record Unit(long MaxSeconds, long DivisorSeconds, string Suffix);

    // 由小到大；命中第一個 elapsed < MaxSeconds 的區間。
    private static readonly Unit[] Units =
    [
        new(60,            1,     " 秒前"),
        new(3600,          60,    " 分鐘前"),
        new(86400,         3600,  " 小時前"),
        new(30L * 86400,   86400, " 天前"),
    ];

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DateTime time) return string.Empty;

        var elapsed = DateTime.Now - time;
        long seconds = (long)Math.Max(0, elapsed.TotalSeconds);

        foreach (var unit in Units)
        {
            if (seconds < unit.MaxSeconds)
            {
                long n = Math.Max(1, seconds / unit.DivisorSeconds);
                return n + unit.Suffix;
            }
        }
        return time.ToString("yyyy/MM/dd", culture);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
