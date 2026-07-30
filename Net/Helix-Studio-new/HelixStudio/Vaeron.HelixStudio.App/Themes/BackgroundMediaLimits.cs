using System.IO;
using System.Windows.Media.Imaging;
using Vaeron.Kernel;

namespace Vaeron.HelixStudio.Themes;

public sealed record BackgroundMediaEvaluation(TimeSpan Duration);

/// <summary>背景素材的軟體底線：主題不得使軟體無回應（藍圖_樣式系統 底線 3）</summary>
public static class BackgroundMediaLimits
{
    /// <summary>解壓後總畫素上限，超過視為過重</summary>
    private const long MaxTotalPixels = 400_000_000;

    /// <summary>檔案大小上限</summary>
    private const long MaxFileBytes = 64L * 1024 * 1024;

    /// <summary>回傳是否在上限內，以及檔案自述的播放長度（無法判定時為 Zero）</summary>
    public static Result<BackgroundMediaEvaluation> Evaluate(string path)
    {
        try
        {
            if (new FileInfo(path).Length > MaxFileBytes)
                return Result<BackgroundMediaEvaluation>.Fail(ResultKind.ValidationFailed, "檔案大小超過上限");
        }
        catch (IOException ex)
        {
            return Result<BackgroundMediaEvaluation>.Fail(ResultKind.Unavailable, ex.Message);
        }

        if (!string.Equals(Path.GetExtension(path), ".gif", StringComparison.OrdinalIgnoreCase))
            return Result<BackgroundMediaEvaluation>.Ok(new BackgroundMediaEvaluation(TimeSpan.Zero));

        return EvaluateGif(path);
    }

    private static Result<BackgroundMediaEvaluation> EvaluateGif(string path)
    {
        GifBitmapDecoder decoder;
        try
        {
            decoder = new GifBitmapDecoder(new Uri(path), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.None);
        }
        catch (FileFormatException) { return Result<BackgroundMediaEvaluation>.Ok(new BackgroundMediaEvaluation(TimeSpan.Zero)); }
        catch (IOException) { return Result<BackgroundMediaEvaluation>.Ok(new BackgroundMediaEvaluation(TimeSpan.Zero)); }
        catch (NotSupportedException) { return Result<BackgroundMediaEvaluation>.Ok(new BackgroundMediaEvaluation(TimeSpan.Zero)); }

        if (decoder.Frames.Count == 0) return Result<BackgroundMediaEvaluation>.Ok(new BackgroundMediaEvaluation(TimeSpan.Zero));

        var first = decoder.Frames[0];
        if ((long)first.PixelWidth * first.PixelHeight * decoder.Frames.Count > MaxTotalPixels)
            return Result<BackgroundMediaEvaluation>.Fail(ResultKind.ValidationFailed, "解壓後總畫素超過上限");

        var total = TimeSpan.Zero;
        foreach (var frame in decoder.Frames) total += FrameDelay(frame, static _ => { });
        return Result<BackgroundMediaEvaluation>.Ok(new BackgroundMediaEvaluation(total));
    }

    /// <summary>幀延遲存在 GIF 的圖形控制延伸區塊，單位 1/100 秒。查詢失敗時不影響整體評估，回傳預設值即可。</summary>
    private static TimeSpan FrameDelay(BitmapFrame frame, Action<Exception> onFailure)
    {
        const int defaultMs = 100;
        if (frame.Metadata is not BitmapMetadata metadata) return TimeSpan.FromMilliseconds(defaultMs);

        try
        {
            if (metadata.GetQuery("/grctlext/Delay") is ushort hundredths && hundredths > 0)
                return TimeSpan.FromMilliseconds(hundredths * 10);
        }
        catch (NotSupportedException ex) { onFailure(ex); }
        catch (ArgumentException ex) { onFailure(ex); }

        return TimeSpan.FromMilliseconds(defaultMs);
    }
}
