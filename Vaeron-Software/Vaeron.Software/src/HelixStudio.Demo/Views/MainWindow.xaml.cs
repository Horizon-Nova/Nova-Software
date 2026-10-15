using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using HelixStudio.Logic;
using HelixStudio.Store;

namespace HelixStudio.Views;

/// <summary>平台的視窗（Demo 版：工具跟安裝檔一起來，不用開通、不匯入套件）</summary>
public partial class MainWindow : Window
{
    private readonly ShellStore store = new();

    private readonly PackageStore packages = new();

    /// <summary>側欄現在畫出來的那幾格；從頭到尾是同一份，只補上或拿掉變動的那幾格</summary>
    private readonly ObservableCollection<ShellLogic.RailItem> rail = [];

    /// <summary>浮在頁面上的那幾層，最近一次被叫出來的排到第幾層</summary>
    private int raised = Chrome;

    /// <summary>程式自己在移側欄的落點（不是人點的），這時不換畫面</summary>
    private bool steering;

    private ShellLogic.ShellView shell;

    /// <summary>跳出來還沒消失的每一則通知</summary>
    private readonly ObservableCollection<ShellLogic.NoteItem> notes = [];

    /// <summary>還沒處理掉的每一次狀況</summary>
    private readonly ObservableCollection<ShellLogic.FaultItem> faults = [];

    /// <summary>開啟平台：先裝上跟安裝檔一起來的工具，再建立畫面、讀出資料，算出外殼要顯示的每一格</summary>
    public MainWindow()
    {
        // 跟安裝檔一起來的工具：安裝資料夾 tools 底下的每一個套件，驗過 Demo 的簽章；這台裝的不是這一版就裝上（第一次開、安裝檔換了新版）
        // 在建立畫面之前裝好，工具目錄一讀就讀得到
        var bundled = Path.Combine(AppContext.BaseDirectory, "tools");
        var read = new[] { bundled }
            .Where(Directory.Exists)
            .SelectMany(one => Directory.EnumerateFiles(one, $"*{PackageStore.Extension}"))
            .Select(packages.Read)
            .ToList();

        var known = store.QueryShellList().Where(one => one.type == "tool").ToList();
        var done = read
            .Where(one => one.Faults.Count == 0)
            .Where(one => known.All(two => two.tool_no != one.Manifests.First().tool_no
                                        || !string.Equals(two.home, PackageStore.Home(one.Manifests.First()), StringComparison.OrdinalIgnoreCase)))
            .Select(one => packages.Install(one, "Demo 安裝檔", $"{store.QueryShellItem($"{one.Manifests.First().tool_no}")?.home}"))
            .ToList();

        // 登錄：第一次裝的新增一列，裝過的換成這一版
        done.Where(one => known.All(two => two.tool_no != one.tool_no)).ToList().ForEach(one => store.InsertTool(one));
        done.Where(one => known.Any(two => two.tool_no == one.tool_no)).ToList().ForEach(one => store.UpdateTool(one));

        InitializeComponent();

        NoteList.ItemsSource = notes;
        FaultList.ItemsSource = faults;

        // 內容要裁成圓角，不然裡面的白底會用直角蓋掉外框那個圓角；半徑向外框拿，扣掉框線厚度
        Body.SizeChanged += (_, _) =>
        {
            var edge = (Border)Body.Parent;

            Body.Clip = new RectangleGeometry(
                new System.Windows.Rect(0, 0, Body.ActualWidth, Body.ActualHeight),
                edge.CornerRadius.TopLeft - edge.BorderThickness.Left,
                edge.CornerRadius.TopLeft - edge.BorderThickness.Top);
        };

        // 工具目錄：雙擊清單或按開啟就開那一支
        Catalog.Launched += (_, toolNo) => Run(toolNo);

        // 指令面板：自己收起來，或選了一列就交給這裡去做
        Palette.Closed += (_, _) => Palette.Visibility = Visibility.Collapsed;
        Palette.Picked += (_, item) => Take(item);

        // 設定自己收起來的時候，側邊那顆按鈕要跟著彈起來
        Settings.Closed += (_, _) => SettingsToggle.IsChecked = false;

        shell = ShellLogic.Shell(store.QueryShellList(), store.QueryIconList());
        DataContext = shell;

        // 驗不過的套件（檔案壞了、不是 Demo 簽的）不裝，把每一項原因說出來
        read.Where(one => one.Faults.Count > 0).ToList()
            .ForEach(one => Note($"「{one.Name}」裝不上：{string.Join(" ", one.Faults)}", "warn"));

        shell.Rail.ForEach(one => rail.Add(one));

        RailList.ItemsSource = rail;

        // 開起來停在工具目錄，側欄的落點也在那一格
        Look(string.Empty);

        // 開多大由設計稿與這台螢幕的工作區一起決定：放得下就照設計稿，放不下就是工作區；位置擺在工作區正中間
        var work = SystemParameters.WorkArea;

        Width = Math.Min(Width, work.Width);
        Height = Math.Min(Height, work.Height);
        Left = work.Left + ((work.Width - Width) / 2);
        Top = work.Top + ((work.Height - Height) / 2);

        // 最大化與還原是同一顆按鈕，圖示、提示與內容的留白都跟著視窗現在的狀態走
        StateChanged += (_, _) => ShowState();
        SizeChanged += (_, _) => ShowState();
        ShowState();
    }

