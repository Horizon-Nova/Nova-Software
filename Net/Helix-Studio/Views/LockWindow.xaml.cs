using System.Windows;

namespace Helix_Studio.Views;

/// <summary>
/// 鎖定視窗的檢視層。
/// </summary>
public partial class LockWindow : Window
{
    /// <summary>
    /// 建立鎖定視窗。
    /// </summary>
    public LockWindow()
    {
        InitializeComponent();
    }

    private void Unlock_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
