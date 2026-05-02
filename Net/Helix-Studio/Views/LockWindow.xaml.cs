using System.Windows;

namespace Helix_Studio.Views;

/// <summary>閒置逾時或手動鎖定時的簡單解鎖對話框。</summary>
public partial class LockWindow : Window
{
    /// <summary>僅建立視窗 UI；解鎖由 <c>DialogResult</c> 表達。</summary>
    public LockWindow()
    {
        InitializeComponent();
    }

    private void Unlock_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}