    /// <summary>換回工具目錄；給了工具代碼就在清單上選中它，右邊顯示它的資訊</summary>
    /// <param name="toolNo">要選的工具代碼；只回工具目錄就是空字串</param>
    private void Look(string toolNo)
    {
        new[] { toolNo }.Where(one => one.Length > 0).ToList().ForEach(Catalog.Select);

        Catalog.Visibility = Visibility.Visible;
        Host.Visibility = Visibility.Collapsed;
        Log.Visibility = Visibility.Collapsed;

        CrumbText.Text = shell.Crumb;

        // 側欄落在工具目錄那一格
        steering = true;
        RailList.SelectedIndex = rail
            .Select((one, index) => new { one.Page, index })
            .Where(one => one.Page == "catalog")
            .Select(one => one.index)
            .DefaultIfEmpty(NoRow)
            .First();
        steering = false;
    }

    /// <summary>點側欄的一格：換到那一格的那一頁</summary>
    /// <param name="sender">側欄</param>
    /// <param name="e">這一次換了哪一格</param>
    private void PickRail(object sender, SelectionChangedEventArgs e)
    {
        // 程式自己移落點（看工具資訊、重讀、開工具時跟著走）不算點
        if (steering) return;

        var picked = e.AddedItems.OfType<ShellLogic.RailItem>().ToList();

        // 第一格：回工具目錄
        picked.Where(one => one.Page == "catalog").ToList().ForEach(_ => Look(string.Empty));

        // 一支工具：直接開它的畫面
        picked.Where(one => one.Page == "tool").ToList().ForEach(one => Run(one.Key));

        // 日誌：照現在的紀錄重讀一次，換過去
        picked.Where(one => one.Page == "log").ToList().ForEach(one =>
        {
            Log.Refresh();

            Log.Visibility = Visibility.Visible;
            Catalog.Visibility = Visibility.Collapsed;
            Host.Visibility = Visibility.Collapsed;

            CrumbText.Text = $"— {one.Name}";
        });
    }

    /// <summary>跳出一則通知：停到它該停的秒數為止，同時最多三則</summary>
    /// <param name="text">要說的話</param>
    /// <param name="kind">哪一種：info 或 warn</param>
    public async void Note(string text, string kind)
    {
        var note = ShellLogic.Note(text, kind, shell.Icons);

        notes.Add(note);
        notes.Take(Math.Max(0, notes.Count - Stacked)).ToList().ForEach(one => notes.Remove(one));

        // 通知音效
        Enumerable.Repeat(true, 1).Where(_ => ShellLogic.Setup.Sound).ToList()
            .ForEach(_ => System.Media.SystemSounds.Asterisk.Play());

        // 一格一格走完它該停的時間；滑鼠停在上面那幾格不算
        var left = note.Seconds * Second;

        while (left > 0 && notes.Contains(note))
        {
            await Task.Delay(Grain);

            left -= Grain * Counting[note.Held];
        }

        notes.Remove(note);
    }

    /// <summary>滑鼠停在一則通知上：計時跟著停</summary>
    private void HoldNote(object sender, MouseEventArgs e) =>
        ((ShellLogic.NoteItem)((FrameworkElement)sender).DataContext).Held = true;

