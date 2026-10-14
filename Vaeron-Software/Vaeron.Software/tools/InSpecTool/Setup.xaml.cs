using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using HelixStudio.Contract;
using Models.HelixStudio;

namespace InSpecTool;

/// <summary>InSpecTool 的設定：掛在平台設定的「工具 › InSpecTool」那一節</summary>
public partial class Setup : UserControl, ITool, IData
{
    /// <summary>平台載入後塞進來的保險箱；這支工具用不到，只為符合合約</summary>
    public ISecrets Secrets { get; set; } = null!;

    /// <summary>平台載入後塞進來的資料庫連線</summary>
    public string Connection { get; set; } = string.Empty;

    private readonly List<RowItem> rows = [];

    /// <summary>備好這一節</summary>
    public Setup()
    {
        InitializeComponent();

        FieldRows.ItemsSource = rows;

        // 平台的核取方塊與下拉要的圖示，加上新增、更多動作（lucide）
        DataContext = new
        {
            CheckPaths = new List<string> { "M20 6 9 17l-5-5" },
            DownPaths = new List<string> { "m6 9 6 6 6-6" },
            PlusPaths = new List<string> { "M5 12h14", "M12 5v14" },
            MorePaths = new List<string>
            {
                "M 11,5 A 1,1 0 1 1 13,5 A 1,1 0 1 1 11,5 Z",
                "M 11,12 A 1,1 0 1 1 13,12 A 1,1 0 1 1 11,12 Z",
                "M 11,19 A 1,1 0 1 1 13,19 A 1,1 0 1 1 11,19 Z",
            },
        };
    }

    /// <summary>掛上來：從 vw_inspec_setup 讀設定與欄位結構</summary>
    /// <param name="sender">這一節</param>
    /// <param name="e">掛上來這一次</param>
    private void Ready(object sender, RoutedEventArgs e)
    {
        using var db = Data();
        var all = db.vw_inspec_setups.AsNoTracking().ToList();
        var setup = all.Single(one => one.type == "setting");

        SearchFolder.Text = $"{setup.search_folder}";
        LogFolder.Text = $"{setup.log_folder}";
        BackupFolder.Text = $"{setup.backup_folder}";
        ShowResult.IsChecked = setup.show_result;

        Interval.ItemsSource = setup.scan_options;
        Interval.SelectedItem = $"{setup.scan_interval}";

        rows.Clear();
        rows.AddRange(all.Where(one => one.type == "field").Select((one, at) => new RowItem
        {
            Order = at + 1,
            Name = $"{one.name}",
            Prompt = $"{one.prompt}",
            Mode = one.confirm == true ? 1 : 0,
            Editable = one.editable == true,
        }));
        FieldRows.Items.Refresh();
    }

    /// <summary>選一個資料夾</summary>
    /// <param name="sender">瀏覽（Tag 是哪一項設定）</param>
    /// <param name="e">這一次點擊</param>
    private void Browse(object sender, RoutedEventArgs e)
    {
        var key = $"{((FrameworkElement)sender).Tag}";
        var box = new[] { SearchFolder, LogFolder, BackupFolder }.First(one => $"{one.Tag}" == key);
        using var pick = new System.Windows.Forms.FolderBrowserDialog { Description = "選擇資料夾", UseDescriptionForTitle = true, SelectedPath = Directory.Exists(box.Text) ? box.Text : string.Empty };

        if (pick.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

        box.Text = pick.SelectedPath;
        Keep(key, box.Text.Trim());
    }

    /// <summary>路徑框離開時存起來</summary>
    /// <param name="sender">路徑框（Tag 是哪一項設定）</param>
    /// <param name="e">離開這一格</param>
    private void PathChanged(object sender, RoutedEventArgs e)
    {
        var box = (TextBox)sender;

        Keep($"{box.Tag}", box.Text.Trim());
    }

    /// <summary>條碼解析間隔改了</summary>
    /// <param name="sender">下拉</param>
    /// <param name="e">這一次選取</param>
    private void IntervalChanged(object sender, SelectionChangedEventArgs e)
    {
        // 掛上來時放進去的那一次不算改
        if (Interval.SelectedItem is not string chosen || e.RemovedItems.Count == 0) return;

        Keep("scanInterval", chosen);
    }

    /// <summary>跑完要不要顯示結果</summary>
    /// <param name="sender">勾選框</param>
    /// <param name="e">這一次點擊</param>
    private void ShowResultChanged(object sender, RoutedEventArgs e) =>
        Keep("showResult", ShowResult.IsChecked == true ? "true" : "false");

    /// <summary>新增一個欄位：最下面多一列，游標放在它的名稱；填了名稱離開就存</summary>
    /// <param name="sender">新增欄位</param>
    /// <param name="e">這一次點擊</param>
    private void Add(object sender, RoutedEventArgs e)
    {
        rows.Add(new RowItem { Order = rows.Count + 1, Editable = true });
        FieldRows.Items.Refresh();
        FieldRows.UpdateLayout();

        var last = (ContentPresenter)FieldRows.ItemContainerGenerator.ContainerFromIndex(rows.Count - 1);
        FindName<TextBox>(last)?.Focus();
    }

    /// <summary>點更多動作：在那顆按鈕下面打開上移、下移、移除</summary>
    /// <param name="sender">更多動作</param>
    /// <param name="e">這一次點擊</param>
    private void More(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;

        button.ContextMenu.PlacementTarget = button;
        button.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        button.ContextMenu.IsOpen = true;
    }

    /// <summary>上移或下移一個欄位，條碼分段的順序跟著換，當場存</summary>
    /// <param name="sender">上移或下移（Tag 是 -1 或 1）</param>
    /// <param name="e">這一次點擊</param>
    private void Move(object sender, RoutedEventArgs e)
    {
        var row = (RowItem)((FrameworkElement)sender).DataContext;
        var to = Math.Clamp(rows.IndexOf(row) + int.Parse($"{((FrameworkElement)sender).Tag}"), 0, rows.Count - 1);

        rows.Remove(row);
        rows.Insert(to, row);
        rows.Select((one, at) => (one, at)).ToList().ForEach(each => each.one.Order = each.at + 1);
        FieldRows.Items.Refresh();

        Save(sender, e);
    }

    /// <summary>移除一個欄位，當場存</summary>
    /// <param name="sender">移除</param>
    /// <param name="e">這一次點擊</param>
    private void Remove(object sender, RoutedEventArgs e)
    {
        rows.Remove((RowItem)((FrameworkElement)sender).DataContext);
        rows.Select((one, at) => (one, at)).ToList().ForEach(each => each.one.Order = each.at + 1);
        FieldRows.Items.Refresh();

        Save(sender, e);
    }

    /// <summary>填法改了就存；掛上來時放進去的那一次不算</summary>
    /// <param name="sender">填法</param>
    /// <param name="e">這一次選取</param>
    private void ModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.RemovedItems.Count == 0) return;

