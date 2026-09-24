using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HelixStudio.Logic;
using HelixStudio.Store;

namespace HelixStudio.Views;

/// <summary>工具資訊這一頁</summary>
public partial class DetailPanel : UserControl
{
    private readonly ShellStore store = new();

    private readonly Dictionary<string, List<string>> icons;

    private readonly Dictionary<string, CatalogLogic.RunItem> busy = [];

    private string key = string.Empty;

    private string tab = DetailLogic.Tabs[0].Key;

    private bool asking;

    private string typed = string.Empty;

    private Button? confirm;

    /// <summary>有人要回工具目錄</summary>
    public event EventHandler? Backed;

    /// <summary>有人要開起某一支工具</summary>
    public event EventHandler<string>? Launched;

    /// <summary>有人要打開 AI 助理</summary>
    public event EventHandler? Asked;

    /// <summary>備好這一頁</summary>
    public DetailPanel()
    {
        InitializeComponent();

        icons = store.QueryIcons();
    }

    /// <summary>打開一支工具的資訊：從總覽那一頁開始看</summary>
    /// <param name="toolNo">這一支工具的代碼</param>
    public void Open(string toolNo)
    {
        key = toolNo;
        tab = DetailLogic.Tabs[0].Key;
        asking = false;
        typed = string.Empty;

        Show();
    }

    /// <summary>回工具目錄</summary>
    private void Back(object sender, RoutedEventArgs e) =>
        Backed?.Invoke(this, EventArgs.Empty);

    /// <summary>換一個分頁</summary>
    private void PickTab(object sender, SelectionChangedEventArgs e) =>
        ((ListBox)sender).SelectedItems
            .OfType<DetailLogic.TabItem>()
            .Select(one => one.Key)
            .Where(one => one != tab)
            .ToList()
            .ForEach(one =>
            {
                tab = one;
                Show();
            });

    /// <summary>按主要按鈕：安裝、更新、開啟或送出授權申請</summary>
    private void Primary(object sender, RoutedEventArgs e)
    {
        var view = (DetailLogic.DetailView)DataContext;

        // 沒有使用資格的，只是把申請送出去並說一聲
        view.Says.ForEach(one => Note(one, "warn", []));

        // 已經裝好的，直接開起來
        view.Opens.ForEach(one => Launched?.Invoke(this, one));

        // 要安裝或更新的：需要核可的非必裝工具改成送申請，其餘跑完把結果寫回去
        view.Runs.Where(one => one.Needs).ToList()
            .ForEach(one => Note($"已把「{one.Name}」的安裝申請送給主管核可", "warn", []));
        view.Runs.Where(one => !one.Needs).ToList().ForEach(Run);
    }

    /// <summary>按次要按鈕：移除、先問一次再移除，或回工具目錄</summary>
    private void Secondary(object sender, RoutedEventArgs e)
    {
        var view = (DetailLogic.DetailView)DataContext;

        // 不是必裝的，直接移除
        view.Removes.ForEach(Run);

        // 必裝的，先問一次
        view.Confirms.ForEach(_ =>
        {
            asking = true;
            typed = string.Empty;

            Show();
        });

        // 還沒裝的，這顆按鈕是回工具目錄
        view.Backs.ForEach(_ => Backed?.Invoke(this, EventArgs.Empty));
    }

    /// <summary>確認框一出現就把那顆移除鈕留著，打字時才找得到它</summary>
    private void HoldConfirm(object sender, RoutedEventArgs e) =>
        confirm = (Button)sender;

    /// <summary>在確認框裡打字：打對名稱才按得下移除</summary>
    private void TypeName(object sender, TextChangedEventArgs e)
    {
        typed = ((TextBox)sender).Text;

        confirm!.IsEnabled = typed == ((DetailLogic.ConfirmView)((FrameworkElement)sender).DataContext).Name;
    }

    /// <summary>確認框一出現就把游標放進輸入框</summary>
    private void TakeFocus(object sender, RoutedEventArgs e) =>
        ((TextBox)sender).Focus();

    /// <summary>按 Esc：先收掉正在問的那一次確認</summary>
    private void Escape(object sender, KeyEventArgs e) =>
        new[] { e.Key }
            .Where(one => one == Key.Escape)
            .SelectMany(_ => ((DetailLogic.DetailView)DataContext).Asking)
            .Take(1)
            .ToList()
            .ForEach(_ =>
            {
                asking = false;
                e.Handled = true;

                Show();
            });

