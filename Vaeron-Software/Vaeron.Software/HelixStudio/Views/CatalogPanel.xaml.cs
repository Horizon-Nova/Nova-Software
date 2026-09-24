using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HelixStudio.Logic;
using HelixStudio.Store;
using Models.HelixStudio;

namespace HelixStudio.Views;

/// <summary>工具目錄這一頁</summary>
public partial class CatalogPanel : UserControl
{
    private readonly ShellStore store = new();

    private IReadOnlyList<vw_shell> rows = [];

    private readonly Dictionary<string, List<string>> icons;

    private string cat = CatalogLogic.All;

    private string query = string.Empty;

    private readonly Dictionary<string, CatalogLogic.RunItem> busy = [];

    /// <summary>畫面上現在排著的那幾張卡片；從頭到尾是同一份，只補上或換掉變動的那幾張</summary>
    private readonly ObservableCollection<CatalogLogic.CardItem> cards = [];

    private bool loaded;

    private bool slow;

    /// <summary>有人要打開某一支工具的資訊</summary>
    public event EventHandler<string>? Opened;

    /// <summary>有人要匯入工具套件</summary>
    public event EventHandler? Importing;

    /// <summary>卡片一列排幾張，由容器量到的寬度決定</summary>
    public static readonly DependencyProperty ColumnsProperty =
        DependencyProperty.Register(nameof(Columns), typeof(int), typeof(CatalogPanel), new PropertyMetadata(1));

