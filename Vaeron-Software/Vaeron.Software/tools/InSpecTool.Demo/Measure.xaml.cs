using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using HelixStudio.Contract;
using InSpecTool.Logic;
using InSpecTool.Service;
using InSpecTool.Store;

namespace InSpecTool;

/// <summary>InSpecTool 的量測頁（Demo 版：照常執行量測，不歸檔）：左邊條碼與欄位，右邊量測程式與這次的結果</summary>
/// <remarks>標籤與說明的字寫在 XAML；這裡把值擺上去、把操作交給 InspecService。</remarks>
public partial class Measure : UserControl, ITool, IData
{
    /// <summary>平台載入後塞進來的保險箱；這支工具用不到，只為符合合約</summary>
    public ISecrets Secrets { get; set; } = null!;

    /// <summary>平台載入後塞進來的資料庫連線</summary>
    public string Connection { get; set; } = string.Empty;

    /// <summary>這支工具的串接；掛上來時照平台塞進來的連線建立</summary>
    private InspecService service = null!;

    /// <summary>這一頁打開時讀的那一份：設定、欄位、最近執行</summary>
    private InspecLogic.MeasureView view = new();

    private readonly DispatcherTimer scan = new();

    private bool running;

    private bool typing = true;

    /// <summary>備好這一頁</summary>
    public Measure()
    {
        InitializeComponent();

        scan.Tick += (_, _) => Parse();

        // 平台的核取方塊要的勾（lucide check）
        DataContext = new { CheckPaths = new List<string> { "M20 6 9 17l-5-5" } };
    }

    #region 量測

    /// <summary>畫面掛上來：讀設定與欄位，條碼框取得焦點</summary>
    /// <param name="sender">這一頁</param>
    /// <param name="e">掛上來這一次</param>
    private void Ready(object sender, RoutedEventArgs e)
    {
        // 讀設定、欄位與最近執行；設定在這裡讀一次，改了設定重新開這支工具
        service = new InspecService(new InspecStore(Connection));
        view = service.LoadMeasure();

        Fields.ItemsSource = view.Fields;
        RecentHeads.ItemsSource = view.Heads;
        Recent.ItemsSource = view.Recent;

        ShowNext.IsChecked = view.ShowResult;
        ShowAfter.IsChecked = view.ShowResult;

        // 左欄上次拖到多寬；沒拖過就照版面比例
        if (view.LeftWidth is double left)
        {
            LeftColumn.Width = new GridLength(left);
            RightColumn.Width = new GridLength(1, GridUnitType.Star);
        }

        // 執行時 InSpec 匯出到這個檔
        Directory.CreateDirectory(Path.GetDirectoryName(InspecLogic.InputFile)!);

        Barcode.Focus();
        Barcode.SelectAll();

        Search(false);
    }

    /// <summary>條碼框有字進來：停手「條碼解析間隔」之後才解析</summary>
    /// <param name="sender">條碼框</param>
    /// <param name="e">這一次改字</param>
    private void Typed(object sender, TextChangedEventArgs e)
    {
        if (!typing) return;

        scan.Stop();
        scan.Interval = TimeSpan.FromMilliseconds(view.ScanInterval);
        scan.Start();
    }

    /// <summary>解析條碼：以 | 分段依序填入欄位，料號整理成找檔用的樣子，然後找量測程式</summary>
    private void Parse()
    {
        scan.Stop();

        var parsed = service.LoadParsed(Barcode.Text, view.Fields);

        BarcodeFault.Visibility = parsed.Blocked ? Visibility.Visible : Visibility.Collapsed;
        view.Fields.Select((one, at) => (one, at)).ToList().ForEach(each => each.one.Value = parsed.Values[each.at]);

        Search(false);
    }

    /// <summary>改了一個欄位：條碼跟著欄位的值，重新找量測程式（欄位格子不重畫，點下一格一次就進得去）</summary>
    /// <param name="sender">那一格</param>
    /// <param name="e">離開這一格</param>
    private void Edited(object sender, RoutedEventArgs e)
    {
        typing = false;
        Barcode.Text = service.LoadJoined(view.Fields);
        typing = true;

        Search(false);
    }

    /// <summary>量測頁變寬變窄：左欄最寬只到右欄還留得下它的最小寬度，拖到最右邊或視窗縮小時查詢結果都還在</summary>
    /// <remarks>用量測頁自己的寬度算；兩欄那一層會被右欄的最小寬度撐開，拿它算會跟著變大。</remarks>
    /// <param name="sender">量測頁</param>
    /// <param name="e">這一次大小變化</param>
    private void Fit(object sender, SizeChangedEventArgs e)
    {
        LeftColumn.MaxWidth = Math.Max(LeftColumn.MinWidth, e.NewSize.Width - Splitter - RightColumn.MinWidth);
    }

    /// <summary>拖完中間的分隔：記住左欄多寬，下次打開照這個寬度</summary>
    /// <param name="sender">分隔</param>
    /// <param name="e">這一次拖曳</param>
    private void Resized(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        var width = Math.Round(LeftColumn.ActualWidth);

        service.LoadChanged("leftWidth", null, (decimal)width, null);
        view.LeftWidth = width;
    }

