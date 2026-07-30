using System.Windows;
using System.Windows.Controls;

namespace Vaeron.HelixStudio.AppsManagement;

public partial class AppsManagementView : UserControl
{
    public AppsManagementView(AppsManagementViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
        => ((AppsManagementViewModel)DataContext).ApplyViewLoaded();

    private void OnUnloaded(object sender, RoutedEventArgs e)
        => ((AppsManagementViewModel)DataContext).ApplyViewUnloaded();

    /// <summary>聚焦搜尋框（供 Shell 導覽呼叫）</summary>
    public void FocusSearchBox() => SearchBox.Focus();

    /// <summary>PasswordBox 密碼變更同步回 ViewModel</summary>
    private void OnModuleKeyPasswordChanged(object sender, RoutedEventArgs e)
    {
        ((AppsManagementViewModel)DataContext).ModuleKeyInput = ModuleKeyPasswordBox.Password;
        ModuleKeyPlaceholder.Visibility = ModuleKeyPasswordBox.Password.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>解鎖 Modal 顯示時清空密碼框並取得焦點</summary>
    private void OnUnlockModalVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (UnlockModalOverlay.Visibility != Visibility.Visible) return;
        ModuleKeyPasswordBox.Password = "";
        ModuleKeyPlaceholder.Visibility = Visibility.Visible;
        ModuleKeyPasswordBox.Focus();
    }
}
