using System.Windows;
using ProcessMonitor.Logic;

namespace ProcessMonitor;

/// <summary>限制對話框：一支程式的上傳上限、不准連網路、CPU 上限、記憶體上限</summary>
/// <remarks>蓋住整個主視窗（遮罩連標題列、側欄一起變暗），對話框在正中間；套用交給監控台，做不到就把原因寫在按鈕上面。</remarks>
public partial class Limit : Window
{
    /// <summary>按套用：上傳、不准連網路、CPU、記憶體的字；回傳做不到的原因（都做到是空字串）</summary>
    private readonly Func<string, bool, string, string, Task<string>> apply;

    /// <summary>照主視窗的位置與大小蓋上去</summary>
    /// <param name="owner">主視窗</param>
    /// <param name="view">那一支現在設的上限與量到的值</param>
    /// <param name="apply">按套用時做的事</param>
    internal Limit(Window owner, MonitorLogic.LimitView view, Func<string, bool, string, string, Task<string>> apply)
    {
        InitializeComponent();
        this.apply = apply;
        DataContext = view;
        Owner = owner;

        // 蓋住主視窗（最大化時照它實際的範圍）
        var bounds = owner.WindowState == WindowState.Maximized ? SystemParameters.WorkArea : new Rect(owner.Left, owner.Top, owner.ActualWidth, owner.ActualHeight);
        Left = bounds.Left;
        Top = bounds.Top;
        Width = bounds.Width;
        Height = bounds.Height;

        Loaded += (_, _) => Up.Focus();
    }

    /// <summary>套用：交給監控台，做到就關掉，做不到把原因寫出來</summary>
    private async void Apply(object sender, RoutedEventArgs e)
    {
        ApplyButton.IsEnabled = false;
        var fault = await apply(Up.Text, Block.IsChecked == true, Cpu.Text, Mem.Text);
        ApplyButton.IsEnabled = true;

        Fault.Text = fault;
        Fault.Visibility = fault.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (fault.Length == 0)
        {
            Close();
        }
    }

    /// <summary>取消、叉叉、Esc：不改就關掉</summary>
    private void Cancel(object sender, RoutedEventArgs e) =>
        Close();
}
