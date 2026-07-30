using System.Windows;
using System.Windows.Controls;

namespace Vaeron.HelixStudio.Internal.Runtime.Themes;

/// <summary>TextBox 提示文字附加屬性。放在 Runtime（非 App）——Feature 的 View XAML 以
/// clr-namespace 直接引用附加屬性所在型別，Feature 專案不能反向依賴 App 根專案。</summary>
public static class TextBoxPlaceholder
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached(
        "Text", typeof(string), typeof(TextBoxPlaceholder), new PropertyMetadata(""));

    // WPF 附加屬性的 Get{Name}/Set{Name} 簽名由框架規定，XAML 編譯器靠這個精確形狀辨識附加屬性，
    // 回傳型別不能改成 Result——這裡不是「可能失敗的操作回傳裸型別」，是框架讀取器要求的固定介面。
#pragma warning disable VAE003
    public static string GetText(TextBox element) => (string)element.GetValue(TextProperty);
#pragma warning restore VAE003
    public static void SetText(TextBox element, string value) => element.SetValue(TextProperty, value);
}
