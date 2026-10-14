using System.Text;
using System.Windows;
using System.Windows.Controls;
using Vaeron.Kernel.Secrets;

namespace Vaeron.VNCSCP.Internal.Wpf.Components;

/// <summary>可切換遮罩與明文的密碼輸入框。兩個輸入框內容互相同步，切換只是對調可見性。</summary>
public partial class RevealablePasswordBox : UserControl
{
    public static readonly DependencyProperty WatermarkProperty = DependencyProperty.Register(
        nameof(Watermark), typeof(string), typeof(RevealablePasswordBox), new PropertyMetadata(""));

    public static readonly DependencyProperty RevealedWatermarkProperty = DependencyProperty.Register(
        nameof(RevealedWatermark), typeof(string), typeof(RevealablePasswordBox), new PropertyMetadata(""));

    public RevealablePasswordBox()
    {
        InitializeComponent();
        MaskedBox.PasswordChanged += OnMaskedChanged;
        RevealedBox.TextChanged += OnRevealedChanged;
        Unloaded += OnUnloaded;
    }

    public string Watermark
    {
        get => (string)GetValue(WatermarkProperty);
        set => SetValue(WatermarkProperty, value);
    }

    public string RevealedWatermark
    {
        get => (string)GetValue(RevealedWatermarkProperty);
        set => SetValue(RevealedWatermarkProperty, value);
    }

    /// <summary>目前輸入的密碼；空白時序列為空，呼叫端拿不到緩衝區。</summary>
    public SecretBuffer? TryGetSecretBuffer()
        => new[] { MaskedBox.Password }
            .Where(password => password.Length > 0)
            .Select(password => new SecretBuffer(Encoding.UTF8.GetBytes(password)))
            .FirstOrDefault();

    public void Clear()
    {
        MaskedBox.Clear();
        RevealedBox.Clear();
    }

    /// <summary>把已存的密碼填回遮罩框，行為與其他欄位一致：展開即帶出目前值。</summary>
    public void PrefillMasked(string plaintext) => MaskedBox.Password = plaintext;

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        MaskedBox.PasswordChanged -= OnMaskedChanged;
        RevealedBox.TextChanged -= OnRevealedChanged;
        Unloaded -= OnUnloaded;
    }

    private void OnMaskedChanged(object sender, RoutedEventArgs e) => RevealedBox.Text = MaskedBox.Password;

    private void OnRevealedChanged(object sender, TextChangedEventArgs e) => MaskedBox.Password = RevealedBox.Text;

    private void OnToggleRevealClick(object sender, RoutedEventArgs e)
    {
        var masked = MaskedBox.Visibility;
        MaskedBox.Visibility = RevealedBox.Visibility;
        RevealedBox.Visibility = masked;
    }
}