        Save(sender, e);
    }

    /// <summary>儲存欄位結構：整份換掉，順序就是條碼分段的順序；每改一格就存一次</summary>
    /// <param name="sender">改了的那一格</param>
    /// <param name="e">這一次改動</param>
    private void Save(object sender, RoutedEventArgs e)
    {
        // 名稱是空的那一列不存
        var kept = rows.Where(one => one.Name.Trim().Length > 0).ToList();

        using var db = Data();
        db.inspecs.RemoveRange(db.inspecs.Where(one => one.type == "field"));
        db.inspecs.AddRange(kept.Select((one, at) => new inspec
        {
            type = "field",
            name = one.Name.Trim(),
            prompt = one.Prompt.Trim(),
            mode = one.Mode == 1 ? "confirm" : "fill",
            editable = one.Editable,
            sort = at + 1,
            created_at = DateTime.UtcNow,
            updated_at = DateTime.UtcNow,
        }));
        db.SaveChanges();

        SaveText.Text = $"已儲存 {kept.Count} 個欄位 · {DateTime.Now:HH:mm:ss}";
    }

    #region 對照

    /// <summary>在一列裡找第一個某種控制項</summary>
    /// <param name="root">那一列</param>
    private static T? FindName<T>(DependencyObject root) where T : DependencyObject =>
        Enumerable.Range(0, System.Windows.Media.VisualTreeHelper.GetChildrenCount(root))
            .Select(at => System.Windows.Media.VisualTreeHelper.GetChild(root, at))
            .Select(one => one as T ?? FindName<T>(one))
            .FirstOrDefault(one => one is not null);

    /// <summary>平台的資料庫</summary>
    private HelixStudioDbContext Data() =>
        new(new DbContextOptionsBuilder<HelixStudioDbContext>().UseNpgsql(Connection).Options);

    /// <summary>存一項設定</summary>
    /// <param name="key">哪一項</param>
    /// <param name="value">新的值</param>
    private void Keep(string key, string value)
    {
        using var db = Data();
        db.inspecs.Where(one => one.type == "setting" && one.setting_key == key)
            .ExecuteUpdate(set => set.SetProperty(one => one.value, value).SetProperty(one => one.updated_at, DateTime.UtcNow));
    }

    #endregion

    #region 模型

    /// <summary>欄位結構的一列</summary>
    public class RowItem
    {
        /// <summary>第幾欄，也是條碼的第幾段</summary>
        public int Order { get; set; }

        /// <summary>欄位名稱</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>對應的 InSpec 提示文字</summary>
        public string Prompt { get; set; } = string.Empty;

        /// <summary>0 直接填、1 先確認再填（下拉的位置）</summary>
        public int Mode { get; set; }

        /// <summary>主頁上可否改</summary>
        public bool Editable { get; set; }
    }

    #endregion
}
