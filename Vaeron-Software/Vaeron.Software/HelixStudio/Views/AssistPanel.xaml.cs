using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HelixStudio.Logic;
using HelixStudio.Store;

namespace HelixStudio.Views;

/// <summary>AI 助理這一頁</summary>
public partial class AssistPanel : UserControl
{
    private readonly ShellStore store = new();

    private readonly Dictionary<string, List<string>> icons;

    private string draft = string.Empty;

    private bool thinking;

    private string where = string.Empty;

    private string cite = string.Empty;

    /// <summary>有人要收起這個面板</summary>
    public event EventHandler? Closed;

    /// <summary>備好這一頁</summary>
    public AssistPanel()
    {
        InitializeComponent();

        icons = store.QueryIcons();
    }

    /// <summary>打開 AI 助理：記下現在停在哪一個畫面，問答才知道要附哪一份來源</summary>
    /// <param name="screen">現在停在哪一個畫面</param>
    /// <param name="source">回答要附的來源</param>
    public void Open(string screen, string source)
    {
        where = screen;

        // 設定關掉「回答附上來源文件」時就不附
        cite = new[] { source }.Where(_ => ShellLogic.Setup.Cite).DefaultIfEmpty(string.Empty).First();

        Show();
    }

    /// <summary>收起面板</summary>
    private void Close(object sender, RoutedEventArgs e)
    {
        // 設定關掉「保留問答紀錄」時，收起面板就把這台裝置上的紀錄清掉
        Enumerable.Repeat(true, 1).Where(_ => !ShellLogic.Setup.KeepLog).ToList().ForEach(_ => store.DropMessages());

        Closed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>按 Esc：收起面板</summary>
    private void Escape(object sender, KeyEventArgs e) =>
        new[] { e.Key }
            .Where(one => one == Key.Escape)
            .ToList()
            .ForEach(_ =>
            {
                e.Handled = true;

                Closed?.Invoke(this, EventArgs.Empty);
            });

    /// <summary>打字</summary>
    private void Type(object sender, TextChangedEventArgs e)
    {
        draft = Draft.Text;

        Show();
    }

    /// <summary>在輸入框按 Enter 就送出</summary>
    private void Send(object sender, KeyEventArgs e) =>
        new[] { e.Key }
            .Where(one => one == Key.Enter)
            .Where(_ => Keyboard.Modifiers != ModifierKeys.Shift)
            .ToList()
            .ForEach(_ =>
            {
                e.Handled = true;

                Ask(sender, e);
            });

    /// <summary>點一個快速提問</summary>
    private void Pick(object sender, RoutedEventArgs e)
    {
        // 這一顆的字就在它裡面那一塊文字上
        Draft.Text = ((TextBlock)((Button)sender).Content).Text;

        Ask(sender, e);
    }

    /// <summary>問一句：把問的寫進去，助理想一下再把回答寫進去</summary>
    private async void Ask(object sender, RoutedEventArgs e)
    {
        // 還沒打字或正在查的時候不再問一次
        var asked = new[] { draft.Trim() }.Where(one => one.Length > 0).Where(_ => !thinking).ToList();

        foreach (var ask in asked)
        {
            // 問的那一句先寫進去，輸入框清空
            var said = AssistLogic.Say(ask, where, cite);

            store.SaveMessage(said[0].Who, said[0].Text, said[0].Cite);

            draft = string.Empty;
            thinking = true;
            Draft.Text = string.Empty;

            Show();

            // 查手冊與工單要一點時間
            await Task.Delay(Thinking);

            store.SaveMessage(said[1].Who, said[1].Text, said[1].Cite);

            thinking = false;

            Show();

            Stream.ScrollToEnd();
        }
    }

    /// <summary>把這一頁重畫一次</summary>
    private void Show() =>
        DataContext = AssistLogic.Assist(
            store.QueryAssists(),
            store.QueryShells().Single(one => one.tool_no == Assistant),
            icons,
            where,
            draft,
            thinking);

    #region 對照

    /// <summary>AI 助理是哪一支工具</summary>
    private const string Assistant = ShellLogic.Assistant;

    /// <summary>查手冊與工單花多久（毫秒）</summary>
    private const int Thinking = 900;

    #endregion
}
