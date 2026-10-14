using System.Windows;
using System.Windows.Input;

namespace InSpecTool;

/// <summary>先確認再填：InSpec 問到這一欄時，讓操作員看一次、可以改，確認後才填進去</summary>
public partial class Confirm : Window
{
    /// <summary>備好這個視窗</summary>
    public Confirm()
    {
        InitializeComponent();
    }

    /// <summary>問一次：回傳操作員確認後的值</summary>
    /// <param name="owner">蓋在哪一個視窗上</param>
    /// <param name="name">欄位名稱</param>
    /// <param name="ask">InSpec 的提示文字</param>
    /// <param name="value">條碼帶出來的值</param>
    public static string Ask(Window? owner, string name, string ask, string value)
    {
        var box = new Confirm { Owner = owner };

        box.Heading.Text = $"確認{name}";
        box.Asked.Text = $"InSpec 提示「{ask}」";
        box.FieldName.Text = name;
        box.Value.Text = value;

        box.ShowDialog();

        return box.Value.Text.Trim();
    }

    /// <summary>打開：游標在值上、全選，直接打字就換掉</summary>
    /// <param name="sender">這個視窗</param>
    /// <param name="e">打開這一次</param>
    private void Ready(object sender, RoutedEventArgs e)
    {
        Value.Focus();
        Value.SelectAll();
    }

    /// <summary>確認填入</summary>
    /// <param name="sender">確認填入</param>
    /// <param name="e">這一次點擊</param>
    private void Done(object sender, RoutedEventArgs e) =>
        Close();

    /// <summary>沒有取消：Esc 不關</summary>
    /// <param name="sender">這個視窗</param>
    /// <param name="e">按下的鍵</param>
    private void Keys(object sender, KeyEventArgs e) =>
        e.Handled = e.Key == Key.Escape;
}
