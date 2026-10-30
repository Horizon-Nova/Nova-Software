using System.Windows;
using ProcessMonitor.Logic;

namespace ProcessMonitor;

/// <summary>HelixStudio 不在前面時，螢幕右下角跳出的小視窗：沒有回應的那一支，可以打開監控台、緊急關閉、緩和</summary>
/// <remarks>不搶焦點（ShowActivated=False）、一直在最上面；監控台每秒把卡最久的那一支交進來，按鈕做的事也交回監控台。</remarks>
public partial class Alert : Window
{
    private readonly Action open;

    private readonly Func<string, Task> end;

    private readonly Func<string, Task> ease;

    private readonly Action<string> dismiss;

    /// <summary>備好按鈕要做的事；貼在工作區右下角</summary>
    /// <param name="open">打開監控台</param>
    /// <param name="end">緊急關閉那一支</param>
    /// <param name="ease">緩和（或取消緩和）那一支</param>
    /// <param name="dismiss">按叉叉：這一支這一次不再跳</param>
    internal Alert(Action open, Func<string, Task> end, Func<string, Task> ease, Action<string> dismiss)
    {
        InitializeComponent();
        this.open = open;
        this.end = end;
        this.ease = ease;
        this.dismiss = dismiss;

        SizeChanged += (_, _) =>
        {
            Left = SystemParameters.WorkArea.Right - ActualWidth;
            Top = SystemParameters.WorkArea.Bottom - ActualHeight;
        };
    }

    /// <summary>換上這一秒卡最久的那一支</summary>
    /// <param name="hang">那一支</param>
    internal void Show(MonitorLogic.HangItem hang)
    {
        DataContext = hang;
        EaseButton.IsEnabled = hang.HogKey.Length > 0;
        EaseWord.Visibility = hang.Eased ? Visibility.Collapsed : Visibility.Visible;
        UneaseWord.Visibility = hang.Eased ? Visibility.Visible : Visibility.Collapsed;
        Show();
    }

    /// <summary>打開監控台：主視窗叫到前面、開程式監控</summary>
    private void OpenBoard(object sender, RoutedEventArgs e)
    {
        Hide();
        open();
    }

    /// <summary>按住 1 秒緊急關閉</summary>
    private async void EndHung(object sender, RoutedEventArgs e) =>
        await end(((MonitorLogic.HangItem)DataContext).Key);

    /// <summary>緩和或取消緩和</summary>
    private async void Ease(object sender, RoutedEventArgs e) =>
        await ease(((MonitorLogic.HangItem)DataContext).Key);

    /// <summary>叉叉：這一支這一次不再跳</summary>
    private void Dismiss(object sender, RoutedEventArgs e)
    {
        dismiss(((MonitorLogic.HangItem)DataContext).Key);
        Hide();
    }
}
