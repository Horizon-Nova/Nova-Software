using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Helix_Studio.Components;

/// <summary>主視窗用之全畫面覆蓋式確認／取消對話框元件。</summary>
public partial class ModalDialog : UserControl
{
    /// <summary>識別 <see cref="Title"/> 相依性屬性。</summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title),
        typeof(string),
        typeof(ModalDialog),
        new PropertyMetadata(string.Empty));

    /// <summary>識別 <see cref="Message"/> 相依性屬性。</summary>
    public static readonly DependencyProperty MessageProperty = DependencyProperty.Register(
        nameof(Message),
        typeof(string),
        typeof(ModalDialog),
        new PropertyMetadata(string.Empty));

    /// <summary>識別 <see cref="Description"/> 相依性屬性。</summary>
    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        nameof(Description),
        typeof(string),
        typeof(ModalDialog),
        new PropertyMetadata(string.Empty));

    /// <summary>識別 <see cref="CancelText"/> 相依性屬性。</summary>
    public static readonly DependencyProperty CancelTextProperty = DependencyProperty.Register(
        nameof(CancelText),
        typeof(string),
        typeof(ModalDialog),
        new PropertyMetadata(string.Empty));

    /// <summary>識別 <see cref="ConfirmText"/> 相依性屬性。</summary>
    public static readonly DependencyProperty ConfirmTextProperty = DependencyProperty.Register(
        nameof(ConfirmText),
        typeof(string),
        typeof(ModalDialog),
        new PropertyMetadata(string.Empty));

    /// <summary>對話框標題。</summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>主要訊息文字。</summary>
    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    /// <summary>補充說明文字。</summary>
    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <summary>取消按鈕文字；空字串時可隱藏取消按鈕。</summary>
    public string CancelText
    {
        get => (string)GetValue(CancelTextProperty);
        set => SetValue(CancelTextProperty, value);
    }

    /// <summary>確認按鈕文字。</summary>
    public string ConfirmText
    {
        get => (string)GetValue(ConfirmTextProperty);
        set => SetValue(ConfirmTextProperty, value);
    }

    private TaskCompletionSource<bool>? _tcs;
    private Button? _confirmButton;
    private Button? _cancelButton;

    /// <summary>初始化對話框為隱藏且不可點擊。</summary>
    public ModalDialog()
    {
        InitializeComponent();
        Visibility = Visibility.Collapsed;
        IsHitTestVisible = false;
        PreviewKeyDown += OnRootPreviewKeyDown;
    }

    /// <summary>以非阻塞方式顯示對話框並回傳使用者是否確認。</summary>
    public Task<bool> ShowDialogAsync(
        string title,
        string message,
        string? description = null,
        string confirmText = "確認",
        string? cancelText = "取消")
    {
        if (_tcs is not null)
        {
            throw new InvalidOperationException("已有對話方塊在顯示，請先關閉再開啟新對話方塊。");
        }

        _tcs = new TaskCompletionSource<bool>();
        Title = title;
        Message = message;
        Description = description ?? string.Empty;
        ConfirmText = string.IsNullOrEmpty(confirmText) ? "確認" : confirmText;
        CancelText = cancelText ?? string.Empty;

        CacheFooterButtons();
        if (_cancelButton is not null)
        {
            _cancelButton.Visibility = string.IsNullOrEmpty(cancelText) ? Visibility.Collapsed : Visibility.Visible;
        }

        if (_confirmButton is not null)
        {
            _confirmButton.IsDefault = true;
        }
        if (_cancelButton is not null)
        {
            _cancelButton.IsDefault = false;
        }

        IsHitTestVisible = true;
        Visibility = Visibility.Visible;
        _ = Dispatcher.BeginInvoke(() => _confirmButton?.Focus(), DispatcherPriority.Input);

        return _tcs.Task;
    }

    private void OnRootPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        e.Handled = true;
        Complete(false);
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => Complete(false);

    private void ConfirmButton_Click(object sender, RoutedEventArgs e) => Complete(true);

    private void Complete(bool confirmed)
    {
        if (_tcs is null) return;
        _tcs.TrySetResult(confirmed);
        _tcs = null;

        Title = string.Empty;
        Message = string.Empty;
        Description = string.Empty;
        CancelText = string.Empty;
        ConfirmText = string.Empty;

        if (_cancelButton is not null)
        {
            _cancelButton.Visibility = Visibility.Visible;
        }
        if (_confirmButton is not null)
        {
            _confirmButton.IsDefault = false;
        }

        Visibility = Visibility.Collapsed;
        IsHitTestVisible = false;
    }

    private void CacheFooterButtons()
    {
        if (_cancelButton is not null && _confirmButton is not null) return;

        var buttons = CollectButtonsDepthFirst(this);
        if (buttons.Count < 2) return;
        _cancelButton = buttons[0];
        _confirmButton = buttons[1];
    }

    private static List<Button> CollectButtonsDepthFirst(DependencyObject root)
    {
        var list = new List<Button>();
        Walk(root);
        return list;

        void Walk(DependencyObject o)
        {
            var n = VisualTreeHelper.GetChildrenCount(o);
            for (var i = 0; i < n; i++)
            {
                var c = VisualTreeHelper.GetChild(o, i);
                if (c is Button b) list.Add(b);
                Walk(c);
            }
        }
    }
}

