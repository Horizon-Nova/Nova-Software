using System.Windows;
using System.Windows.Controls;

namespace Vaeron.HelixStudio.Themes;

/// <summary>TextBox 提示文字附加屬性</summary>
public static class TextBoxPlaceholder
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached(
        "Text", typeof(string), typeof(TextBoxPlaceholder), new PropertyMetadata(""));

    public static string GetText(TextBox element) => (string)element.GetValue(TextProperty);
    public static void SetText(TextBox element, string value) => element.SetValue(TextProperty, value);
}
