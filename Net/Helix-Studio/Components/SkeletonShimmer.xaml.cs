using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Helix_Studio.Components;

/// <summary>
/// 可重用的 Skeleton Shimmer 元件（對應 HTML <c>.skeleton-item::after</c> 掃光效果）。
/// </summary>
public partial class SkeletonShimmer : UserControl
{
    private const double ShimmerBandWidthRatio = 0.6;
    private static readonly TimeSpan ShimmerDuration = TimeSpan.FromSeconds(1.4);
    private static readonly Color FallbackBaseColor = Color.FromRgb(0xE6, 0xEA, 0xF2);

    private DoubleAnimation? _bandAnimation;
    private DoubleAnimation? _brushAnimation;
    private LinearGradientBrush? _shimmerBrush;
    private TranslateTransform? _brushTransform;
    private bool _layoutHooked;

    /// <summary>
    /// 定義 <see cref="IsActive"/> 依賴屬性。
    /// </summary>
    public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(
        nameof(IsActive),
        typeof(bool),
        typeof(SkeletonShimmer),
        new PropertyMetadata(false, OnIsActiveChanged));

    /// <summary>
    /// 定義 <see cref="SkeletonWidth"/> 依賴屬性。
    /// </summary>
    public static readonly DependencyProperty SkeletonWidthProperty = DependencyProperty.Register(
        nameof(SkeletonWidth),
        typeof(double),
        typeof(SkeletonShimmer),
        new PropertyMetadata(96d, static (d, _) => ((SkeletonShimmer)d).ScheduleShimmerRestart()));

    /// <summary>
    /// 定義 <see cref="SkeletonHeight"/> 依賴屬性。
    /// </summary>
    public static readonly DependencyProperty SkeletonHeightProperty = DependencyProperty.Register(
        nameof(SkeletonHeight),
        typeof(double),
        typeof(SkeletonShimmer),
        new PropertyMetadata(12d, static (d, _) => ((SkeletonShimmer)d).ScheduleShimmerRestart()));

