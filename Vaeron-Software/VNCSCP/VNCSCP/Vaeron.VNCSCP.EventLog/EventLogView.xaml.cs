using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Vaeron.VNCSCP.EventLog;

public partial class EventLogView : UserControl
{
    private static readonly TimeSpan CopyFeedbackDuration = TimeSpan.FromMilliseconds(1200);

    public EventLogView(EventLogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
        => ((EventLogViewModel)DataContext).ApplyViewLoaded();

    private void OnUnloaded(object sender, RoutedEventArgs e)
        => ((EventLogViewModel)DataContext).ApplyViewUnloaded();

    private void OnExportClick(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "匯出事件日誌",
            Filter = "JSON 檔 (*.json)|*.json",
            FileName = $"事件日誌_{DateTimeOffset.Now:yyyyMMdd_HHmmss}.json"
        };

        foreach (var path in new[] { dialog }.Where(picker => picker.ShowDialog() is true).Select(picker => picker.FileName))
        {
            ((EventLogViewModel)DataContext).ApplyExport(path);
        }
    }

    /// <summary>複製明細到剪貼簿，並給短暫視覺回饋。</summary>
    private void OnCopyDetailClick(object sender, RoutedEventArgs e)
    {
        foreach (var row in new[] { sender }.OfType<FrameworkElement>().Select(element => element.DataContext).OfType<EventRow>())
        {
            Clipboard.SetText(row.Detail);
            row.IsJustCopied = true;

            var timer = new DispatcherTimer { Interval = CopyFeedbackDuration, Tag = row };
            timer.Tick += OnCopyFeedbackTick;
            timer.Start();
        }
    }

    private void OnCopyFeedbackTick(object? sender, EventArgs e)
    {
        var timer = (DispatcherTimer)sender!;
        timer.Tick -= OnCopyFeedbackTick;
        timer.Stop();

        foreach (var row in new[] { timer.Tag }.OfType<EventRow>())
        {
            row.IsJustCopied = false;
        }
    }
}
