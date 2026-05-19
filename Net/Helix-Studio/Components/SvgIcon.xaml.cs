using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Helix_Studio.Components;

/// <summary>
/// SVG 圖示元件。
/// </summary>
public partial class SvgIcon : UserControl
{
    /// <summary>
    /// 識別 <see cref="Source"/> 相依性屬性。
    /// </summary>
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source),
        typeof(Uri),
        typeof(SvgIcon),
        new PropertyMetadata(null, null, CoerceSource));

    static object? CoerceSource(DependencyObject _, object? baseValue)
    {
        if (baseValue is not Uri uri)
            return baseValue;

        if (uri.IsAbsoluteUri && string.Equals(uri.Scheme, "pack", StringComparison.OrdinalIgnoreCase))
            return uri;

        var path = uri.OriginalString.TrimStart('/');

        var normalized = path.Replace('\\', '/');
        var candidates = new[]
        {
            $"pack://application:,,,/{normalized}",
            $"pack://application:,,,/{normalized.ToLowerInvariant()}",
        };

        var resolved = candidates
            .Select(static value => new Uri(value, UriKind.Absolute))
            .FirstOrDefault(static candidate => Application.GetResourceStream(candidate) is not null);

        return resolved ?? new Uri(candidates[0], UriKind.Absolute);
    }

    /// <summary>
    /// 識別 <see cref="IconSize"/> 相依性屬性。
    /// </summary>
    public static readonly DependencyProperty IconSizeProperty = DependencyProperty.Register(
        nameof(IconSize),
        typeof(double),
        typeof(SvgIcon),
        new PropertyMetadata(16d, OnIconSizeChanged));

    static void OnIconSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not SvgIcon icon || e.NewValue is not double size)
            return;

        icon.Width = size;
        icon.Height = size;
    }

    /// <summary>
    /// 取得或設定 SVG 資源 URI。
    /// </summary>
    public Uri? Source
    {
        get => (Uri?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>
    /// 取得或設定圖示大小。
    /// </summary>
    public double IconSize
    {
        get => (double)GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    /// <summary>
    /// 建立 SVG 圖示元件。
    /// </summary>
    public SvgIcon()
    {
        InitializeComponent();
        Width = IconSize;
        Height = IconSize;
    }
}