    /// <summary>滑鼠離開一則通知：計時接著走</summary>
    private void ResumeNote(object sender, MouseEventArgs e) =>
        ((ShellLogic.NoteItem)((FrameworkElement)sender).DataContext).Held = false;

    /// <summary>把一則通知關掉</summary>
    private void DropNote(object sender, RoutedEventArgs e) =>
        notes.Remove((ShellLogic.NoteItem)((Button)sender).DataContext);

    /// <summary>某一步沒做成：把它擺到畫面上，停到使用者處理掉為止</summary>
    /// <param name="detail">系統回報的原文</param>
    public void Fail(string detail)
    {
        faults.Add(ShellLogic.Fault(detail, shell.Icons));

        // 同一件事重複發生時只留一條，不疊成一排
        faults.GroupBy(one => one.Detail).SelectMany(one => one.Skip(1)).ToList().ForEach(one => faults.Remove(one));
    }

    /// <summary>重試：把資料重讀一次，讀得到就把這一條收掉</summary>
    private void Retry(object sender, RoutedEventArgs e)
    {
        Catalog.Refresh();

        faults.Remove((ShellLogic.FaultItem)((Button)sender).DataContext);
    }

    /// <summary>把一條狀況關掉</summary>
    private void DropFault(object sender, RoutedEventArgs e) =>
        faults.Remove((ShellLogic.FaultItem)((Button)sender).DataContext);

    /// <summary>快捷鍵：Ctrl K 開指令面板</summary>
    private void Shortcut(object sender, KeyEventArgs e)
    {
        var held = Keyboard.Modifiers == ModifierKeys.Control;

        new[] { e.Key }.Where(_ => held).Where(one => one == Key.K).ToList()
            .ForEach(_ =>
            {
                e.Handled = true;

                OpenPalette(this, new RoutedEventArgs());
            });
    }

    /// <summary>開起一支工具：把它自己的畫面掛在內容區，記一筆開啟</summary>
    /// <param name="toolNo">這一支工具的代碼</param>
    private void Run(string toolNo)
    {
        var name = rail.Concat(shell.Rail).Where(two => two.Key == toolNo).Select(two => two.Name).DefaultIfEmpty(toolNo).First();
        store.InsertEventLog("open", toolNo, $"{nameof(MainWindow)}.{nameof(Run)}", $"開啟 {name}", null);

        Host.Open(toolNo);

        Host.Visibility = Visibility.Visible;
        Catalog.Visibility = Visibility.Collapsed;
        Log.Visibility = Visibility.Collapsed;

        CrumbText.Text = $"— {name}";

        // 側欄的落點跟著走
        steering = true;
        RailList.SelectedIndex = rail
            .Select((two, index) => new { two.Key, index })
            .Where(two => two.Key == toolNo)
            .Select(two => two.index)
            .DefaultIfEmpty(NoRow)
            .First();
        steering = false;
    }

    /// <summary>打開指令面板：頂部搜尋、頭像、Ctrl K 都是同一件事</summary>
    private void OpenPalette(object sender, RoutedEventArgs e)
    {
        // 剛被叫出來的那一層在最前面
        Panel.SetZIndex(Palette, ++raised);

        Palette.Visibility = Visibility.Visible;

        Palette.Open();
    }

    /// <summary>選了面板上的一列：照那一列帶著的東西做</summary>
    /// <param name="item">選中的那一列</param>
    private void Take(PaletteLogic.PaletteItem item)
    {
        // 選過的工具記起來，下次打開面板就排在最近使用
        item.Remembers.ForEach(one => store.InsertSearch(one));

        // 一支工具：在工具目錄選中它
        item.Opens.ForEach(one => Look(one));

        // 回工具目錄
        item.Catalogs.ForEach(_ => Look(string.Empty));

        // 開設定
        item.Settings.ForEach(_ => SettingsToggle.IsChecked = true);
    }

    /// <summary>開啟或關掉設定</summary>
    private void ToggleSettings(object sender, RoutedEventArgs e)
    {
        // 剛被叫出來的那一層在最前面
        Panel.SetZIndex(Settings, ++raised);

        new[] { SettingsToggle.IsChecked == true }.Where(one => one).ToList().ForEach(_ => Settings.Open());

        Settings.Visibility = Showing[SettingsToggle.IsChecked == true];
    }