    /// <summary>保留這支工具</summary>
    private void KeepTool(object sender, RoutedEventArgs e)
    {
        asking = false;

        Show();
    }

    /// <summary>名稱打對了，移除這支必裝的工具</summary>
    private void RemoveTool(object sender, RoutedEventArgs e)
    {
        var confirm = (DetailLogic.ConfirmView)((Button)sender).DataContext;

        asking = false;

        Run(confirm.Run);
    }

    /// <summary>問 AI 助理</summary>
    private void AskAi(object sender, RoutedEventArgs e)
    {
        Asked?.Invoke(this, EventArgs.Empty);

        Note("已開啟 AI 助理，作業畫面留在原處", "info", []);
    }

    /// <summary>回報給維護團隊</summary>
    private void Report(object sender, RoutedEventArgs e) =>
        Note(((DetailLogic.DetailView)DataContext).Guides.Select(one => one.ReportText).First(), "info", []);

    /// <summary>請維護團隊補上說明</summary>
    private void ReportBlank(object sender, RoutedEventArgs e) =>
        Note(((DetailLogic.DetailView)DataContext).Blanks.Select(one => one.ReportText).First(), "info", []);

    /// <summary>打開現場 SOP</summary>
    private void OpenSop(object sender, RoutedEventArgs e) =>
        Note(((DetailLogic.DetailView)DataContext).Guides.Select(one => one.SopText).First(), "info", []);

    /// <summary>跑一件要花時間的事：進度推到滿，把結果寫回資料庫，再重讀一次</summary>
    /// <param name="run">要跑的那一件事</param>
    private async void Run(CatalogLogic.RunItem run)
    {
        System.IO.File.AppendAllText("C:/Users/user/AppData/Local/Temp/helix-run.log",
            $"[Detail.Run] {DateTime.Now:HH:mm:ss.fff} {run.Action} {run.Key} <<< "
            + Environment.StackTrace.Replace(Environment.NewLine, " | ") + Environment.NewLine);

        // 這一頁進到忙碌的樣子：按鈕換字、按不下去、進度條出現
        busy[run.Key] = run;

        Show();

        // 推進度只動這一件事身上的那個數字，整頁不重算也不重畫
        foreach (var pct in Steps)
        {
            run.Pct = pct;

            await Task.Delay(Tick);
        }

        busy.Remove(run.Key);
        store.SaveTool(run.Key, run.State, run.Version);

        Show();

        // 側欄與狀態列跟著這一次的結果換
        ((MainWindow)Window.GetWindow(this)!).Reread();
        Note($"「{run.Name}」{CatalogLogic.DoneText[run.Action]}", "info",
            new[] { run }.Where(one => CatalogLogic.Undoable.Contains(one.Action)).ToList());
    }

    /// <summary>跳出一則通知</summary>
    /// <param name="text">要說的話</param>
    /// <param name="kind">哪一種：info 或 warn</param>
    /// <param name="undos">按了復原要還原成什麼樣；不能復原就是零筆</param>
    private void Note(string text, string kind, IReadOnlyList<CatalogLogic.RunItem> undos) =>
        ((MainWindow)Window.GetWindow(this)!).Note(text, kind, undos);

    /// <summary>資料在別處被改過，這一頁重讀一次</summary>
    public void Refresh() =>
        new[] { key }.Where(one => one.Length > 0).ToList().ForEach(_ => Show());

    /// <summary>把這一頁重畫一次</summary>
    private void Show()
    {
        // 這一支工具那一列，還有用這台裝置的人是誰
        var rows = store.QueryShells();
        var person = rows.Where(one => one.type == "person").ToList();

        DataContext = DetailLogic.Detail(
            store.QueryShell(key),
            icons,
            person.SelectMany(one => JsonSerializer.Deserialize<List<string>>($"{one.roles}") ?? [])
                .Select(one => one.Replace("一般員工", string.Empty))
                .ToList(),
            person.Select(one => $"{one.dept}").DefaultIfEmpty(string.Empty).First(),
            tab,
            busy,
            asking);
    }

    #region 對照

    /// <summary>進度依序走過的這幾格</summary>
    private static readonly int[] Steps = [8, 24, 46, 68, 86, 100];

    /// <summary>每一格停多久（毫秒）</summary>
    private const int Tick = 260;

    #endregion
}
