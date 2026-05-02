using System.Windows;
using System.Windows.Controls;
using Helix_Studio.ViewModels;

namespace Helix_Studio.Views;

/// <summary>主選單「首頁」分頁：系統與工作區概覽、模組捷徑。</summary>
public partial class OverviewPage : UserControl
{
    private readonly OverviewPageViewModel _viewModel;

    /// <summary>建立分頁、繫結 <see cref="OverviewPageViewModel"/> 並在載入時取快照、卸載時釋放。</summary>
    public OverviewPage()
    {
        InitializeComponent();

        _viewModel = new OverviewPageViewModel();
        DataContext = _viewModel;

        Loaded += (_, _) => _viewModel.LoadedCommand.Execute(null);
        Unloaded += (_, _) => _viewModel.Dispose();
    }

    private void GoToModules_Click(object sender, RoutedEventArgs e)
        => NavigateMainWindow("Modules");

    private void GoToSettings_Click(object sender, RoutedEventArgs e)
        => NavigateMainWindow("Settings");

    private void GoToWorkspaces_Click(object sender, RoutedEventArgs e)
        => NavigateMainWindow("Settings");

    private void GoToEventLog_Click(object sender, RoutedEventArgs e)
        => NavigateMainWindow("EventLog");

    private void OpenModule_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        if (btn.Tag is not string moduleId) return;
        NavigateMainWindow($"Module:{moduleId}");
    }

    private void NavigateMainWindow(string tag)
    {
        if (Window.GetWindow(this) is MainWindow mw)
            mw.NavigateTo(tag);
    }
}

