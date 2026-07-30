using System.Windows;
using Vaeron.HelixStudio.Internal.Runtime.AppRuntime;

namespace Vaeron.HelixStudio;

/// <summary>闔螢幕顯示關閉選單的實際彈窗——PowerMenuRuntime.PowerMenuRequested 唯一訂閱者，
/// 訂閱接線見 App.xaml.cs。只負責「使用者選了什麼」，選完之後怎麼執行是呼叫方的事。</summary>
public partial class PowerMenuWindow : Window
{
    public PowerMenuAction SelectedAction { get; private set; } = PowerMenuAction.None;

    public PowerMenuWindow()
    {
        InitializeComponent();
    }

    private void OnSleepClick(object sender, RoutedEventArgs e) => CloseWith(PowerMenuAction.Sleep);

    private void OnHibernateClick(object sender, RoutedEventArgs e) => CloseWith(PowerMenuAction.Hibernate);

    private void OnShutdownClick(object sender, RoutedEventArgs e) => CloseWith(PowerMenuAction.Shutdown);

    private void OnCancelClick(object sender, RoutedEventArgs e) => Close();

    private void CloseWith(PowerMenuAction action)
    {
        SelectedAction = action;
        Close();
    }
}