    /// <summary>跳至主內容：把鍵盤焦點送進內容區</summary>
    private void SkipToMain(object sender, RoutedEventArgs e) =>
        Body.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));

    /// <summary>把視窗縮到工作列</summary>
    private void Minimize(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    /// <summary>在最大化與還原之間切換</summary>
    private void ToggleMax(object sender, RoutedEventArgs e) =>
        WindowState = new[] { WindowState }
            .Where(one => one == WindowState.Maximized)
            .Select(_ => WindowState.Normal)
            .DefaultIfEmpty(WindowState.Maximized)
            .First();

    /// <summary>關閉視窗</summary>
    private void CloseWindow(object sender, RoutedEventArgs e) =>
        Close();

    /// <summary>視窗現在的狀態：按鈕的圖示與提示，還有內容要離視窗邊緣多遠</summary>
    private void ShowState()
    {
        var maximized = new[] { WindowState }.Where(one => one == WindowState.Maximized).ToList();

        MaxButton.Content = maximized
            .Select(_ => shell.Icons["copy"])
            .DefaultIfEmpty(shell.Icons["square"])
            .First();

        MaxButton.ToolTip = maximized
            .Select(_ => "向下還原")
            .DefaultIfEmpty("最大化")
            .First();

        // 最大化時視窗的框比工作區大一圈，超出去的那一圈畫不到螢幕上；量出超出多少，內容就往內退多少
        var source = PresentationSource.FromVisual(this);
        var handle = new WindowInteropHelper(this).Handle;

        Root.Margin = maximized
            .Where(_ => source != null && handle != IntPtr.Zero)
            .Select(_ =>
            {
                // 視窗現在的框，與它所在那一台螢幕扣掉工作列之後的工作區，兩個都是實際像素
                GetWindowRect(handle, out var window);
                var info = new MonitorInfo { cbSize = Marshal.SizeOf<MonitorInfo>() };
                GetMonitorInfo(MonitorFromWindow(handle, Nearest), ref info);

                // 實際像素換成畫面座標，內容才不會因為螢幕縮放比例而退得太多或太少
                var scale = source!.CompositionTarget.TransformFromDevice;

                return new Thickness(
                    (info.work.left - window.left) * scale.M11,
                    (info.work.top - window.top) * scale.M22,
                    (window.right - info.work.right) * scale.M11,
                    (window.bottom - info.work.bottom) * scale.M22);
            })
            .DefaultIfEmpty(new Thickness(0))
            .First();
    }

    #region 視窗管理員

    /// <summary>通知同時最多疊幾則</summary>
    private const int Stacked = 3;

    /// <summary>側欄一格都沒落在上面</summary>
    private const int NoRow = -1;

    /// <summary>視窗自己那幾件（跳至主內容、標題列）疊到第幾層；浮起來的東西從這之上開始疊</summary>
    private const int Chrome = 3;

    /// <summary>開著就看得到，收起來就不占位置</summary>
    private static IReadOnlyDictionary<bool, Visibility> Showing { get; } = new Dictionary<bool, Visibility>
    {
        [true] = Visibility.Visible,
        [false] = Visibility.Collapsed,
    };

    /// <summary>一秒有幾毫秒</summary>
    private const int Second = 1000;

    /// <summary>計時一次走幾毫秒</summary>
    private const int Grain = 100;

    /// <summary>停著的時候這一格不算，沒停著才算</summary>
    private static IReadOnlyDictionary<bool, int> Counting { get; } = new Dictionary<bool, int>
    {
        [true] = 0,
        [false] = 1,
    };

    /// <summary>找螢幕時取離視窗最近的那一台</summary>
    private const int Nearest = 0x00000002;

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr handle, int flags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr handle, out Rect rect);

    #endregion

    #region 模型

    /// <summary>一塊長方形</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        /// <summary>左</summary>
        public int left;

        /// <summary>上</summary>
        public int top;

        /// <summary>右</summary>
        public int right;

        /// <summary>下</summary>
        public int bottom;
    }

    /// <summary>這一台螢幕</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct MonitorInfo
    {
        /// <summary>這份資料多大</summary>
        public int cbSize;

        /// <summary>整片螢幕</summary>
        public Rect monitor;

        /// <summary>扣掉工作列之後的工作區</summary>
        public Rect work;

        /// <summary>旗標</summary>
        public int flags;
    }

    #endregion
}
