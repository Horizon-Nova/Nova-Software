using System.Windows;
using System.Windows.Controls;
using Helix_Studio.ViewModels;

namespace Helix_Studio.Views;

/// <summary>事件日誌分頁：篩選、重新整理與清除（經使用者確認）。</summary>
public partial class EventLogPage : UserControl
{
    private readonly EventLogPageViewModel _viewModel;

    /// <summary>建立分頁、繫結 <see cref="EventLogPageViewModel"/>；載入時重新整理、卸載時取消訂閱。</summary>
    public EventLogPage()
    {
        InitializeComponent();

        _viewModel = new EventLogPageViewModel();
        DataContext = _viewModel;

        Loaded += (_, _) => _viewModel.RefreshCommand.Execute(null);
        Unloaded += (_, _) => _viewModel.Dispose();
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        var r = MessageBox.Show(
            "確定要清除所有記錄嗎？",
            "確認",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (r != MessageBoxResult.Yes) return;

        _viewModel.ClearCommand.Execute(null);
    }
}

