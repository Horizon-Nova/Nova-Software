using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Vaeron.HelixStudio.Components;

public partial class TableSkeletonView : UserControl
{
    public static readonly DependencyProperty ColumnsProperty =
        DependencyProperty.Register(nameof(Columns), typeof(IReadOnlyList<TableSkeletonColumn>), typeof(TableSkeletonView),
            new PropertyMetadata(null, OnLayoutPropertyChanged));

    public static readonly DependencyProperty RowCountProperty =
        DependencyProperty.Register(nameof(RowCount), typeof(int), typeof(TableSkeletonView),
            new PropertyMetadata(5, OnLayoutPropertyChanged));

    public static readonly DependencyProperty RowHeightProperty =
        DependencyProperty.Register(nameof(RowHeight), typeof(double), typeof(TableSkeletonView),
            new PropertyMetadata(38.0, OnLayoutPropertyChanged));

    public IReadOnlyList<TableSkeletonColumn> Columns
    {
        get => (IReadOnlyList<TableSkeletonColumn>)GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    public int RowCount
    {
        get => (int)GetValue(RowCountProperty);
        set => SetValue(RowCountProperty, value);
    }

    public double RowHeight
    {
        get => (double)GetValue(RowHeightProperty);
        set => SetValue(RowHeightProperty, value);
    }

    public TableSkeletonView()
    {
        InitializeComponent();
        Loaded += (_, _) => Rebuild();
    }

    private static void OnLayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((TableSkeletonView)d).Rebuild();

    private void Rebuild()
    {
        var columns = Columns;
        HeaderRow.ColumnDefinitions.Clear();
        HeaderRow.Children.Clear();
        RowsPanel.Children.Clear();
        if (columns is null || columns.Count == 0) return;

        foreach (var column in columns)
            HeaderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = column.Width });

        var headerDivider = new Border
        {
            BorderBrush = (Brush)FindResource("BorderDefaultBrush"),
            BorderThickness = new Thickness(0, 0, 0, 1),
        };
        Grid.SetColumnSpan(headerDivider, columns.Count);
        HeaderRow.Children.Add(headerDivider);

        for (var i = 0; i < columns.Count; i++)
        {
            if (string.IsNullOrEmpty(columns[i].Header)) continue;
            var header = new TextBlock
            {
                Text = columns[i].Header,
                Margin = columns[i].HeaderMargin,
                HorizontalAlignment = columns[i].Alignment,
                FontFamily = (FontFamily)FindResource("AppFontFamily"),
                FontSize = (double)FindResource("FontSizeXs"),
                FontWeight = FontWeights.Bold,
                Foreground = (Brush)FindResource("TextSecondaryBrush"),
            };
            Grid.SetColumn(header, i);
            HeaderRow.Children.Add(header);
        }

        for (var r = 0; r < RowCount; r++)
        {
            var row = new Grid { MinHeight = RowHeight };
            foreach (var column in columns)
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = column.Width });

            var rowDivider = new Border
            {
                BorderBrush = (Brush)FindResource("BorderSubtleBrush"),
                BorderThickness = new Thickness(0, 0, 0, 1),
                VerticalAlignment = VerticalAlignment.Bottom,
            };
            Grid.SetColumnSpan(rowDivider, columns.Count);
            row.Children.Add(rowDivider);

            for (var i = 0; i < columns.Count; i++)
            {
                if (columns[i].Chips.Count == 0) continue;

                var chipsPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = columns[i].Alignment,
                };
                foreach (var chip in columns[i].Chips)
                {
                    chipsPanel.Children.Add(new Border
                    {
                        Style = (Style)FindResource("SkeletonBarStyle"),
                        Width = chip.Width,
                        Height = chip.Height,
                        CornerRadius = new CornerRadius(chip.CornerRadius),
                        Margin = chip.Margin,
                    });
                }

                Grid.SetColumn(chipsPanel, i);
                row.Children.Add(chipsPanel);
            }

            RowsPanel.Children.Add(row);
        }
    }
}
