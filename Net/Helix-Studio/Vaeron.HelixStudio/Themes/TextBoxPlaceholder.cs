using System.Windows;
using System.Windows.Controls;

namespace Vaeron.HelixStudio.Themes;

/// <summary>標準 TextBox 沒有內建的提示文字（WinUI 的 PlaceholderText 這裡沒有對應物），
/// 用附加屬性補一個，讓 AppTextBoxStyle 的 Template 可以疊一層提示文字——空搜尋框
/// 沒有任何線索告訴使用者要打什麼，這是全 App 通用的問題，不是單一畫面的事，
/// 所以做成附加屬性放共用 Themes，不是各畫面各自刻一份。</summary>
public static class TextBoxPlaceholder
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached(
        "Text", typeof(string), typeof(TextBoxPlaceholder), new PropertyMetadata(""));

    public static string GetText(TextBox element) => (string)element.GetValue(TextProperty);
    public static void SetText(TextBox element, string value) => element.SetValue(TextProperty, value);
}
