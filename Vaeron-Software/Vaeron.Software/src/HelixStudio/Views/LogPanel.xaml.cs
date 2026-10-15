using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using HelixStudio.Logic;
using HelixStudio.Store;
using Models.HelixStudio;

namespace HelixStudio.Views;

/// <summary>日誌這一頁：左邊一件一列，右邊選中那一件的明細</summary>
public partial class LogPanel : UserControl
{
    private readonly ShellStore store = new();

    private readonly Dictionary<string, List<string>> icons;

    private string query = string.Empty;

    /// <summary>照哪一欄排：at、type、tool、who</summary>
    private string sort = "at";

    private bool descending = true;

    /// <summary>選中那一件的流水號</summary>
    private int picked;

    /// <summary>選中那一件剛被複製</summary>
    private bool copied;

    /// <summary>vw_event_log 這一次讀到的每一列</summary>
    private IReadOnlyList<vw_event_log> rows = [];

    /// <summary>「幾分前」跟著時間走</summary>
    private readonly DispatcherTimer clock = new() { Interval = TimeSpan.FromSeconds(Tick) };

    /// <summary>打開日誌</summary>
    public LogPanel()
    {
        InitializeComponent();

        icons = store.QueryIconList();

        // 看得到這一頁時右邊的「幾分前」每 30 秒重算一次，看不到就停
        clock.Tick += (_, _) => Show();
        IsVisibleChanged += (_, _) => clock.IsEnabled = IsVisible;
    }

    /// <summary>照現在的搜尋字與排序重讀一次，整頁重畫</summary>
    public void Refresh()
    {
        rows = store.QueryEventLogList(query, sort, descending);

        var view = LogLogic.Log(rows, icons, query, sort, descending, picked, copied, DateTime.UtcNow);

        picked = view.Picked;
        DataContext = view;
        Detail.ItemsSource = view.Picks;
    }

    /// <summary>打字搜尋：清單從頭擺</summary>
    private void Search(object sender, TextChangedEventArgs e)
    {
        query = SearchText.Text.Trim();

        Refresh();
    }

    /// <summary>在搜尋框按 Esc：清掉搜尋字</summary>
    private void SearchKey(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || SearchText.Text.Length == 0) return;

        e.Handled = true;
        SearchText.Text = string.Empty;
    }

    /// <summary>清除搜尋字</summary>
    private void Clear(object sender, RoutedEventArgs e) =>
        SearchText.Text = string.Empty;

    /// <summary>點欄名：照那一欄排，再點一次反過來；時間先新到舊，其餘先小到大</summary>
    private void Sort(object sender, RoutedEventArgs e)
    {
        var key = $"{((Button)sender).Tag}";

        descending = key == sort ? !descending : key == "at";
        sort = key;

        Refresh();
    }

    /// <summary>點清單的一列：右邊換成那一件</summary>
    private void PickRow(object sender, SelectionChangedEventArgs e)
    {
        var row = EventList.SelectedItem as LogLogic.RowItem;

        if (row is null || row.Id == picked) return;

        picked = row.Id;
        copied = false;

        Show();
    }

    /// <summary>複製選中那一件：按鈕換成勾號停 2 秒</summary>
    private async void Copy(object sender, RoutedEventArgs e)
    {
        var text = ((LogLogic.PickView)((Button)sender).DataContext).CopyText;

        // 剪貼簿被別的程式佔著時放不進去，按鈕就不換
        try
        {
            Clipboard.SetText(text);

            copied = true;
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            copied = false;
        }

        Show();

        await Task.Delay(Linger);

        copied = false;

        Show();
    }

    /// <summary>只重畫右邊選中那一件；清單不動，捲到哪就停在哪</summary>
    private void Show() =>
        Detail.ItemsSource = LogLogic.Log(rows, icons, query, sort, descending, picked, copied, DateTime.UtcNow).Picks;

    #region 對照

    /// <summary>「幾分前」幾秒重算一次</summary>
    private const int Tick = 30;

    /// <summary>複製後勾號停多久（毫秒）</summary>
    private const int Linger = 2000;

    #endregion
}
