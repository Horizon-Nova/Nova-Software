using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using Models.HelixStudio;

namespace InSpecTool;

/// <summary>InSpec 自己寫入了一筆結果：操作員沒透過 InSpecTool 跑時，在這裡確認路徑與料號再歸檔</summary>
public partial class Manual : Window
{
    private readonly string connection;

    private readonly vw_inspec_measure setup;

    private readonly string input;

    private readonly string program;

    private string folder = string.Empty;

    private readonly List<PendingItem> queue = [];


    private bool auto = true;

    private bool filling;

    /// <summary>備好這個視窗</summary>
    /// <param name="connection">平台資料庫的連線</param>
    /// <param name="setup">vw_inspec_measure 的 setting 那一列：InSpecTool 的設定</param>
    /// <param name="input">InSpec 寫入的那個檔</param>
    /// <param name="program">InSpec 開著的量測程式（找得到就是完整路徑，找不到就是檔名）</param>
    public Manual(string connection, vw_inspec_measure setup, string input, string program)
    {
        InitializeComponent();

        this.connection = connection;
        this.setup = setup;
        this.input = input;
        this.program = program;
    }

    /// <summary>又有幾筆 InSpec 寫入的結果要歸檔：接在後面；沒有正在處理的就從第一筆開始</summary>
    /// <param name="items">寫入的當下已經記成 inspec 的那幾列，一次一筆</param>
    /// <param name="folder">系統紀錄資料夾</param>
    public void Add(List<PendingItem> items, string folder)
    {
        var idle = queue.Count == 0;

        this.folder = folder;
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
        Where.Text = Target(item.Part);
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
        Where.Text = Target(Part.Text);
        filling = false;
    }

    /// <summary>要存的那個檔：系統紀錄資料夾\資料夾\{料號}_{批號}.txt</summary>
    /// <remarks>資料夾照歸檔規則用料號與分析出來的壓合算；壓合分析不出來，用量測程式所在的資料夾對到系統紀錄資料夾。沒有料號就是空字串。</remarks>
    /// <param name="part">料號</param>
    private string Target(string part)
    {
        var item = queue[0];
        var safe = global::InSpecTool.Measure.Safe(part);
        var analyzed = global::InSpecTool.Measure.Folder(part, item.Press);
        var name = global::InSpecTool.Measure.Safe(item.Lot.Length > 0 ? $"{safe}_{item.Lot}.txt" : $"{safe}.txt");

        return safe.Length > 0 ? Path.Combine(folder, analyzed.Length > 0 ? analyzed : item.ProgramFolder, name) : string.Empty;
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

    /// <summary>歸檔到路徑：附加到歸檔檔、記一列，清空 InSpec 寫入的檔</summary>
    /// <param name="sender">匯出圖示</param>
    /// <param name="e">這一次點擊</param>
    private void Export(object sender, RoutedEventArgs e)
    {
        ExportButton.IsEnabled = false;

        // 寫進路徑；路徑所在的磁碟或網路資料夾找不到，就改寫到備份路徑（同樣的相對路徑）
        var target = Where.Text.Trim();
        var root = $"{setup.log_folder}".Trim();
        var backup = $"{setup.backup_folder}".Trim();
        var relative = target.Length > 0 && root.Length > 0 && Path.GetFullPath(target).StartsWith(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase)
            ? Path.GetRelativePath(root, target)
            : Path.GetFileName(target);
        var candidates = new[] { target, backup.Length > 0 && relative.Length > 0 ? Path.Combine(backup, relative) : string.Empty }
            .Where(one => one.Length > 0)
            .Select(Path.GetFullPath)
            .ToList();
        var overlong = candidates.Where(one => one.Split(Path.DirectorySeparatorChar).Any(part => part.Length > global::InSpecTool.Measure.NameLimit)).ToList();
        var archived = candidates.Except(overlong).FirstOrDefault(one => Directory.Exists(Path.GetPathRoot(one)));

        if (archived is null)
        {
            Fault.Text = candidates.Count == 0 ? "路徑是空的：填上料號，或在路徑直接打要存的檔。"
                : overlong.Count == candidates.Count ? $"路徑裡有一層超過 Windows 的 {(global::InSpecTool.Measure.NameLimit)} 字上限，沒有歸檔。"
                : $"找不到 {string.Join("、", candidates)} 所在的磁碟或網路資料夾，沒有歸檔。";
            Fault.Visibility = Visibility.Visible;
            ExportButton.IsEnabled = true;
            return;
        }

        global::InSpecTool.Measure.Append(archived, Body.Text);

        // 寫入時記下的那一列：補上歸檔到哪裡，料號、批號、壓合照這裡確認過的值
        using var db = new HelixStudioDbContext(new DbContextOptionsBuilder<HelixStudioDbContext>().UseNpgsql(connection).Options);
        var row = db.inspecs.Single(one => one.id == queue[0].Id);
        var values = JsonSerializer.Deserialize<Dictionary<string, string>>(row.fields) ?? [];

        values["料號"] = Part.Text.Trim();
        row.fields = JsonSerializer.Serialize(values);
        row.archive = archived;
        row.note = string.Join(" ", new[] { "InSpec 自己寫入，操作員在歸檔視窗歸檔。", archived != candidates.First() ? $"路徑找不到，改寫到備份路徑：{candidates.First()}" : null }.Where(one => one is not null));
        row.updated_at = DateTime.UtcNow;
        db.SaveChanges();

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

    #region 模型

    /// <summary>一筆等著歸檔的結果：InSpec 寫入的當下已經記成 inspec 的一列</summary>
    public class PendingItem
    {
        /// <summary>記在 inspec 的哪一列</summary>
        public int Id { get; set; }

        /// <summary>InSpec 寫出的這一筆內容</summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>料號：內容裡對得上的回答，沒有就是左邊欄位填的值</summary>
        public string Part { get; set; } = string.Empty;

        /// <summary>批號</summary>
        public string Lot { get; set; } = string.Empty;

        /// <summary>壓合</summary>
        public string Press { get; set; } = string.Empty;

        /// <summary>InSpec 寫入的時刻</summary>
        public DateTime Time { get; set; }

        /// <summary>寫入當下 InSpec 開著的量測程式名稱</summary>
        public string Program { get; set; } = string.Empty;

        /// <summary>那支量測程式在搜尋資料夾裡的哪一層；找不到是空字串</summary>
        public string ProgramFolder { get; set; } = string.Empty;
    }

    #endregion
}
