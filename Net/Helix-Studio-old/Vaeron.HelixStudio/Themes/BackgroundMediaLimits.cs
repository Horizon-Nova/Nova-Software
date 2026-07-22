using System.IO;
using System.Windows.Media.Imaging;

namespace Vaeron.HelixStudio.Themes;

/// <summary>背景素材的軟體底線：主題不得使軟體無回應（藍圖_樣式系統 底線 3）</summary>
public static class BackgroundMediaLimits
{
    /// <summary>解壓後總畫素上限，超過視為過重</summary>
    private const long MaxTotalPixels = 400_000_000;

    /// <summary>檔案大小上限</summary>
    private const long MaxFileBytes = 64L * 1024 * 1024;

    /// <summary>回傳是否在上限內，以及檔案自述的播放長度（無法判定時為 Zero）</summary>
    public static (bool WithinLimit, TimeSpan Duration) Evaluate(string path)
    {
        try
        {
            if (new FileInfo(path).Length > MaxFileBytes) return (false, TimeSpan.Zero);
        }
        catch (IOException) { return (false, TimeSpan.Zero); }

        if (!string.Equals(Path.GetExtension(path), ".gif", StringComparison.OrdinalIgnoreCase))
            return (true, TimeSpan.Zero);

        return EvaluateGif(path);
    }

    private static (bool WithinLimit, TimeSpan Duration) EvaluateGif(string path)
    {
        GifBitmapDecoder decoder;
        try
        {
            decoder = new GifBitmapDecoder(new Uri(path), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.None);
        }
        catch (FileFormatException) { return (true, TimeSpan.Zero); }
        catch (IOException) { return (true, TimeSpan.Zero); }
        catch (NotSupportedException) { return (true, TimeSpan.Zero); }

        if (decoder.Frames.Count == 0) return (true, TimeSpan.Zero);

        var first = decoder.Frames[0];
        if ((long)first.PixelWidth * first.PixelHeight * decoder.Frames.Count > MaxTotalPixels)
            return (false, TimeSpan.Zero);

        var total = TimeSpan.Zero;
        foreach (var frame in decoder.Frames) total += FrameDelay(frame);
        return (true, total);
    }

    /// <summary>幀延遲存在 GIF 的圖形控制延伸區塊，單位 1/100 秒</summary>
    private static TimeSpan FrameDelay(BitmapFrame frame)
    {
        const int defaultMs = 100;
        if (frame.Metadata is not BitmapMetadata metadata) return TimeSpan.FromMilliseconds(defaultMs);

        try
        {
            if (metadata.GetQuery("/grctlext/Delay") is ushort hundredths && hundredths > 0)
                return TimeSpan.FromMilliseconds(hundredths * 10);
        }
        catch (NotSupportedException) { }
        catch (ArgumentException) { }

        return TimeSpan.FromMilliseconds(defaultMs);
    }
}