    /// <summary>卡片一列排幾張</summary>
    public int Columns
    {
        get => (int)GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    /// <summary>打開工具目錄：讀出資料，算出這一頁要顯示的每一格</summary>
    public CatalogPanel()
    {
        InitializeComponent();

        icons = store.QueryIcons();

        CardGrid.ItemsSource = cards;

        // 容器有多寬就排幾張卡片：一張最少 296，張與張之間空 16
        SizeChanged += (_, _) => Columns = Math.Max(1, (int)((CardGrid.ActualWidth + Gap) / (CardWidth + Gap)));

        Read();
    }

    /// <summary>讀出資料：讀得快就直接出現，讀太久才先擺骨架</summary>
    private async void Read()
    {
        // 先把空的畫上去，這時什麼都還沒有
        Show();

        // 讀與等這兩件事誰先到；等到了資料還沒回來，才算讀太久
        var reading = Task.Run(store.QueryShells);

        await Task.WhenAny(reading, Task.Delay(Patience));

        slow = !reading.IsCompleted;

        Show();

        rows = await reading;
        loaded = true;
        slow = false;

        Show();
    }

    /// <summary>開起來先看哪一個畫面：設定寫哪一個分類就篩到哪一個</summary>
    /// <param name="start">設定裡「開啟後顯示」的值</param>
    public void Begin(string start)
    {
        // 那個值本身就是分類名稱時就篩它，其餘維持看全部
        cat = new[] { start }
            .Where(one => rows.Any(row => $"{row.cat}" == one))
            .DefaultIfEmpty(CatalogLogic.All)
            .First();

        Show();
    }

    /// <summary>打字搜尋</summary>
    private void Search(object sender, TextChangedEventArgs e)
    {
        query = SearchText.Text;
        RecentPanel.IsOpen = false;

        Show();
    }

    /// <summary>點進搜尋框：還沒打字就把最近搜尋攤出來</summary>
    private void OpenRecent(object sender, KeyboardFocusChangedEventArgs e) =>
        RecentPanel.IsOpen = query.Trim().Length == 0 && ((CatalogLogic.CatalogView)DataContext).Recents.Count > 0;

    /// <summary>離開搜尋框：等一下下再收起來，讓點得到最近搜尋那幾列</summary>
    private async void CloseRecent(object sender, KeyboardFocusChangedEventArgs e)
    {
        await Task.Delay(Linger);

        RecentPanel.IsOpen = SearchText.IsKeyboardFocusWithin;
    }

    /// <summary>按 Esc：先收掉攤開的最近搜尋</summary>
    private void Escape(object sender, KeyEventArgs e) =>
        new[] { e.Key }
            .Where(one => one == Key.Escape)
            .Where(_ => RecentPanel.IsOpen)
            .ToList()
            .ForEach(_ =>
            {
                RecentPanel.IsOpen = false;
                e.Handled = true;
            });

    /// <summary>點最近搜尋的一項</summary>
    private void PickRecent(object sender, RoutedEventArgs e)
    {
        // 這個字填回搜尋框，分類回到全部；TextChanged 會接著重算這一頁
        var picked = $"{((Button)sender).Content}";

        store.SaveSearch(picked);
        rows = store.QueryShells();

        cat = CatalogLogic.All;
        SearchText.Text = picked;
        SearchText.CaretIndex = picked.Length;
    }

    /// <summary>點一個分類</summary>
    private void PickCat(object sender, SelectionChangedEventArgs e) =>
        ((ListBox)sender).SelectedItems
            .OfType<CatalogLogic.ChipItem>()
            .Select(one => one.Text)
            .Where(one => one != cat)
            .ToList()
            .ForEach(one =>
            {
                cat = one;
                Show();
            });

    /// <summary>清除篩選</summary>
    private void Clear(object sender, RoutedEventArgs e)
    {
        cat = CatalogLogic.All;
        SearchText.Text = string.Empty;

        Show();
    }

    /// <summary>找不到工具時按「看全部」</summary>
    private void ShowAll(object sender, RoutedEventArgs e) =>
        Clear(sender, e);

    /// <summary>找不到工具時按建議的分類</summary>
    private void PickSuggest(object sender, RoutedEventArgs e)
    {
        cat = ((CatalogLogic.SuggestItem)((Button)sender).DataContext).Cat;
        SearchText.Text = string.Empty;

        Show();
    }

    /// <summary>按匯入工具套件</summary>
    /// <param name="sender">按下去的那一顆</param>
    /// <param name="e">這一次點擊</param>
    private void OpenImport(object sender, RoutedEventArgs e) =>
        Importing?.Invoke(this, EventArgs.Empty);

    /// <summary>按卡片上那顆動作按鈕</summary>
    private void Act(object sender, RoutedEventArgs e)
    {
        // 點在按鈕上就不算點整張卡片
        e.Handled = true;

        var card = (CatalogLogic.CardItem)((Button)sender).DataContext;

        // 沒有使用資格的，只是把申請送出去並說一聲
        card.Says.ForEach(one => Note(one, "warn", []));

        // 已經裝好的，直接開起來
        card.Opens.ForEach(one => Opened?.Invoke(this, one));

        // 要安裝或更新的：需要核可的非必裝工具改成送申請，其餘跑完把結果寫回去
        card.Runs.Where(one => one.Needs).ToList()
            .ForEach(one => Note($"已把「{one.Name}」的安裝申請送給主管核可", "warn", []));
        card.Runs.Where(one => !one.Needs).ToList()
            .ForEach(one => Run(one, card.Name));
    }

    /// <summary>把整張卡片點下去，看這支工具的資訊</summary>
    private void Open(object sender, RoutedEventArgs e) =>
        Opened?.Invoke(this, ((CatalogLogic.CardItem)((Button)sender).DataContext).Key);

    /// <summary>把可以更新的工具一次更新完</summary>
    public void UpdateAll(object sender, RoutedEventArgs e)
    {
        var updates = ((CatalogLogic.CatalogView)DataContext).Updatables;

        // 先說這一次要動幾支；一支都沒有就說都是最新版
        Enumerable.Repeat("已安裝的工具都是最新版", 1)
            .Where(_ => updates.Count == 0)
            .Concat(Enumerable.Repeat($"已開始更新 {updates.Count} 支工具", 1).Where(_ => updates.Count > 0))
            .ToList()
            .ForEach(one => Note(one, "info", []));

        updates.ForEach(one => Run(one, one.Name));
    }

    /// <summary>把指定的那幾支工具裝起來</summary>
    /// <param name="runs">要跑的每一件事</param>
    public void Install(IReadOnlyList<CatalogLogic.RunItem> runs) =>
        runs.ToList().ForEach(one => Run(one, one.Name));

    /// <summary>跑一件要花時間的事：進度推到滿，把結果寫回資料庫，再重讀一次</summary>
    /// <param name="run">要跑的那一件事</param>
    /// <param name="name">那一支工具叫什麼</param>
    private async void Run(CatalogLogic.RunItem run, string name)
    {
        // 這張卡片進到忙碌的樣子：按鈕換字、按不下去、進度條出現
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

        // 側欄、狀態列與這一頁都跟著這一次的結果換；重讀會連這一頁一起重畫，這裡不必再畫一次
        ((MainWindow)Window.GetWindow(this)!).Reread();
        Note($"「{name}」{CatalogLogic.DoneText[run.Action]}", "info",
            new[] { run }.Where(one => CatalogLogic.Undoable.Contains(one.Action)).ToList());
    }

    /// <summary>跳出一則通知</summary>
    /// <param name="text">要說的話</param>
    /// <param name="kind">哪一種：info 或 warn</param>
    /// <param name="undos">按了復原要還原成什麼樣；不能復原就是零筆</param>
    private void Note(string text, string kind, IReadOnlyList<CatalogLogic.RunItem> undos) =>
        ((MainWindow)Window.GetWindow(this)!).Note(text, kind, undos);

    /// <summary>資料在別處被改過，這一頁重讀一次</summary>
    public void Refresh()
    {
        rows = store.QueryShells();

        Show();
    }

    /// <summary>把這一頁重畫一次</summary>
    private void Show()
    {
        var view = CatalogLogic.Catalog(rows, icons, cat, query, busy, loaded, slow);

        DataContext = view;

        // 卡片照這一次的結果補差額，沒變的那幾張原封不動；整批重建會讓每一張重新淡入，看起來就是整頁閃一下
        cards.Where(one => view.Cards.All(two => two.Key != one.Key)).ToList()
            .ForEach(one => cards.Remove(one));

        view.Cards.Where(one => cards.All(two => two.Key != one.Key)).ToList()
            .ForEach(one => cards.Insert(view.Cards.IndexOf(one), one));

        view.Cards.Select((one, index) => new { one, index })
            .Where(one => cards[one.index].Mark != one.one.Mark)
            .ToList()
            .ForEach(one => cards[one.index] = one.one);
    }

    #region 對照

    /// <summary>一張卡片最少多寬：設計檔 minmax(296px,1fr)</summary>
    private const double CardWidth = 296;

    /// <summary>卡片之間空多少</summary>
    private const double Gap = 16;

    /// <summary>離開搜尋框之後最近搜尋還留多久（毫秒）</summary>
    private const int Linger = 120;

    /// <summary>進度依序走過的這幾格</summary>
    private static readonly int[] Steps = [8, 24, 46, 68, 86, 100];

    /// <summary>每一格停多久（毫秒）</summary>
    private const int Tick = 260;

    /// <summary>讀多久以內不顯示載入狀態（毫秒）</summary>
    private const int Patience = 300;

    #endregion
}
