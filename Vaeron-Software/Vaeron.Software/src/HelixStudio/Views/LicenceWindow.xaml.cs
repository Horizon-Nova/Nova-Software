using System.Windows;
using System.Windows.Controls;
using HelixStudio.Logic;
using HelixStudio.Store;

namespace HelixStudio.Views;

/// <summary>授權這一頁：這台電腦還沒有授權時才出現</summary>
public partial class LicenceWindow : Window
{
    private readonly Dictionary<string, List<string>> icons = new ShellStore().QueryIconList();

    private readonly LicenceLogic.MomentItem moment = new();

    private readonly LicenceStore.LicenceView view = new LicenceStore().Licence();

    private readonly IReadOnlyList<DeviceStore.MarkItem> shown = new DeviceStore().Shown();

    /// <summary>備好這一頁</summary>
    public LicenceWindow()
    {
        InitializeComponent();

        Show();
    }

    /// <summary>複製申請碼給賣方</summary>
    /// <param name="sender">按下去的那一顆</param>
    /// <param name="e">這一次點擊</param>
    private async void CopyAsk(object sender, RoutedEventArgs e)
    {
        // 放進剪貼簿，按鈕換成已複製，第一步算完成
        Clipboard.SetText(((LicenceLogic.ActivateView)DataContext).Ask);

        moment.Copied = true;
        moment.AskCopied = true;

        Show();

        // 停一下再換回複製申請碼
        await Task.Delay(Copied);

        moment.Copied = false;

        Show();
    }

    /// <summary>在貼上授權那一格打字或貼上</summary>
    /// <param name="sender">那一格</param>
    /// <param name="e">這一次變動</param>
    private void Typed(object sender, TextChangedEventArgs e)
    {
        // 有東西就算第二步完成；上一次的錯誤跟著清掉
        moment.Pasted = Paste.Text.Trim().Length > 0;
        moment.PasteError = string.Empty;

        Show();
    }

    /// <summary>套用貼進來的那一把授權：驗過就完成啟用，直接進平台</summary>
    /// <param name="sender">按下去的那一顆</param>
    /// <param name="e">這一次點擊</param>
    private void Apply(object sender, RoutedEventArgs e)
    {
        // 存起來，這一刻再讀一次：簽章要過，兩邊都有的機器識別要全部對得上，還沒過到期日（當天仍可用）
        new LicenceStore().Take(Paste.Text);

        var paper = new LicenceStore().Licence();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var pass = new[] { paper.Sealed }
            .Where(_ => paper.Both > 0 && paper.Same == paper.Both)
            .Where(_ => paper.Papers.All(one => one.Expires.Length == 0 || (DateOnly.TryParse(one.Expires, out var until) && until >= today)))
            .DefaultIfEmpty(0)
            .First();

        // 驗不過就在欄位下說原因，貼上的字留著讓人對照
        moment.PasteError = new[] { "授權碼驗證失敗，請確認完整貼上，或向供應商重新索取一次。" }
            .Where(_ => pass == 0)
            .DefaultIfEmpty(string.Empty)
            .First();

        Show();

        // 驗過就進平台
        Enumerable
            .Repeat(0, 1)
            .Where(_ => pass > 0)
            .ToList()
            .ForEach(_ =>
            {
                new MainWindow().Show();

                Close();
            });
    }

    /// <summary>結束</summary>
    /// <param name="sender">按下去的那一顆</param>
    /// <param name="e">這一次點擊</param>
    private void Quit(object sender, RoutedEventArgs e) =>
        Application.Current.Shutdown();

    /// <summary>把這一頁重畫一次</summary>
    private new void Show() =>
        DataContext = LicenceLogic.Activate(view, shown, moment, icons);

    #region 對照

    /// <summary>已複製停多久：設計檔 1600ms</summary>
    private const int Copied = 1600;

    #endregion
}
