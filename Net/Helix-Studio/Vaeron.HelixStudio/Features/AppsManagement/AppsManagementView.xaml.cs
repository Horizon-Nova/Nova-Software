using System.Windows;
using System.Windows.Controls;

namespace Vaeron.HelixStudio.Features.AppsManagement;

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

    /// <summary>頂部列搜尋按鈕（跨畫面導覽）呼叫的入口——Shell 持有本 View 的具體型別參考，
    /// 導覽到本畫面後直接呼叫，不需要再另開一個跨 Feature 的事件機制。</summary>
    public void FocusSearchBox() => SearchBox.Focus();

    /// <summary>PasswordBox.Password 不是 DependencyProperty，無法直接雙向綁定——這是 WPF
    /// 刻意的安全設計（避免明文密碼長駐在綁定系統與記憶體），因此改用 code-behind 接
    /// PasswordChanged 事件同步回 ViewModel，這是「UI 事件接線」的合法範圍
    /// （feature-structure.md：code-behind 僅 UI 事件接線），不是業務邏輯外洩到 View。</summary>
    private void OnModuleKeyPasswordChanged(object sender, RoutedEventArgs e)
    {
        ((AppsManagementViewModel)DataContext).ModuleKeyInput = ModuleKeyPasswordBox.Password;
        ModuleKeyPlaceholder.Visibility = ModuleKeyPasswordBox.Password.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Modal 每次開啟都要是空白輸入框——ViewModel 端已經把 ModuleKeyInput 重設為空字串
    /// （見 ApplyOpenUnlockPopup），但 PasswordBox 的視覺內容不受那次綁定影響（本來就沒有綁），
    /// 這裡在 Modal 變為可見時手動清空＋取得焦點，讓使用者不用先清掉上次殘留的字才能打。</summary>
    private void OnUnlockModalVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (UnlockModalOverlay.Visibility != Visibility.Visible) return;
        ModuleKeyPasswordBox.Password = "";
        ModuleKeyPlaceholder.Visibility = Visibility.Visible;
        ModuleKeyPasswordBox.Focus();
    }
}
