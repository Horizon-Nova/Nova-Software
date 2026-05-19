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
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title),
        typeof(string),
        typeof(InputModalDialog),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty MessageProperty = DependencyProperty.Register(
        nameof(Message),
        typeof(string),
        typeof(InputModalDialog),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty Input1LabelProperty = DependencyProperty.Register(
        nameof(Input1Label),
        typeof(string),
        typeof(InputModalDialog),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty Input2LabelProperty = DependencyProperty.Register(
        nameof(Input2Label),
        typeof(string),
        typeof(InputModalDialog),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty Input1TextProperty = DependencyProperty.Register(
        nameof(Input1Text),
        typeof(string),
        typeof(InputModalDialog),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty Input2TextProperty = DependencyProperty.Register(
        nameof(Input2Text),
        typeof(string),
        typeof(InputModalDialog),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty CancelTextProperty = DependencyProperty.Register(
        nameof(CancelText),
        typeof(string),
        typeof(InputModalDialog),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ConfirmTextProperty = DependencyProperty.Register(
        nameof(ConfirmText),
        typeof(string),
        typeof(InputModalDialog),
        new PropertyMetadata(string.Empty));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public string Input1Label
    {
        get => (string)GetValue(Input1LabelProperty);
        set => SetValue(Input1LabelProperty, value);
    }

    public string Input2Label
    {
        get => (string)GetValue(Input2LabelProperty);
        set => SetValue(Input2LabelProperty, value);
    }

    public string Input1Text
    {
        get => (string)GetValue(Input1TextProperty);
        set => SetValue(Input1TextProperty, value);
    }

    public string Input2Text
    {
        get => (string)GetValue(Input2TextProperty);
        set => SetValue(Input2TextProperty, value);
    }

    public string CancelText
    {
        get => (string)GetValue(CancelTextProperty);
        set => SetValue(CancelTextProperty, value);
    }

    public string ConfirmText
    {
        get => (string)GetValue(ConfirmTextProperty);
        set => SetValue(ConfirmTextProperty, value);
    }

    private TaskCompletionSource<bool>? _tcs;

    public InputModalDialog()
    {
        InitializeComponent();
        PreviewKeyDown += OnRootPreviewKeyDown;
    }

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

