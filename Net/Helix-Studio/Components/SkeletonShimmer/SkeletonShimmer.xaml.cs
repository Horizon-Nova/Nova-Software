using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Helix_Studio.Components;

/// <summary>
/// 可重用的 Skeleton Shimmer 元件。
/// 用於在載入或切換期間顯示骨架與掃光效果。
/// </summary>
public partial class SkeletonShimmer : UserControl
{
    /// <summary>
    /// 識別 <see cref="IsActive"/> 相依性屬性。
    /// </summary>
    public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(
        nameof(IsActive),
        typeof(bool),
        typeof(SkeletonShimmer),
        new PropertyMetadata(false));

    /// <summary>
    /// 識別 <see cref="SkeletonWidth"/> 相依性屬性。
    /// </summary>
    public static readonly DependencyProperty SkeletonWidthProperty = DependencyProperty.Register(
        nameof(SkeletonWidth),
        typeof(double),
        typeof(SkeletonShimmer),
        new PropertyMetadata(96d));

    /// <summary>
    /// 識別 <see cref="SkeletonHeight"/> 相依性屬性。
    /// </summary>
    public static readonly DependencyProperty SkeletonHeightProperty = DependencyProperty.Register(
        nameof(SkeletonHeight),
        typeof(double),
        typeof(SkeletonShimmer),
        new PropertyMetadata(12d));

    /// <summary>
    /// 識別 <see cref="CornerRadius"/> 相依性屬性。
    /// </summary>
    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
        nameof(CornerRadius),
        typeof(CornerRadius),
        typeof(SkeletonShimmer),
        new PropertyMetadata(new CornerRadius(3)));

    /// <summary>
    /// 識別 <see cref="HighlightOpacity"/> 相依性屬性。
    /// </summary>
    public static readonly DependencyProperty HighlightOpacityProperty = DependencyProperty.Register(
        nameof(HighlightOpacity),
        typeof(double),
        typeof(SkeletonShimmer),
        new PropertyMetadata(0.85d));

    /// <summary>
    /// 識別 <see cref="UseStretch"/> 相依性屬性。
    /// </summary>
    public static readonly DependencyProperty UseStretchProperty = DependencyProperty.Register(
        nameof(UseStretch),
        typeof(bool),
        typeof(SkeletonShimmer),
        new PropertyMetadata(false));

    /// <summary>
    /// 識別 <see cref="BaseBackground"/> 相依性屬性。
    /// </summary>
    public static readonly DependencyProperty BaseBackgroundProperty = DependencyProperty.Register(
        nameof(BaseBackground),
        typeof(Brush),
        typeof(SkeletonShimmer),
        new PropertyMetadata(null));

    /// <summary>
    /// 是否顯示骨架動畫。
    /// </summary>
    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    /// <summary>
    /// 骨架寬度。
    /// </summary>
    public double SkeletonWidth
    {
        get => (double)GetValue(SkeletonWidthProperty);
        set => SetValue(SkeletonWidthProperty, value);
    }

    /// <summary>
    /// 骨架高度。
    /// </summary>
    public double SkeletonHeight
    {
        get => (double)GetValue(SkeletonHeightProperty);
        set => SetValue(SkeletonHeightProperty, value);
    }

    /// <summary>
    /// 骨架圓角。
    /// </summary>
    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    /// <summary>
    /// 亮帶不透明度。
    /// </summary>
    public double HighlightOpacity
    {
        get => (double)GetValue(HighlightOpacityProperty);
        set => SetValue(HighlightOpacityProperty, value);
    }

    /// <summary>
    /// 是否延伸填滿父容器寬度（適用於全寬區塊型骨架，例如清單列、導航按鈕）。
    /// 設為 True 時，<see cref="SkeletonWidth"/> 會被忽略。
    /// </summary>
    public bool UseStretch
    {
        get => (bool)GetValue(UseStretchProperty);
        set => SetValue(UseStretchProperty, value);
    }

    /// <summary>
    /// 骨架的底色筆刷（例如 SurfaceSidebarDeep）。
    /// 設定後骨架元件自帶底色，不需要額外的 Border 包裝。
    /// 不設定時為透明（適合疊加在其他元素上使用）。
    /// </summary>
    public Brush BaseBackground
    {
        get => (Brush)GetValue(BaseBackgroundProperty);
        set => SetValue(BaseBackgroundProperty, value);
    }

    /// <summary>
    /// 初始化 Skeleton Shimmer 元件。
    /// </summary>
    public SkeletonShimmer()
    {
        InitializeComponent();
    }
}
