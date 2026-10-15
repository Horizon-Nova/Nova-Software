using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HelixStudio.Logic;
using HelixStudio.Store;

namespace HelixStudio.Views;

/// <summary>工具目錄右邊那一欄：選中那一支工具的資訊、開啟、移除</summary>
public partial class DetailPanel : UserControl
{
    private readonly ShellStore store = new();

    private readonly Dictionary<string, List<string>> icons;

    private string key = string.Empty;

    /// <summary>看的是第幾個分頁：0 總覽、1 操作說明、2 更新日誌</summary>
    private int selected;

    /// <summary>放大看的那一張截圖；沒有放大就是空字串</summary>
    private string zoom = string.Empty;

    /// <summary>這一支工具移除了</summary>
    public event EventHandler? Removed;

    /// <summary>有人按了移除工具：主視窗蓋上遮罩問一次，帶的是工具名稱</summary>
    public event EventHandler<string>? Asking;

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

    /// <summary>移除工具：先問一次，打對名稱才移；問的那一層蓋住整個視窗，所以交給主視窗</summary>
    private void Secondary(object sender, RoutedEventArgs e) =>
        Asking?.Invoke(this, $"{((DetailLogic.DetailView)DataContext).Tool.name}");

    /// <summary>按 Esc：收掉放大的截圖</summary>
    private void Escape(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || zoom.Length == 0) return;

        e.Handled = true;
        ZoomOut(sender, e);
    }

    /// <summary>名稱打對了：這支工具的資料列拿掉，檔案從這台電腦刪掉</summary>
    public void Remove()
    {
        var tool = ((DetailLogic.DetailView)DataContext).Tool;
        var window = (MainWindow)Window.GetWindow(this)!;

        // 資料列：拿掉之後工具目錄、側欄、狀態列都不再有它
        store.DeleteTool(key);

        // 檔案：這一次開過的工具組件還映射在平台這個行程裡，Windows 鎖著它的檔，就先留著並說出在哪裡；沒映射就整個刪掉
        var home = Path.GetDirectoryName($"{tool.home}")!;
        var loaded = Process.GetCurrentProcess().Modules
            .OfType<ProcessModule>()
            .Any(one => one.FileName.StartsWith(home, StringComparison.OrdinalIgnoreCase));

        new[] { home }.Where(_ => !loaded).Where(Directory.Exists).ToList().ForEach(one => Directory.Delete(one, true));

        var left = loaded ? home : string.Empty;

        key = string.Empty;

        Removed?.Invoke(this, EventArgs.Empty);
        window.Reread();
        window.Note(left.Length == 0 ? $"已移除「{tool.name}」" : $"已移除「{tool.name}」；檔案還在使用中，關掉平台後可以手動刪掉 {left}", "info");
    }

    /// <summary>把這一欄重畫一次；這一支已經不在就什麼都不畫</summary>
    private void Show()
    {
        var rows = store.QueryCatalogItem(key);

        DataContext = rows.Any(row => row.type == "tool") ? DetailLogic.Detail(rows, icons, selected, zoom) : null;
    }
}
