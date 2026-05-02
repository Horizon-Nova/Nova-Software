using System.Windows;
using System.Windows.Media;

namespace Helix_Studio.Infrastructure.Localization;

/// <summary>整體字型縮放之資源鍵與套用邏輯。</summary>
public static class UiScale
{
    /// <summary>字型縮放係數之資源鍵。</summary>
    public const string ScaleResourceKey = "UiFontScale";

    /// <summary>目前字型縮放係數。</summary>
    public static double CurrentScale { get; private set; } = 1.0;

    /// <summary>套用指定縮放係數並更新主視窗內容之配置轉換。</summary>
    public static void Apply(double scale)
    {
        if (scale <= 0) scale = 1.0;

        CurrentScale = scale;
        Application.Current!.Resources[ScaleResourceKey] = scale;

        ApplyToMainWindow(scale);
    }

    /// <summary>依設定畫面之字型縮放索引套用預設係數。</summary>
    public static void ApplyByIndex(int fontScaleIndex)
        => Apply(fontScaleIndex switch
        {
            0 => 0.90,
            2 => 1.10,
            3 => 1.25,
            _ => 1.00,
        });

    private static void ApplyToMainWindow(double scale)
    {
        if (Application.Current?.MainWindow?.Content is not FrameworkElement root) return;
        root.LayoutTransform = new ScaleTransform(scale, scale);
    }
}
