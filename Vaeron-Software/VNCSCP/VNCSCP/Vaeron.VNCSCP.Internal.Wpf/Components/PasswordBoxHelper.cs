using System.Windows;
using System.Windows.Controls;

namespace Vaeron.VNCSCP.Internal.Wpf.Components;

/// <summary>密碼框是否已有內容。PasswordBox.Password 不是相依屬性，浮水印樣式無從直接繫結，改由本附加屬性承載。</summary>
public static class PasswordBoxHelper
{
    public static readonly DependencyProperty HasTextProperty = DependencyProperty.RegisterAttached(
        "HasText", typeof(bool), typeof(PasswordBoxHelper), new PropertyMetadata(false));

    static PasswordBoxHelper()
        => EventManager.RegisterClassHandler(
            typeof(PasswordBox), PasswordBox.PasswordChangedEvent, new RoutedEventHandler(OnPasswordChanged));

    public static bool GetHasText(DependencyObject element) => (bool)element.GetValue(HasTextProperty);

    public static void SetHasText(DependencyObject element, bool value) => element.SetValue(HasTextProperty, value);

    /// <summary>觸發靜態建構子，讓類別處理常式在第一個密碼框出現前就註冊完成。</summary>
    public static void EnsureRegistered()
    {
    }

    private static void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        var box = (PasswordBox)sender;
        SetHasText(box, box.Password.Length > 0);
    }
}
