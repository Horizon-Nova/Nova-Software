using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using HelixStudio.Logic;
using HelixStudio.Service;
using HelixStudio.Store;
using Microsoft.Win32;

namespace HelixStudio.Views;

/// <summary>平台的視窗</summary>
public partial class MainWindow : Window
{
    private readonly ShellService service = new();

    /// <summary>正在問要不要移除的那一支工具的名稱</summary>
    private string removing = string.Empty;

    /// <summary>側欄現在畫出來的那幾格；從頭到尾是同一份，只補上或拿掉變動的那幾格</summary>
    private readonly ObservableCollection<ShellLogic.RailItem> rail = [];

    /// <summary>浮在頁面上的那幾層，最近一次被叫出來的排到第幾層</summary>
    private int raised = Chrome;

    /// <summary>程式自己在移側欄的落點（不是人點的），這時不換畫面</summary>
    private bool steering;

    /// <summary>正在等工具金鑰啟用的那一支工具</summary>
    private string pending = string.Empty;

    private ShellLogic.ShellView shell;

    /// <summary>跳出來還沒消失的每一則通知</summary>
    private readonly ObservableCollection<ShellLogic.NoteItem> notes = [];

    /// <summary>還沒處理掉的每一次狀況</summary>
    private readonly ObservableCollection<ShellLogic.FaultItem> faults = [];

    /// <summary>開啟平台：讀出資料，算出外殼要顯示的每一格</summary>
    public MainWindow()
    {
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

        // 匯入工具套件
        Catalog.Importing += (_, _) => Bring();

        // 右邊那一欄按了移除工具：遮罩蓋住整個視窗問一次
        Catalog.Info.Asking += (_, name) => AskRemove(name);


        shell = service.LoadShell();
        DataContext = shell;

        shell.Rail.ForEach(one => rail.Add(one));

        RailList.ItemsSource = rail;

        // 開起來停在工具目錄，側欄的落點也在那一格
        Look(string.Empty);

        // 在背景跑的工具：要把自己叫到前面時，主視窗還原、叫到前面、開它
        Host.Show = toolNo =>
        {
            WindowState = WindowState == WindowState.Minimized ? WindowState.Normal : WindowState;
            Activate();
            Run(toolNo);
        };

        // 設成自動啟動的工具：授權准裝、工具金鑰配得上、組件檔在的，現在就把畫面建好在背景跑（不掛上）；之後點進去掛的是同一份
        service.LoadAutostarts().ForEach(Host.Start);

        // 工具目錄右邊切了自動啟動：切成開就現在在背景跑起來；說一句，清單跟著換
        Catalog.Info.Autostarted += (_, one) =>
        {
            new[] { one.ToolNo }.Where(_ => one.On).ToList().ForEach(Host.Start);
            Note(one.Said, "info");
            Catalog.Refresh();
        };

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
        Activation.Visibility = Visibility.Collapsed;

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
            Activation.Visibility = Visibility.Collapsed;

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

        // 一格一格走完它該停的時間
        var left = note.Seconds * Second;

        while (left > 0 && notes.Contains(note))
        {
            await Task.Delay(Grain);

            left -= Grain;
        }

        notes.Remove(note);
    }



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

    /// <summary>工具的狀態被改過，整個外殼重讀一次</summary>
    /// <remarks>側欄列哪幾支、徽章幾則、狀態列的已安裝與可更新、合規那一格，全都跟著這一份走。</remarks>
    public void Reread()
    {
        // 現在停在哪一格，等一下要放回去
        var here = RailList.SelectedItems.OfType<ShellLogic.RailItem>()
            .Select(one => one.Key)
            .DefaultIfEmpty(string.Empty)
            .First();

        // 重讀一份：側欄、徽章、狀態列的數字都在裡面
        shell = service.LoadShell();

        DataContext = shell;

        // 側欄照這一次的結果補差額，沒變的那幾格原封不動；整列重建會讓選中的那一格閃一下
        rail.Where(one => shell.Rail.All(two => two.Key != one.Key)).ToList()
            .ForEach(one => rail.Remove(one));

        shell.Rail.Where(one => rail.All(two => two.Key != one.Key)).ToList()
            .ForEach(one => rail.Insert(shell.Rail.IndexOf(one), one));

        shell.Rail.Select((one, index) => new { one, index })
            .Where(one => rail[one.index].Mark != one.one.Mark)
            .ToList()
            .ForEach(one => rail[one.index] = one.one);

        // 剛才那一格如果還在就回到它，不在就一格都不落
        steering = true;
        RailList.SelectedIndex = rail
            .Select((one, index) => new { one.Key, index })
            .Where(one => one.Key == here)
            .Select(one => one.index)
            .DefaultIfEmpty(NoRow)
            .First();
        steering = false;

        Catalog.Refresh();
    }

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

