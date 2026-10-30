using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HelixStudio.Logic;
using HelixStudio.Service;

namespace HelixStudio.Views;

/// <summary>工具目錄這一頁：左邊清單，右邊選中那一支的資訊</summary>
public partial class CatalogPanel : UserControl
{
    private readonly CatalogService service = new();

    private string query = string.Empty;

    /// <summary>這一次攤出來的這一頁</summary>
    private CatalogLogic.CatalogView view = new();

    /// <summary>選中的工具代碼</summary>
    private string picked = string.Empty;

    private bool loaded;

    private bool slow;

    /// <summary>有人要開起某一支工具</summary>
    public event EventHandler<string>? Launched;

    /// <summary>有人要匯入工具套件</summary>
    public event EventHandler? Importing;

    /// <summary>打開工具目錄：讀出資料，擺到畫面上</summary>
    public CatalogPanel()
    {
        InitializeComponent();

        // 右邊那一欄按開啟就開這支；移除了就讓清單重選
        Info.Launched += (_, toolNo) => Launched?.Invoke(this, toolNo);
        Info.Removed += (_, _) => picked = string.Empty;

        Read();
    }

    /// <summary>讀出資料：讀得快就直接出現，讀太久才先擺骨架</summary>
    private async void Read()
    {
        // 先把空的畫上去，這時什麼都還沒有
        Show(service.LoadCatalog(string.Empty, false, false, picked));

        // 在背景另開一份連線讀；讀與等這兩件事誰先到，等到了資料還沒回來，才算讀太久
        var reading = Task.Run(() => new CatalogService().LoadCatalog(string.Empty, true, false, picked));

        await Task.WhenAny(reading, Task.Delay(Patience));

        slow = !reading.IsCompleted;
        new[] { slow }.Where(one => one).ToList().ForEach(_ => Show(service.LoadCatalog(string.Empty, false, true, picked)));

        var read = await reading;

        loaded = true;
        slow = false;
        Show(read);
    }

    /// <summary>選一支工具：清單落在它那一列，右邊換成它的資訊</summary>
    /// <param name="toolNo">工具代碼</param>
    public void Select(string toolNo)
    {
        picked = toolNo;

        Refresh();
    }

    /// <summary>資料在別處被改過，照現在的搜尋字重讀一次</summary>
    public void Refresh()
    {
        Show(service.LoadCatalog(query, loaded, slow, picked));
    }

    /// <summary>打字搜尋</summary>
    private void Search(object sender, TextChangedEventArgs e)
    {
        query = SearchText.Text.Trim();
        RecentPanel.IsOpen = false;

        Refresh();
    }

    /// <summary>點進搜尋框：還沒打字就把最近搜尋攤出來</summary>
    private void OpenRecent(object sender, KeyboardFocusChangedEventArgs e) =>
        RecentPanel.IsOpen = query.Length == 0 && view.Recents.Count > 0;

    /// <summary>離開搜尋框：等一下下再收起來，讓點得到最近搜尋那幾列</summary>
    private async void CloseRecent(object sender, KeyboardFocusChangedEventArgs e)
    {
        await Task.Delay(Linger);

        RecentPanel.IsOpen = SearchText.IsKeyboardFocusWithin;
    }

    /// <summary>按 Esc：先收掉攤開的最近搜尋</summary>
    private void Escape(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || !RecentPanel.IsOpen) return;

        RecentPanel.IsOpen = false;
        e.Handled = true;
    }

    /// <summary>點最近搜尋的一項：這個字記一次，填回搜尋框</summary>
    private void PickRecent(object sender, RoutedEventArgs e)
    {
        var word = $"{((Button)sender).Content}";

        service.LoadSearched([word]);

        SearchText.Text = word;
        SearchText.CaretIndex = word.Length;
    }

    /// <summary>清除搜尋字，看全部</summary>
    private void Clear(object sender, RoutedEventArgs e)
    {
        SearchText.Text = string.Empty;

        Refresh();
    }

    /// <summary>按匯入工具套件</summary>
    /// <param name="sender">按下去的那一顆</param>
    /// <param name="e">這一次點擊</param>
    private void OpenImport(object sender, RoutedEventArgs e) =>
        Importing?.Invoke(this, EventArgs.Empty);

    /// <summary>點清單的一列：右邊換成那一支的資訊</summary>
    private void PickTool(object sender, SelectionChangedEventArgs e)
    {
        var row = ToolList.SelectedItem as CatalogLogic.RowItem;

        if (row is null || row.Key == picked) return;

        picked = row.Key;
        Info.Open(picked);
    }

    /// <summary>雙擊清單的一列：開起那一支</summary>
    private void LaunchTool(object sender, MouseButtonEventArgs e)
    {
        if (ToolList.SelectedItem is not CatalogLogic.RowItem row) return;

        Launched?.Invoke(this, row.Key);
    }

    /// <summary>在清單上按 Enter：開起選中的那一支</summary>
    private void ToolKey(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || ToolList.SelectedItem is not CatalogLogic.RowItem row) return;

        e.Handled = true;
        Launched?.Invoke(this, row.Key);
    }

    /// <summary>把這一頁擺上去：清單落在選中那一支，右邊跟著換；一支都沒有就收起右邊</summary>
    /// <param name="shown">攤好的這一頁</param>
    private void Show(CatalogLogic.CatalogView shown)
    {
        view = shown;
        picked = view.Picked;
        DataContext = view;
        Info.Visibility = picked.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        Info.Open(picked);
    }

    #region 對照

    /// <summary>離開搜尋框之後最近搜尋還留多久（毫秒）</summary>
    private const int Linger = 120;

    /// <summary>讀多久以內不顯示載入狀態（毫秒）</summary>
    private const int Patience = 300;

    #endregion
}
