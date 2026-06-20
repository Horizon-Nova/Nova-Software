using System;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Shapes;
using WpfMedia = System.Windows.Media;
using HorizonNova.AITrainingModule.ViewModels;

namespace HorizonNova.AITrainingModule.Views;

public class StringEqConverter : IValueConverter
{
    public static readonly StringEqConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => string.Equals(value as string, parameter as string, StringComparison.Ordinal);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? (object)(parameter as string ?? string.Empty) : System.Windows.Data.Binding.DoNothing;
}

public partial class AITrainingView : System.Windows.Controls.UserControl
{
    private readonly AITrainingViewModel _vm;

    private static WpfMedia.Color C(byte r, byte g, byte b) => WpfMedia.Color.FromRgb(r, g, b);

    private static readonly WpfMedia.Color TrainColor = C(0x0F, 0x4C, 0x81);
    private static readonly WpfMedia.Color ValColor   = C(0xC8, 0x84, 0x00);
    private static readonly WpfMedia.Color MapColor   = C(0x2E, 0x7D, 0x4F);
    private static readonly WpfMedia.Color GridColor  = C(0xE3, 0xE6, 0xEC);
    private static readonly WpfMedia.Color LabelColor = C(0x8A, 0x94, 0xA6);

    public AITrainingView(AITrainingViewModel vm)
    {
        _vm = vm;
        DataContext = vm;
        InitializeComponent();

        vm.Metrics.CollectionChanged += OnMetricsChanged;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AITrainingViewModel.EpochProgress))
                UpdateProgressBar();
            if (e.PropertyName == nameof(AITrainingViewModel.StatusCode))
                UpdateStatusBadge();
        };

        vm.LogLines.CollectionChanged += (_, _) =>
            Dispatcher.InvokeAsync(() => LogScroll.ScrollToEnd());

        UpdateStatusBadge();
    }

    private void OnMetricsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RedrawLossChart();
        RedrawMapChart();
    }

    private void UpdateProgressBar()
    {
        var parent = ProgressFill.Parent as UIElement;
        double trackWidth = parent is FrameworkElement fe ? fe.ActualWidth : 0;
        ProgressFill.Width = Math.Max(0, trackWidth * _vm.EpochProgress);
    }

    private void UpdateStatusBadge()
    {
        (string text, WpfMedia.Color dot, WpfMedia.Color bg, WpfMedia.Color border) = _vm.StatusCode switch
        {
            "training" => ("訓練中",  C(0x0F,0x4C,0x81), C(0xE7,0xEE,0xF5), C(0x0F,0x4C,0x81)),
            "done"     => ("已完成",  C(0x2E,0x7D,0x4F), C(0xE3,0xF0,0xE8), C(0x2E,0x7D,0x4F)),
            "error"    => ("錯誤",    C(0xB3,0x26,0x1E), C(0xF8,0xDE,0xDB), C(0xB3,0x26,0x1E)),
            _          => ("空閒",    C(0x8A,0x94,0xA6), C(0xFA,0xFB,0xFC), C(0xE3,0xE6,0xEC)),
        };
        StatusLabel.Text        = text;
        StatusDot.Fill          = new WpfMedia.SolidColorBrush(dot);
        StatusBadge.Background  = new WpfMedia.SolidColorBrush(bg);
        StatusBadge.BorderBrush = new WpfMedia.SolidColorBrush(border);
    }

    private void LossCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => RedrawLossChart();

    private void RedrawLossChart()
    {
        LossCanvas.Children.Clear();
        double w = LossCanvas.ActualWidth, h = LossCanvas.ActualHeight;
        if (w < 10 || h < 10) return;

        var metrics = _vm.Metrics.ToList();
        if (metrics.Count < 2) return;

        double maxLoss = metrics.Max(m => Math.Max(m.TrainLoss, m.ValLoss));
        maxLoss = Math.Max(maxLoss, 0.1) * 1.1;

        DrawGrid(LossCanvas, w, h);

        var trainPts = new WpfMedia.PointCollection();
        var valPts   = new WpfMedia.PointCollection();
        for (int i = 0; i < metrics.Count; i++)
        {
            double x = w * i / (metrics.Count - 1);
            trainPts.Add(new Point(x, h * (1.0 - metrics[i].TrainLoss / maxLoss)));
            valPts.Add(new Point(x, h * (1.0 - metrics[i].ValLoss   / maxLoss)));
        }

        LossCanvas.Children.Add(MakeLine(trainPts, TrainColor, 2));
        LossCanvas.Children.Add(MakeLine(valPts,   ValColor,   2, dashed: true));

        for (int i = 1; i <= 3; i++)
        {
            double val = maxLoss * i / 4.0;
            double y   = h * (1.0 - (double)i / 4.0);
            var tb = new TextBlock
            {
                Text       = val.ToString("F2"),
                FontSize   = 9,
                Foreground = new WpfMedia.SolidColorBrush(LabelColor),
            };
            Canvas.SetLeft(tb, 3);
            Canvas.SetTop(tb,  y - 8);
            LossCanvas.Children.Add(tb);
        }
    }

    private void MapCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => RedrawMapChart();

    private void RedrawMapChart()
    {
        MapCanvas.Children.Clear();
        double w = MapCanvas.ActualWidth, h = MapCanvas.ActualHeight;
        if (w < 10 || h < 10) return;

        var metrics = _vm.Metrics.ToList();
        if (metrics.Count < 2) return;

        DrawGrid(MapCanvas, w, h);

        var pts = new WpfMedia.PointCollection();
        for (int i = 0; i < metrics.Count; i++)
            pts.Add(new Point(w * i / (metrics.Count - 1), h * (1.0 - metrics[i].mAP50)));

        MapCanvas.Children.Add(MakeLine(pts, MapColor, 2));
    }

    private static void DrawGrid(Canvas canvas, double w, double h)
    {
        var brush = new WpfMedia.SolidColorBrush(GridColor) { Opacity = 0.7 };
        for (int i = 1; i <= 3; i++)
        {
            double y = h * i / 4.0;
            canvas.Children.Add(new Line
            {
                X1 = 0, Y1 = y, X2 = w, Y2 = y,
                Stroke = brush, StrokeThickness = 1,
                StrokeDashArray = new WpfMedia.DoubleCollection { 4, 4 },
            });
        }
    }

    private static Polyline MakeLine(WpfMedia.PointCollection pts, WpfMedia.Color color, double thickness, bool dashed = false)
    {
        var pl = new Polyline
        {
            Points          = pts,
            Stroke          = new WpfMedia.SolidColorBrush(color),
            StrokeThickness = thickness,
            StrokeLineJoin  = WpfMedia.PenLineJoin.Round,
        };
        if (dashed)
            pl.StrokeDashArray = new WpfMedia.DoubleCollection { 5, 3 };
        return pl;
    }
}