    /// <summary>開起一支工具：授權、工具金鑰都過了才把它自己的畫面掛在內容區；金鑰不行就請操作員貼一把</summary>
    /// <param name="toolNo">這一支工具的代碼</param>
    private void Run(string toolNo)
    {
        var view = service.LoadOpened(toolNo);

        // 授權不准：說出來，停在工具資訊那一頁
        view.Warnings.ForEach(one => Note(one, "warn"));
        Enumerable.Repeat(toolNo, 1).Where(_ => view.Warnings.Count > 0).ToList().ForEach(Look);

        // 工具金鑰沒有或配不上：請操作員貼一把（啟用），這一次先不開
        view.Activations.ForEach(one =>
        {
            pending = toolNo;
            ActivationTitle.Text = one.Title;
            ActivationAsk.Text = one.Ask;
            ActivationKey.Clear();
            ActivationFault.Text = one.Fault;
            Activation.Visibility = Visibility.Visible;
            ActivationKey.Focus();
        });

        // 開得起來：把它的畫面備好、換過去
        Enumerable
            .Repeat(toolNo, 1)
            .Where(_ => view.Open)
            .ToList()
            .ForEach(one =>
            {
                Activation.Visibility = Visibility.Collapsed;

                Host.Open(one);

                Host.Visibility = Visibility.Visible;
                Catalog.Visibility = Visibility.Collapsed;
                Log.Visibility = Visibility.Collapsed;

                CrumbText.Text = $"— {view.Name}";

                // 側欄的落點跟著走
                steering = true;
                RailList.SelectedIndex = rail
                    .Select((two, index) => new { two.Key, index })
                    .Where(two => two.Key == one)
                    .Select(two => two.index)
                    .DefaultIfEmpty(NoRow)
                    .First();
                steering = false;
            });
    }

    /// <summary>啟用工具：驗貼上的工具金鑰，配得上就存起來、記一筆啟用，接著開這支工具</summary>
    /// <param name="sender">啟用</param>
    /// <param name="e">這一次點擊</param>
    private void Activate(object sender, RoutedEventArgs e)
    {
        var (fault, name) = service.LoadActivated(pending, ActivationKey.Text.Trim());

        // 配不上：原因寫在框下面，金鑰留著讓操作員改
        ActivationFault.Text = fault;
        if (fault.Length > 0) return;

        Note($"已啟用 {name}", "info");

        Run(pending);
    }

    /// <summary>不啟用：回工具目錄</summary>
    /// <param name="sender">回工具目錄</param>
    /// <param name="e">這一次點擊</param>
    private void CancelActivation(object sender, RoutedEventArgs e)
    {
        Activation.Visibility = Visibility.Collapsed;
        Look(string.Empty);
    }

    /// <summary>移除工具：先問一次，打對名稱才移；遮罩蓋住整個視窗</summary>
    /// <param name="name">要移除的那一支工具的名稱</param>
    private void AskRemove(string name)
    {
        removing = name;
        RemoveAsk.DataContext = new { Name = name };
        ConfirmText.Clear();
        ConfirmButton.IsEnabled = false;
        RemoveAsk.Visibility = Visibility.Visible;

        Dispatcher.BeginInvoke(() => ConfirmText.Focus(), System.Windows.Threading.DispatcherPriority.Input);
    }

    /// <summary>在確認框裡打字：打對名稱才按得下移除</summary>
    private void TypeName(object sender, TextChangedEventArgs e) =>
        ConfirmButton.IsEnabled = ConfirmText.Text == removing;

    /// <summary>保留這支工具：收掉問的那一層</summary>
    private void KeepTool(object sender, RoutedEventArgs e) =>
        RemoveAsk.Visibility = Visibility.Collapsed;

    /// <summary>在問的那一層按 Esc：保留這支工具</summary>
    private void RemoveKey(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;

        e.Handled = true;
        KeepTool(sender, e);
    }

    /// <summary>名稱打對了：交給右邊那一欄移除，收掉問的那一層</summary>
    private void RemoveTool(object sender, RoutedEventArgs e)
    {
        RemoveAsk.Visibility = Visibility.Collapsed;
        Catalog.Info.Remove();
    }
    /// <summary>複製申請碼，按鈕換成勾停一下</summary>
    /// <param name="sender">複製鈕</param>
    /// <param name="e">這一次點擊</param>
    private void CopyAsk(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(ActivationAsk.Text);
        Note("已複製申請碼", "info");
    }

    /// <summary>匯入工具套件：選檔案、驗章、解開、登錄成工具目錄裡的一支</summary>
    private void Bring()
    {
        // 讓使用者挑套件檔；沒挑就什麼都不做
        var box = new OpenFileDialog
        {
            Title = "匯入工具套件",
            Filter = $"工具套件（*{PackageStore.Extension}）|*{PackageStore.Extension}",
            Multiselect = true,
            InitialDirectory = ShellLogic.Setup.Source,
        };

        var chosen = new[] { box.ShowDialog() == true }.Where(one => one).SelectMany(_ => box.FileNames).ToList();

        // 驗章、驗授權、解開、記一次匯入；每一句照實說
        var said = service.LoadImported(chosen);

        // 側欄、狀態列、工具目錄都跟著這一次的結果換
        Enumerable.Repeat(0, 1).Where(_ => said.Any(one => one.Kind == "info")).ToList().ForEach(_ => Reread());

        said.ForEach(one => Note(one.Text, one.Kind));
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
        new CatalogService().LoadSearched(item.Remembers);

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
