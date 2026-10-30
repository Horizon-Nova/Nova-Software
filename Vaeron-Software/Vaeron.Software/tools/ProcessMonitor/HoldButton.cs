using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace ProcessMonitor;

/// <summary>按住 1 秒才算數的按鈕：按著時紅色由左往右填，放開沒滿就退回；滿了發出 Held</summary>
/// <remarks>結束、緊急關閉用。滑鼠左鍵或空白鍵按住都算；移出按鈕等於放開。</remarks>
public class HoldButton : Button
{
    /// <summary>填到哪裡：0 到 1</summary>
    public static readonly DependencyProperty ProgressProperty =
        DependencyProperty.Register(nameof(Progress), typeof(double), typeof(HoldButton), new PropertyMetadata(0.0));

    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    /// <summary>按滿 1 秒</summary>
    public event RoutedEventHandler? Held;

    private readonly DispatcherTimer ticker = new() { Interval = TimeSpan.FromMilliseconds(30) };

    private DateTime started;

    /// <summary>備好計時：每 30ms 填一次</summary>
    public HoldButton()
    {
        ticker.Tick += (_, _) =>
        {
            Progress = Math.Min(1, (DateTime.Now - started).TotalMilliseconds / 1000);
            if (Progress >= 1)
            {
                // 按滿了：放開按鈕（不用等手放開），監控台馬上換上結果
                ticker.Stop();
                Progress = 0;
                ReleaseMouseCapture();
                Held?.Invoke(this, new RoutedEventArgs());
            }
        };
    }

    /// <summary>按下去開始數</summary>
    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonDown(e);
        started = DateTime.Now;
        ticker.Start();
    }

    /// <summary>放開：沒滿就退回</summary>
    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonUp(e);
        ticker.Stop();
        Progress = 0;
    }

    /// <summary>移出按鈕等於放開</summary>
    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        ticker.Stop();
        Progress = 0;
    }

    /// <summary>空白鍵按住也算（重複觸發的不重算）</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Space)
        {
            e.Handled = true;
            if (!e.IsRepeat)
            {
                started = DateTime.Now;
                ticker.Start();
            }

            return;
        }

        base.OnKeyDown(e);
    }

    /// <summary>空白鍵放開：沒滿就退回</summary>
    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.Key == Key.Space)
        {
            e.Handled = true;
            ticker.Stop();
            Progress = 0;
            return;
        }

        base.OnKeyUp(e);
    }
}
