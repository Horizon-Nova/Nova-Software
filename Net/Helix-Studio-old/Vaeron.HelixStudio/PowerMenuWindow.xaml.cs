using System.Windows;

namespace Vaeron.HelixStudio;

/// <summary>闔螢幕顯示關閉選單的實際彈窗——PowerMenuRuntime.PowerMenuRequested 唯一訂閱者，
/// 訂閱接線見 App.xaml.cs。只負責「使用者選了什麼」，選完之後怎麼執行是呼叫方的事。</summary>
public partial class PowerMenuWindow : Window
{
    public string? SelectedAction { get; private set; }

    public PowerMenuWindow()
    {
        InitializeComponent();
    }

    private void OnSleepClick(object sender, RoutedEventArgs e) => CloseWith("睡眠");

    private void OnHibernateClick(object sender, RoutedEventArgs e) => CloseWith("休眠");

    private void OnShutdownClick(object sender, RoutedEventArgs e) => CloseWith("關機");

    private void OnCancelClick(object sender, RoutedEventArgs e) => Close();

    private void CloseWith(string action)
    {
        SelectedAction = action;
        Close();
    }
}