    /// <summary>
    /// 定義 <see cref="CornerRadius"/> 依賴屬性。
    /// </summary>
    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
        nameof(CornerRadius),
        typeof(CornerRadius),
        typeof(SkeletonShimmer),
        new PropertyMetadata(new CornerRadius(4)));

    /// <summary>
    /// 定義 <see cref="UseStretch"/> 依賴屬性。
    /// </summary>
    public static readonly DependencyProperty UseStretchProperty = DependencyProperty.Register(
        nameof(UseStretch),
        typeof(bool),
        typeof(SkeletonShimmer),
        new PropertyMetadata(false, static (d, _) => ((SkeletonShimmer)d).ScheduleShimmerRestart()));

    /// <summary>
    /// 定義 <see cref="BaseBackground"/> 依賴屬性。
    /// </summary>
    public static readonly DependencyProperty BaseBackgroundProperty = DependencyProperty.Register(
        nameof(BaseBackground),
        typeof(Brush),
        typeof(SkeletonShimmer),
        new PropertyMetadata(null, static (d, _) => ((SkeletonShimmer)d).ScheduleShimmerRestart()));

    /// <summary>
    /// 是否啟用掃光動畫。
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
    /// 圓角半徑。
    /// </summary>
    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    /// <summary>
    /// 是否延展至可用寬度。
    /// </summary>
    public bool UseStretch
    {
        get => (bool)GetValue(UseStretchProperty);
        set => SetValue(UseStretchProperty, value);
    }

    /// <summary>
    /// 骨架底色。
    /// </summary>
    public Brush? BaseBackground
    {
        get => (Brush?)GetValue(BaseBackgroundProperty);
        set => SetValue(BaseBackgroundProperty, value);
    }

    /// <summary>
    /// 初始化 Skeleton Shimmer 元件。
    /// </summary>
    public SkeletonShimmer()
    {
        InitializeComponent();
        ShellBorder.SizeChanged += (_, _) => ScheduleShimmerRestart();
        Loaded += (_, _) =>
        {
            BaseBackground ??=
                (TryFindResource("SkeletonBase") as Brush)
                ?? (TryFindResource("SurfaceSubtle") as Brush)
                ?? new SolidColorBrush(FallbackBaseColor);

            ScheduleShimmerRestart();
        };
        IsVisibleChanged += (_, e) =>
        {
            if (e.NewValue is true)
            {
                ScheduleShimmerRestart();
                return;
            }

            StopShimmerAnimation();
        };
        Unloaded += (_, _) => StopShimmerAnimation();
    }

    private static void OnIsActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SkeletonShimmer shimmer)
        {
            shimmer.ScheduleShimmerRestart();
        }
    }

    private void ScheduleShimmerRestart()
    {
        Dispatcher.BeginInvoke(RestartShimmerAnimation, DispatcherPriority.Loaded);
        Dispatcher.BeginInvoke(RestartShimmerAnimation, DispatcherPriority.Render);
    }

    private void RestartShimmerAnimation()
    {
        StopShimmerAnimation();

        if (!IsActive || !IsVisible)
        {
            RestoreStaticBackground();
            return;
        }

        var hostWidth = ShellBorder.ActualWidth;
        if (hostWidth <= 0)
        {
            HookLayoutUpdated();
            return;
        }

        UnhookLayoutUpdated();
        StartShimmer(hostWidth);
    }

    private void HookLayoutUpdated()
    {
        if (_layoutHooked)
        {
            return;
        }

        _layoutHooked = true;
        LayoutUpdated += OnLayoutUpdatedForShimmer;
    }

    private void UnhookLayoutUpdated()
    {
        if (!_layoutHooked)
        {
            return;
        }

        _layoutHooked = false;
        LayoutUpdated -= OnLayoutUpdatedForShimmer;
    }

    private void OnLayoutUpdatedForShimmer(object? sender, EventArgs e)
    {
        if (!IsActive || !IsVisible || ShellBorder.ActualWidth <= 0)
        {
            return;
        }

        UnhookLayoutUpdated();
        StartShimmer(ShellBorder.ActualWidth);
    }

    private void StartShimmer(double hostWidth)
    {
        var bandWidth = hostWidth * ShimmerBandWidthRatio;
        ShimmerBand.Width = bandWidth;
        ShimmerTransform.X = -hostWidth;

        var easing = new QuadraticEase { EasingMode = EasingMode.EaseInOut };

        _bandAnimation = new DoubleAnimation
        {
            From = -hostWidth,
            To = hostWidth * 2,
            Duration = ShimmerDuration,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = easing,
        };
        ShimmerTransform.BeginAnimation(TranslateTransform.XProperty, _bandAnimation);

        var baseColor = ResolveBaseColor();
        var highlightColor = Color.FromArgb(
            255,
            (byte)Math.Min(255, baseColor.R + 12),
            (byte)Math.Min(255, baseColor.G + 16),
            (byte)Math.Min(255, baseColor.B + 22));

        _brushTransform = new TranslateTransform();
        _shimmerBrush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0.5),
            EndPoint = new Point(1, 0.5),
            MappingMode = BrushMappingMode.RelativeToBoundingBox,
            RelativeTransform = _brushTransform,
        };
        _shimmerBrush.GradientStops.Add(new GradientStop(baseColor, 0));
        _shimmerBrush.GradientStops.Add(new GradientStop(highlightColor, 0.5));
        _shimmerBrush.GradientStops.Add(new GradientStop(baseColor, 1));
        ShellBorder.Background = _shimmerBrush;

        _brushAnimation = new DoubleAnimation
        {
            From = -1.2,
            To = 1.2,
            Duration = ShimmerDuration,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = easing,
        };
        _brushTransform.BeginAnimation(TranslateTransform.XProperty, _brushAnimation);
    }

    private Color ResolveBaseColor()
        => BaseBackground is SolidColorBrush solid ? solid.Color : FallbackBaseColor;

    private void RestoreStaticBackground()
    {
        ShellBorder.ClearValue(Border.BackgroundProperty);
        if (BaseBackground is not null)
        {
            ShellBorder.Background = BaseBackground;
        }
    }

    private void StopShimmerAnimation()
    {
        UnhookLayoutUpdated();
        ShimmerTransform.BeginAnimation(TranslateTransform.XProperty, null);
        _brushTransform?.BeginAnimation(TranslateTransform.XProperty, null);
        _bandAnimation = null;
        _brushAnimation = null;
        _shimmerBrush = null;
        _brushTransform = null;
        RestoreStaticBackground();
    }
}

