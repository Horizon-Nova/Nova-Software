using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Helix_Studio.Components;

/// <summary>
/// 主視窗用之全畫面覆蓋式輸入對話框元件。
/// </summary>
public partial class InputModalDialog : UserControl
{
    /// <summary>
    /// 對話框標題的相依屬性。
    /// </summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title),
        typeof(string),
        typeof(InputModalDialog),
        new PropertyMetadata(string.Empty));

    /// <summary>
    /// 對話框訊息的相依屬性。
    /// </summary>
    public static readonly DependencyProperty MessageProperty = DependencyProperty.Register(
        nameof(Message),
        typeof(string),
        typeof(InputModalDialog),
        new PropertyMetadata(string.Empty));

    /// <summary>
    /// 第一個輸入欄位標籤的相依屬性。
    /// </summary>
    public static readonly DependencyProperty Input1LabelProperty = DependencyProperty.Register(
        nameof(Input1Label),
        typeof(string),
        typeof(InputModalDialog),
        new PropertyMetadata(string.Empty));

    /// <summary>
    /// 第二個輸入欄位標籤的相依屬性。
    /// </summary>
    public static readonly DependencyProperty Input2LabelProperty = DependencyProperty.Register(
        nameof(Input2Label),
        typeof(string),
        typeof(InputModalDialog),
        new PropertyMetadata(string.Empty));

    /// <summary>
    /// 第一個輸入欄位文字的相依屬性。
    /// </summary>
    public static readonly DependencyProperty Input1TextProperty = DependencyProperty.Register(
        nameof(Input1Text),
        typeof(string),
        typeof(InputModalDialog),
        new PropertyMetadata(string.Empty));

    /// <summary>
    /// 第二個輸入欄位文字的相依屬性。
    /// </summary>
    public static readonly DependencyProperty Input2TextProperty = DependencyProperty.Register(
        nameof(Input2Text),
        typeof(string),
        typeof(InputModalDialog),
        new PropertyMetadata(string.Empty));

    /// <summary>
    /// 取消按鈕文字的相依屬性。
    /// </summary>
    public static readonly DependencyProperty CancelTextProperty = DependencyProperty.Register(
        nameof(CancelText),
        typeof(string),
        typeof(InputModalDialog),
        new PropertyMetadata(string.Empty));

    /// <summary>
    /// 確認按鈕文字的相依屬性。
    /// </summary>
    public static readonly DependencyProperty ConfirmTextProperty = DependencyProperty.Register(
        nameof(ConfirmText),
        typeof(string),
        typeof(InputModalDialog),
        new PropertyMetadata(string.Empty));

    /// <summary>
    /// 對話框標題。
    /// </summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>
    /// 對話框訊息內容。
    /// </summary>
    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    /// <summary>
    /// 第一個輸入欄位標籤。
    /// </summary>
    public string Input1Label
    {
        get => (string)GetValue(Input1LabelProperty);
        set => SetValue(Input1LabelProperty, value);
    }

    /// <summary>
    /// 第二個輸入欄位標籤。
    /// </summary>
    public string Input2Label
    {
        get => (string)GetValue(Input2LabelProperty);
        set => SetValue(Input2LabelProperty, value);
    }

    /// <summary>
    /// 第一個輸入欄位內容。
    /// </summary>
    public string Input1Text
    {
        get => (string)GetValue(Input1TextProperty);
        set => SetValue(Input1TextProperty, value);
    }

    /// <summary>
    /// 第二個輸入欄位內容。
    /// </summary>
    public string Input2Text
    {
        get => (string)GetValue(Input2TextProperty);
        set => SetValue(Input2TextProperty, value);
    }

    /// <summary>
    /// 取消按鈕文字。
    /// </summary>
    public string CancelText
    {
        get => (string)GetValue(CancelTextProperty);
        set => SetValue(CancelTextProperty, value);
    }

    /// <summary>
    /// 確認按鈕文字。
    /// </summary>
    public string ConfirmText
    {
        get => (string)GetValue(ConfirmTextProperty);
        set => SetValue(ConfirmTextProperty, value);
    }

    private TaskCompletionSource<bool>? _tcs;

    /// <summary>
    /// 建立輸入對話框元件。
    /// </summary>
    public InputModalDialog()
    {
        InitializeComponent();
        PreviewKeyDown += OnRootPreviewKeyDown;
    }

    /// <summary>
    /// 顯示輸入對話框並等待使用者確認結果。
    /// </summary>
    /// <param name="title">對話框標題。</param>
    /// <param name="message">對話框訊息。</param>
    /// <param name="input1Label">第一個輸入欄位標籤。</param>
    /// <param name="input2Label">第二個輸入欄位標籤。</param>
    /// <param name="confirmText">確認按鈕文字。</param>
    /// <param name="cancelText">取消按鈕文字。</param>
    /// <returns>使用者是否確認。</returns>
    public Task<bool> ShowDialogAsync(
        string title,
        string message,
        string input1Label,
        string input2Label,
        string confirmText,
        string cancelText)
    {
        if (_tcs is not null)
        {
            throw new InvalidOperationException("已有對話方塊在顯示，請先關閉再開啟新對話方塊。");
        }

        _tcs = new TaskCompletionSource<bool>();

        Title = title ?? string.Empty;
        Message = message ?? string.Empty;
        Input1Label = input1Label ?? string.Empty;
        Input2Label = input2Label ?? string.Empty;
        ConfirmText = string.IsNullOrWhiteSpace(confirmText) ? "確認" : confirmText;
        CancelText = string.IsNullOrWhiteSpace(cancelText) ? "取消" : cancelText;

        Visibility = Visibility.Visible;
        IsHitTestVisible = true;
        _ = Dispatcher.BeginInvoke(() => Input1Box.Focus(), DispatcherPriority.Input);

        return _tcs.Task;
    }

    private void OnRootPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        e.Handled = true;
        Complete(false);
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => Complete(false);

    private void ConfirmButton_Click(object sender, RoutedEventArgs e) => Complete(true);

    private void Complete(bool confirmed)
    {
        if (_tcs is null)
        {
            return;
        }

        _tcs.TrySetResult(confirmed);
        _tcs = null;

        Title = string.Empty;
        Message = string.Empty;
        Input1Label = string.Empty;
        Input2Label = string.Empty;
        CancelText = string.Empty;
        ConfirmText = string.Empty;

        Visibility = Visibility.Collapsed;
        IsHitTestVisible = false;
    }
}
