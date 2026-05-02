using System.Windows;
using System.Windows.Controls;

namespace Helix_Studio.Components;

/// <summary>輕量 Toast 通知控制項，可置於主視窗 <c>ToastHostPanel</c> 中顯示。</summary>
public partial class Toast : UserControl
{
    /// <summary>預設顯示時間（毫秒）。</summary>
    public const int DefaultDisplayMilliseconds = 5000;

    /// <summary>同一主機面板內同時可見之 Toast 數量上限。</summary>
    public const int MaxVisibleInHost          = 3;

    /// <summary>識別 <see cref="Title"/> 相依性屬性。</summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title),
        typeof(string),
        typeof(Toast),
        new PropertyMetadata(string.Empty, OnTitleOrMessageChanged));

    /// <summary>識別 <see cref="Message"/> 相依性屬性。</summary>
    public static readonly DependencyProperty MessageProperty = DependencyProperty.Register(
        nameof(Message),
        typeof(string),
        typeof(Toast),
        new PropertyMetadata(string.Empty, OnTitleOrMessageChanged));

    /// <summary>識別 <see cref="Severity"/> 相依性屬性。</summary>
    public static readonly DependencyProperty SeverityProperty = DependencyProperty.Register(
        nameof(Severity),
        typeof(ToastSeverity),
        typeof(Toast),
        new PropertyMetadata(ToastSeverity.Information));

    /// <summary>通知標題（可為空以隱藏標題列）。</summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>通知本文。</summary>
    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    /// <summary>通知嚴重程度。</summary>
    public ToastSeverity Severity
    {
        get => (ToastSeverity)GetValue(SeverityProperty);
        set => SetValue(SeverityProperty, value);
    }

    /// <summary>初始化控制項並於載入時更新標題可見度。</summary>
    public Toast()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateTitleVisibility();
    }

    private static void OnTitleOrMessageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Toast t) return;
        t.UpdateTitleVisibility();
    }

    private void UpdateTitleVisibility()
    {
        if (!IsLoaded) return;
        var has = !string.IsNullOrWhiteSpace(Title);
        TitleBlock.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
        MessageBlock.Margin     = new Thickness(0, has ? 4 : 0, 0, 0);
    }

    /// <summary>在指定視窗之 Toast 主機顯示僅本文之通知。</summary>
    public static void Show(Window? owner, string message, ToastSeverity severity = ToastSeverity.Warning, int displayMilliseconds = DefaultDisplayMilliseconds)
    {
        Show(owner, null, message, severity, displayMilliseconds);
    }

    /// <summary>在指定視窗之 Toast 主機顯示含標題與本文之通知。</summary>
    public static void Show(Window? owner, string? title, string message, ToastSeverity severity = ToastSeverity.Warning, int displayMilliseconds = DefaultDisplayMilliseconds)
    {
        if (string.IsNullOrEmpty(message)) return;
        owner ??= Application.Current?.MainWindow;
        if (owner?.FindName("ToastHostPanel") is not StackPanel host) return;

        var toast = new Toast
        {
            Title    = title ?? string.Empty,
            Message  = message,
            Severity = severity,
        };

        while (host.Children.Count >= MaxVisibleInHost)
        {
            host.Children.RemoveAt(0);
        }
        host.Children.Add(toast);
        _ = RemoveLaterAsync(toast, displayMilliseconds, host);
    }

    private static async Task RemoveLaterAsync(Toast toast, int displayMilliseconds, StackPanel host)
    {
        var ms = Math.Max(displayMilliseconds, 800);
        await Task.Delay(ms);
        _ = host.Dispatcher.InvokeAsync(() => host.Children.Remove(toast));
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        var host = Parent as Panel;
        host?.Children.Remove(this);
    }
}
