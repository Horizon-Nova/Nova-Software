using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Vaeron.HelixStudio.Internal.Runtime.Components;

/// <summary>換頁過渡用的通用骨架屏</summary>
public partial class SkeletonScreen : UserControl
{
    public static readonly DependencyProperty RowCountProperty =
        DependencyProperty.Register(nameof(RowCount), typeof(int), typeof(SkeletonScreen),
            new PropertyMetadata(6, OnRowCountChanged));

    public int RowCount
    {
        get => (int)GetValue(RowCountProperty);
        set => SetValue(RowCountProperty, value);
    }

    public SkeletonScreen()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    // 只需首次進入視覺樹時建置一次：後續 Rebuild 由 RowCountProperty 變更觸發，
    // 重複進出視覺樹不會改變 RowCount，Rebuild 的輸出是同一份，不需要每次 Loaded 都重跑。
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        Rebuild();
    }

    private static void OnRowCountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((SkeletonScreen)d).Rebuild();

    private void Rebuild()
    {
        RowsPanel.Children.Clear();
        for (var i = 0; i < RowCount; i++)
            RowsPanel.Children.Add(BuildRow());
    }

    private Grid BuildRow()
    {
        var row = new Grid { Margin = new Thickness(8, 10, 8, 10) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.4, GridUnitType.Star) });

        var dot = NewBar(20, 14, 7);
        Grid.SetColumn(dot, 0);

        var bar1 = NewBar(double.NaN, 12, 6, new Thickness(12, 0, 12, 0));
        bar1.HorizontalAlignment = HorizontalAlignment.Stretch;
        Grid.SetColumn(bar1, 1);

        var bar2 = NewBar(double.NaN, 12, 6, new Thickness(0, 0, 4, 0));
        bar2.HorizontalAlignment = HorizontalAlignment.Stretch;
        Grid.SetColumn(bar2, 2);

        row.Children.Add(dot);
        row.Children.Add(bar1);
        row.Children.Add(bar2);
        return row;
    }

    private Border NewBar(double width, double height, double cornerRadius, Thickness? margin = null) => new()
    {
        Style = (Style)FindResource("SkeletonBarStyle"),
        Width = width,
        Height = height,
        CornerRadius = new CornerRadius(cornerRadius),
        Margin = margin ?? new Thickness(0),
    };
}
