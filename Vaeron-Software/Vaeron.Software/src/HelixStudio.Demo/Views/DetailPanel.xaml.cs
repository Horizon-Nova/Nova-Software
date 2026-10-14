using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HelixStudio.Logic;
using HelixStudio.Store;

namespace HelixStudio.Views;

/// <summary>工具目錄右邊那一欄：選中那一支工具的資訊、開啟（Demo 版的工具跟安裝檔一起來，不在這裡移除）</summary>
public partial class DetailPanel : UserControl
{
    private readonly ShellStore store = new();

    private readonly Dictionary<string, List<string>> icons;

    private string key = string.Empty;

    /// <summary>看的是第幾個分頁：0 總覽、1 操作說明、2 更新日誌</summary>
    private int selected;

    /// <summary>放大看的那一張截圖；沒有放大就是空字串</summary>
    private string zoom = string.Empty;

    /// <summary>有人要開起某一支工具</summary>
    public event EventHandler<string>? Launched;

    /// <summary>備好這一欄</summary>
    public DetailPanel()
    {
        InitializeComponent();

        icons = store.QueryIconList();
    }

    /// <summary>看一支工具的資訊；同一支就只重讀</summary>
    /// <param name="toolNo">這一支工具的代碼</param>
    public void Open(string toolNo)
    {
        // 換了一支就從頂端看起
        var other = toolNo != key;

        key = toolNo;

        Show();

        if (other) InfoScroll.ScrollToTop();
    }

    /// <summary>換一個分頁</summary>
    private void PickTab(object sender, SelectionChangedEventArgs e)
    {
        var picked = ((ListBox)sender).SelectedIndex;

        if (picked < 0 || picked == selected) return;

        selected = picked;

        Show();
        InfoScroll.ScrollToTop();
    }

    /// <summary>點一張截圖：蓋住整個視窗放大看</summary>
    private void ZoomIn(object sender, MouseButtonEventArgs e)
    {
        var window = Window.GetWindow(this)!;

        var offset = InfoScroll.VerticalOffset;

        // 重畫這一欄時操作說明停在原來看的位置
        zoom = $"{((FrameworkElement)sender).Tag}";
        Show();
        InfoScroll.UpdateLayout();
        InfoScroll.ScrollToVerticalOffset(offset);

        // 這一次點擊放開之後才蓋上去，不然放開的那一下就把它收掉
        ZoomLayer.PlacementTarget = (UIElement)window.Content;
        // 浮出來的視窗高不能超過螢幕的四分之三，圖卡照這個上限縮
        ZoomBack.MaxWidth = ((FrameworkElement)window.Content).ActualWidth - 64;
        ZoomBack.MaxHeight = Math.Min(((FrameworkElement)window.Content).ActualHeight - 64, SystemParameters.PrimaryScreenHeight * 0.75 - 32);
        Dispatcher.BeginInvoke(() => ZoomLayer.IsOpen = true, System.Windows.Threading.DispatcherPriority.Input);
        e.Handled = true;
    }

    /// <summary>收起放大的截圖</summary>
    private void ZoomOut(object sender, EventArgs e)
    {
        if (zoom.Length == 0) return;

        var offset = InfoScroll.VerticalOffset;

        zoom = string.Empty;
        ZoomLayer.IsOpen = false;
        Show();
        InfoScroll.UpdateLayout();
        InfoScroll.ScrollToVerticalOffset(offset);
    }

    /// <summary>開啟工具</summary>
    private void Primary(object sender, RoutedEventArgs e) =>
        Launched?.Invoke(this, key);

    /// <summary>按 Esc：收起放大的截圖</summary>
    private void Escape(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || zoom.Length == 0) return;

        e.Handled = true;
        ZoomOut(sender, e);
    }

    /// <summary>把這一欄重畫一次；這一支已經不在就什麼都不畫</summary>
    private void Show()
    {
        var rows = store.QueryCatalogItem(key);

        DataContext = rows.Any(row => row.type == "tool") ? DetailLogic.Detail(rows, icons, false, selected, zoom) : null;
    }
}
