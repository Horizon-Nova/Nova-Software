using System.Windows.Media;

namespace Vaeron.HelixStudio.Themes;

/// <summary>強調色 OKLCH 運算與套用</summary>
public static class AccentColorService
{
    public const double DefaultHue = 231;
    public const double DefaultChroma = 0.11;

    public static void Apply(System.Windows.ResourceDictionary target, double hue, double chroma, bool isDark)
    {
        double tintL = isDark ? 0.28 : 0.92;
        double textL = isDark ? 0.82 : 0.32;
        double flashL = isDark ? 0.32 : 0.90;
        double borderL = isDark ? 0.55 : 0.6;
        double labelL = isDark ? 0.62 : 0.5;
        double solidL = isDark ? 0.62 : 0.55;
        double solidHoverL = isDark ? 0.68 : 0.48;

        target["AccentSolidBrush"] = Brush(solidL, chroma, hue);
        target["AccentSolidHoverBrush"] = Brush(solidHoverL, chroma, hue);
        target["AccentOnSolidBrush"] = new SolidColorBrush(Colors.White);
        target["AccentTextBrush"] = Brush(textL, chroma * 0.9, hue);
        target["AccentTintBrush"] = Brush(tintL, chroma * 0.5, hue);
        target["AccentTintSoftBrush"] = Brush(tintL, chroma * 0.25, hue);
        target["AccentFlashBrush"] = Brush(flashL, chroma * 0.7, hue);
        target["AccentFlashSoftBrush"] = Brush(flashL, chroma * 0.35, hue);
        target["AccentBorderBrush"] = Brush(borderL, chroma * 0.8, hue);
        target["AccentLabelBrush"] = Brush(labelL, chroma * 0.9, hue);
        target["AccentLabelStrongBrush"] = Brush(labelL * 0.85, chroma, hue);
    }

    private static SolidColorBrush Brush(double lightness, double chroma, double hue)
        => new(OklchToColor(lightness, chroma, hue));

    public static Color OklchToColor(double lightness, double chroma, double hueDegrees)
    {
        var hr = hueDegrees * Math.PI / 180.0;
        var a = chroma * Math.Cos(hr);
        var b = chroma * Math.Sin(hr);

        var lPrime = lightness + 0.3963377774 * a + 0.2158037573 * b;
        var mPrime = lightness - 0.1055613458 * a - 0.0638541728 * b;
        var sPrime = lightness - 0.0894841775 * a - 1.2914855480 * b;

        var l = lPrime * lPrime * lPrime;
        var m = mPrime * mPrime * mPrime;
        var s = sPrime * sPrime * sPrime;

        var r = 4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s;
        var g = -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s;
        var bLin = -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s;

        return Color.FromRgb(ToSrgbByte(r), ToSrgbByte(g), ToSrgbByte(bLin));
    }

    private static byte ToSrgbByte(double linear)
    {
        var c = Math.Clamp(linear, 0.0, 1.0);
        var srgb = c <= 0.0031308 ? 12.92 * c : 1.055 * Math.Pow(c, 1 / 2.4) - 0.055;
        return (byte)Math.Round(Math.Clamp(srgb, 0.0, 1.0) * 255);
    }
}
