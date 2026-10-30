using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using InSpecTool.Logic;
using InSpecTool.Service;
using Microsoft.Win32;

namespace InSpecTool;

/// <summary>InSpec 自己寫入了一筆結果：操作員沒透過 InSpecTool 跑時，在這裡確認路徑與料號再歸檔</summary>
public partial class Manual : Window
{
    /// <summary>量測頁的串接</summary>
    private readonly InspecService service;

    /// <summary>量測頁打開時讀的設定：系統紀錄資料夾與備份輸出路徑</summary>
    private readonly InspecLogic.MeasureView setup;

    private readonly List<InspecLogic.PendingItem> queue = [];

    private bool auto = true;

    private bool filling;

    /// <summary>備好這個視窗</summary>
    /// <param name="service">量測頁的串接</param>
    /// <param name="setup">量測頁打開時讀的設定</param>
    public Manual(InspecService service, InspecLogic.MeasureView setup)
    {
        InitializeComponent();

        this.service = service;
        this.setup = setup;
    }

    /// <summary>又有幾筆 InSpec 寫入的結果要歸檔：接在後面；沒有正在處理的就從第一筆開始</summary>
    /// <param name="items">寫入的當下已經記成 inspec 的那幾列，一次一筆</param>
    public void Add(List<InspecLogic.PendingItem> items)
    {
        var idle = queue.Count == 0;

        queue.AddRange(items);

        if (idle) Next();
        else Origin.Text = Header();
    }

    /// <summary>放上排在最前面的那一筆：內容與照規則算好的路徑</summary>
    private void Next()
    {
        var item = queue[0];

        Origin.Text = Header();
        Body.Text = item.Content;
        Fault.Visibility = Visibility.Collapsed;
        ExportButton.IsEnabled = true;

        // 料號一改，路徑跟著換；操作員自己改過路徑就不再跟
        filling = true;
        Part.Text = item.Part;
        Where.Text = service.LoadTarget(item, item.Part, setup.LogFolder);
        filling = false;
        auto = true;

        Part.Focus();
        Part.SelectAll();
    }

    /// <summary>標題下那一行：這一筆什麼時候寫入、哪一支程式、還有幾筆</summary>
    private string Header() =>
        $"{queue[0].Time:HH:mm:ss} · {(queue[0].Program.Length > 0 ? queue[0].Program : "InSpec 沒有開著量測程式")} · 待歸檔 {queue.Count} 筆";

    /// <summary>改了料號：路徑還是自動帶的就跟著換</summary>
    /// <param name="sender">料號框</param>
    /// <param name="e">這一次改字</param>
    private void PartTyped(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (filling || !auto) return;

        filling = true;
        Where.Text = service.LoadTarget(queue[0], Part.Text, setup.LogFolder);
        filling = false;
    }

    /// <summary>操作員自己改了路徑：之後改料號不再動它</summary>
    /// <param name="sender">路徑框</param>
    /// <param name="e">這一次改字</param>
    private void WhereTyped(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (filling) return;

        auto = false;
    }

    /// <summary>選擇路徑：Windows 的另存新檔，從目前路徑的資料夾開始</summary>
    /// <param name="sender">資料夾圖示</param>
    /// <param name="e">這一次點擊</param>
    private void Browse(object sender, RoutedEventArgs e)
    {
        var now = Where.Text.Trim();
        var pick = new SaveFileDialog
        {
            Title = "選擇歸檔路徑",
            Filter = "文字檔|*.txt|所有檔案|*.*",
            OverwritePrompt = false,
            FileName = Path.GetFileName(now),
            InitialDirectory = Directory.Exists(Path.GetDirectoryName(now)) ? Path.GetDirectoryName(now) : string.Empty,
        };

        if (pick.ShowDialog(this) != true) return;

        // 指定了某個檔：路徑就是它；料號從檔名 {料號}_{批號}.txt 帶出來
        var picked = Path.GetFileNameWithoutExtension(pick.FileName);

        filling = true;
        Where.Text = pick.FileName;
        Part.Text = picked.Contains('_') ? picked[..picked.LastIndexOf('_')] : picked;
        filling = false;
        auto = false;
    }

    /// <summary>歸檔到路徑：附加到歸檔檔、補上那一列；寫不成就把原因寫在下面</summary>
    /// <param name="sender">匯出圖示</param>
    /// <param name="e">這一次點擊</param>
    private void Export(object sender, RoutedEventArgs e)
    {
        ExportButton.IsEnabled = false;

        var fault = service.LoadExported(setup, queue[0], Where.Text.Trim(), Part.Text.Trim(), Body.Text);

        if (fault.Length > 0)
        {
            Fault.Text = fault;
            Fault.Visibility = Visibility.Visible;
            ExportButton.IsEnabled = true;
            return;
        }

        // 這一筆歸檔了；還有就換下一筆，沒有就關
        queue.RemoveAt(0);

        if (queue.Count > 0) Next();
        else Close();
    }

    /// <summary>複製內容：圖示換成勾，約 2 秒後換回來</summary>
    /// <param name="sender">複製圖示</param>
    /// <param name="e">這一次點擊</param>
    private void Copy(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(Body.Text);

        CopyMark.Data = Geometry.Parse("M20 6 9 17l-5-5");
        var back = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        back.Tick += (_, _) =>
        {
            back.Stop();
            CopyMark.Data = Geometry.Parse("M8 8h13v13H8z M4 16V4a1 1 0 0 1 1-1h11");
        };
        back.Start();
    }

    /// <summary>關閉：不歸檔；寫入的當下已經記成 inspec 的列，留在最近執行裡，沒有歸檔</summary>
    /// <param name="sender">關閉</param>
    /// <param name="e">這一次點擊</param>
    private void Shut(object sender, RoutedEventArgs e) =>
        Close();

    /// <summary>拖著標題列移動視窗</summary>
    /// <param name="sender">外框</param>
    /// <param name="e">按下滑鼠</param>
    private void Drag(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is System.Windows.Controls.TextBox or System.Windows.Controls.Button) return;

        DragMove();
    }

    /// <summary>Esc 關閉、Enter 歸檔</summary>
    /// <param name="sender">這個視窗</param>
    /// <param name="e">按下的鍵</param>
    private void Keys(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close();
        if (e.Key == Key.Enter && ExportButton.IsEnabled) Export(sender, e);
    }
}