    /// <summary>清除條碼與欄位</summary>
    /// <param name="sender">清除</param>
    /// <param name="e">這一次點擊</param>
    private void Clear(object sender, RoutedEventArgs e)
    {
        typing = false;
        Barcode.Clear();
        typing = true;

        view.Fields.ForEach(one => one.Value = string.Empty);
        BarcodeFault.Visibility = Visibility.Collapsed;

        Search(false);
        Barcode.Focus();
    }

    /// <summary>重新查找：丟掉資料夾快取，重新找一次</summary>
    /// <param name="sender">重新查找</param>
    /// <param name="e">這一次點擊</param>
    private void Again(object sender, RoutedEventArgs e) =>
        Search(true);

    /// <summary>找量測程式：檔名含 SETUP 與料號，新的在上；只有一支就直接選好，按 Enter 就跑</summary>
    /// <param name="again">按了重新查找</param>
    private async void Search(bool again)
    {
        AgainButton.IsEnabled = false;
        var found = await service.LoadPrograms(view, view.Fields, again);
        AgainButton.IsEnabled = !running;

        Programs.ItemsSource = found.Programs;
        ProgramsEmptyText.Text = found.Empty;
        ProgramsEmpty.Visibility = found.Programs.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ProgramCount.Text = $"{found.Programs.Count}";
        Programs.SelectedIndex = found.Programs.Count == 1 ? 0 : -1;
    }

    /// <summary>在清單上按 Enter 就是執行</summary>
    /// <param name="sender">清單</param>
    /// <param name="e">按下的鍵</param>
    private void ProgramKey(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;

        e.Handled = true;
        Run(sender, e);
    }

    /// <summary>執行量測：開檔、自動填提示、等 InSpec 跑完，記下這一次的結果</summary>
    /// <param name="sender">雙擊的那一列，或按 Enter 的清單</param>
    /// <param name="e">這一次操作</param>
    private async void Run(object sender, RoutedEventArgs e)
    {
        // 選了哪一支；正在跑就不再送
        if (Programs.SelectedItem is not InspecLogic.ProgramItem program || running) return;

        // 跑的時候左邊不能改、不能再按；跑完或中途出錯都解開，不會卡住下一次
        running = true;
        Inputs.IsEnabled = false;
        AgainButton.IsEnabled = false;
        Programs.IsEnabled = false;
        ResultLayer.Visibility = Visibility.Collapsed;

        InspecLogic.ResultView result;
        try
        {
            result = await service.LoadRun(view, program, view.Fields, (name, ask, value) => Confirm.Ask(Window.GetWindow(this), name, ask, value));
        }
        finally
        {
            running = false;
            Inputs.IsEnabled = true;
            AgainButton.IsEnabled = true;
            Programs.IsEnabled = true;
        }

        // 最近執行多了這一次；這次的結果照要不要顯示蓋上來
        if (result.Ran) Recent.ItemsSource = result.Recent;
        if (!result.Shown) return;

        Tell(result);
        ShowNext.IsChecked = view.ShowResult;
    }

    /// <summary>關掉這次的結果</summary>
    /// <param name="sender">關閉</param>
    /// <param name="e">這一次點擊</param>
    private void CloseResult(object sender, RoutedEventArgs e)
    {
        ResultLayer.Visibility = Visibility.Collapsed;
        Barcode.Focus();
        Barcode.SelectAll();
    }

    /// <summary>結果蓋著的時候按 Esc 就是關閉</summary>
    /// <param name="sender">結果那一層</param>
    /// <param name="e">按下的鍵</param>
    private void ResultKeys(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;

        e.Handled = true;
        CloseResult(sender, e);
    }

    /// <summary>下次跑完要不要顯示結果</summary>
    /// <param name="sender">勾選框</param>
    /// <param name="e">這一次點擊</param>
    private void ShowNextChanged(object sender, RoutedEventArgs e)
    {
        // 結果視窗裡那一個與左欄最下面那一個是同一項設定，兩個一起跟著變
        var on = ((CheckBox)sender).IsChecked == true;

        ShowNext.IsChecked = on;
        ShowAfter.IsChecked = on;

        service.LoadChanged("showResult", on, null, null);
        view.ShowResult = on;
    }

    #endregion

    #region 對照

    /// <summary>中間那條分隔的寬度，跟 Measure.xaml 的第二欄一樣</summary>
    private const double Splitter = 12;

    /// <summary>蓋在整頁上的結果視窗顯示一件事：標題、每一列、右上角的秒數；有執行內容才附「下次跑完也顯示結果」</summary>
    /// <param name="result">這一次的結果</param>
    private void Tell(InspecLogic.ResultView result)
    {
        ResultTitle.Text = result.Title;
        ResultElapsed.Text = result.Seconds is null ? string.Empty : $"{result.Seconds} 秒";
        ResultMark.Data = Geometry.Parse(result.Done ? "M20 6 9 17l-5-5" : "M12 8v4 M12 16h.01 M3 12a9 9 0 1 0 18 0a9 9 0 1 0 -18 0");
        ResultMark.Stroke = result.Done ? new SolidColorBrush(Color.FromRgb(0x10, 0x7c, 0x10)) : (Brush)FindResource("Danger");
        ResultRows.ItemsSource = result.Rows;
        ShowNext.Visibility = result.Rows.Count > 0 ? Visibility.Visible : Visibility.Hidden;
        ResultLayer.Visibility = Visibility.Visible;
        CloseButton.Focus();
    }

    #endregion
}
