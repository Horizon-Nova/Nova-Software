using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using HelixStudio.Contract;
using ProcessMonitor.Logic;
using ProcessMonitor.Service;

namespace ProcessMonitor;

/// <summary>程式監控的監控台</summary>
/// <remarks>
/// 平台一建立就把連線交進來，從那一刻起每秒量一次（不在畫面上也照量，沒有回應時從右下角跳出小視窗）；
/// 每一個動作交給 MonitorService，拿回來的監控台整份換上。
/// </remarks>
public partial class Board : UserControl, IData, IShow
{
    private MonitorService? service;

    /// <summary>每秒量一次</summary>
    private readonly DispatcherTimer ticker = new() { Interval = TimeSpan.FromSeconds(1) };

    /// <summary>上一次量還沒回來就不再量</summary>
    private bool busy;

    /// <summary>正在把監控台換上去：清單、分頁的選取變了不算使用者點的</summary>
    private bool rendering;

    private MonitorLogic.BoardView view = new();

    private Alert? alert;

    /// <summary>小視窗按過叉叉的：那一支這一次沒回應期間不再跳</summary>
    private readonly HashSet<string> dismissed = [];

    /// <summary>上一秒沒有回應的：新出現的才閃工作列</summary>
    private HashSet<string> hung = [];

    public Board()
    {
        InitializeComponent();
    }

    /// <summary>平台資料庫的連線字串：交進來就開始監控</summary>
    public string Connection
    {
        get => connection;
        set
        {
            connection = value;
            Start();
        }
    }

    private string connection = string.Empty;

    /// <summary>平台的動作：把主視窗叫回前面並開這支工具</summary>
    public Action Show { get; set; } = () => { };

    /// <summary>開始監控：讀畫面上的字、在背景開 Service、每秒量一次；HelixStudio 關掉時停掉網路量測</summary>
    private async void Start()
    {
        // 畫面上的字交給 Service
        var words = ((ResourceDictionary)Resources["Words"]).Keys.OfType<string>().ToDictionary(key => key, key => (string)((ResourceDictionary)Resources["Words"])[key]);

        // 開 Service（讀資料庫、開網路量測）在背景，不卡住平台
        service = await Task.Run(() => new MonitorService(connection, words));
        Application.Current.Exit += (_, _) => service.Dispose();

        // 每秒量一次
        ticker.Tick += async (_, _) =>
        {
            if (busy)
            {
                return;
            }

            // 量不到（例如資料庫連不上）由平台掛在上方；下一秒照樣再量
            busy = true;
            try
            {
                Render(await Task.Run(service.LoadTick));
            }
            finally
            {
                busy = false;
            }
        };
        ticker.Start();
    }

    /// <summary>把監控台換上去：清單保留捲到的位置與選取；有沒有回應的程式、主視窗不在前面就跳出小視窗、閃工作列</summary>
    /// <param name="next">新的監控台</param>
    private void Render(MonitorLogic.BoardView next)
    {
        // 正在按住結束、緊急關閉（還沒滿）時不換（按鈕換掉就斷了）
        if (Mouse.Captured is HoldButton { Progress: > 0 } || (Keyboard.FocusedElement is HoldButton { Progress: > 0 } && Keyboard.IsKeyDown(Key.Space)))
        {
            return;
        }

        rendering = true;
        view = next;

        // 換上去，捲到的位置留著
        var scroller = FindScroller(Programs);
        var offset = scroller?.VerticalOffset ?? 0;
        DataContext = next;
        Programs.SelectedIndex = next.Chosen;
        scroller?.ScrollToVerticalOffset(offset);

        // 上方的橫幅
        AdminBar.Visibility = next.Admin ? Visibility.Collapsed : Visibility.Visible;
        NetBar.Visibility = next.Admin && next.NetFault.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        FaultBar.Visibility = next.Fault.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

        // 工具列：對著選中的那一支；它沒有回應時按鈕上寫它的名字
        Actions.IsEnabled = next.Detail is not null;
        SuspendWord.Visibility = next.Detail?.Paused == true ? Visibility.Collapsed : Visibility.Visible;
        ResumeWord.Visibility = next.Detail?.Paused == true ? Visibility.Visible : Visibility.Collapsed;
        PauseName.Text = next.Detail?.Hung == true ? " " + next.Detail.Name : string.Empty;
        System.Windows.Automation.AutomationProperties.SetName(PauseButton, (next.Detail?.Paused == true ? ResumeWord.Text : SuspendWord.Text) + PauseName.Text);
        EndName.Text = PauseName.Text;

        // 右邊的分頁
        var tab = next.Detail?.Tab ?? "conn";
        Tabs.SelectedItem = Tabs.Items.OfType<ListBoxItem>().FirstOrDefault(one => $"{one.Tag}" == tab);
        ConnPage.Visibility = tab == "conn" ? Visibility.Visible : Visibility.Collapsed;
        PortPage.Visibility = tab == "port" ? Visibility.Visible : Visibility.Collapsed;
        EventPage.Visibility = tab == "event" ? Visibility.Visible : Visibility.Collapsed;
        ModulePage.Visibility = tab == "module" ? Visibility.Visible : Visibility.Collapsed;
        rendering = false;

        // 沒有回應：主視窗不在前面時，卡最久而且沒按過叉叉的那一支跳到右下角；新出現的閃工作列
        var main = Application.Current.MainWindow;
        var now = next.Hangs.Select(one => one.Key).ToHashSet();
        dismissed.IntersectWith(now);
        var top = next.Hangs.FirstOrDefault(one => !dismissed.Contains(one.Key));
        alert ??= new Alert(Show, key => Act(() => service!.LoadEnded(key)), key => Act(() => service!.LoadEased(key)), key => dismissed.Add(key));
        if (top is not null && main is not null && !main.IsActive)
        {
            alert.Show(top);
        }
        else
        {
            alert.Hide();
        }

        if (now.Except(hung).Any() && main is not null && !main.IsActive)
        {
            service!.LoadFlashed(new WindowInteropHelper(main).Handle);
        }

        hung = now;
    }

    /// <summary>做一件事：在背景交給 Service，拿回來的監控台換上</summary>
    private async Task Act(Func<MonitorLogic.BoardView> act)
    {
        if (service is not null)
        {
            Render(await Task.Run(act));
        }
    }

    /// <summary>清單裡的捲軸</summary>
    private static ScrollViewer? FindScroller(DependencyObject root) =>
        root is ScrollViewer found ? found
        : Enumerable.Range(0, VisualTreeHelper.GetChildrenCount(root)).Select(i => FindScroller(VisualTreeHelper.GetChild(root, i))).FirstOrDefault(one => one is not null);

    /// <summary>搜尋框打字</summary>
    private async void Searched(object sender, TextChangedEventArgs e)
    {
        var text = Search.Text;
        await Act(() => service!.LoadSearched(text));
    }

    /// <summary>點欄名排序</summary>
    private async void SortBy(object sender, RoutedEventArgs e)
    {
        var key = $"{((Button)sender).CommandParameter}";
        await Act(() => service!.LoadSorted(key));
    }

    /// <summary>點清單的一列</summary>
    private async void Picked(object sender, SelectionChangedEventArgs e)
    {
        if (!rendering && Programs.SelectedItem is MonitorLogic.ProgramRow row)
        {
            await Act(() => service!.LoadPicked(row.Key));
        }
    }

    /// <summary>右鍵一列：選它，打開選單（優先順序、暫停或繼續、限制、結束）</summary>
    private async void RightPicked(object sender, MouseButtonEventArgs e)
    {
        // 選右鍵按到的那一列
        if (ItemsControl.ContainerFromElement(Programs, (DependencyObject)e.OriginalSource) is not ListBoxItem { DataContext: MonitorLogic.ProgramRow row })
        {
            return;
        }

        e.Handled = true;
        await Act(() => service!.LoadPicked(row.Key));

        // 選單照選中那一支：現在的優先順序打勾、暫停中的寫繼續
        var menu = (ContextMenu)Resources["RowMenu"];
        var items = menu.Items.OfType<MenuItem>().ToList();
        items[0].InputGestureText = $"{view.Detail?.PriorityName} ›";
        items[0].Items.OfType<MenuItem>().ToList().ForEach(one => one.IsChecked = $"{one.Tag}" == $"{view.Detail?.Priority}");
        items[1].Visibility = view.Detail?.Paused == true ? Visibility.Collapsed : Visibility.Visible;
        items[2].Visibility = view.Detail?.Paused == true ? Visibility.Visible : Visibility.Collapsed;
        menu.PlacementTarget = Programs;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
        menu.IsOpen = true;
    }

    /// <summary>打開欄位清單</summary>
    private void OpenColumns(object sender, RoutedEventArgs e) =>
        ColumnsPopup.IsOpen = true;

    /// <summary>勾一個量測項目</summary>
    private async void FlipColumn(object sender, RoutedEventArgs e)
    {
        var key = $"{((CheckBox)sender).Tag}";
        var on = ((CheckBox)sender).IsChecked == true;
        await Act(() => service!.LoadColumn(key, on));
    }

    /// <summary>打開優先順序的下拉：現在的打勾</summary>
    private void OpenPriority(object sender, RoutedEventArgs e)
    {
        var menu = (ContextMenu)Resources["PriorityMenu"];
        menu.Items.OfType<MenuItem>().ToList().ForEach(one => one.IsChecked = $"{one.Tag}" == $"{view.Detail?.Priority}");
        menu.PlacementTarget = PriorityButton;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    /// <summary>選一個優先順序</summary>
    private async void PickPriority(object sender, RoutedEventArgs e)
    {
        var priority = int.Parse($"{((MenuItem)sender).Tag}");
        await Act(() => service!.LoadPriority(priority));
    }

    /// <summary>暫停或繼續選中那一支</summary>
    private async void Pause(object sender, RoutedEventArgs e) =>
        await Act(() => service!.LoadPaused());

    /// <summary>打開限制對話框；套用交給 Service</summary>
    private async void OpenLimit(object sender, RoutedEventArgs e)
    {
        var limit = service is null ? null : await Task.Run(service.LoadLimit);
        if (limit is null || Window.GetWindow(this) is not { } owner)
        {
            return;
        }

        new Limit(owner, limit, (up, block, cpu, mem) => Task.Run(() => service!.LoadLimited(limit.Key, up, block, cpu, mem))).ShowDialog();
        var text = Search.Text;
        await Act(() => service!.LoadSearched(text));
    }

    /// <summary>按住 1 秒結束選中那一支（工具列、右鍵選單）</summary>
    private async void End(object sender, RoutedEventArgs e)
    {
        ((ContextMenu)Resources["RowMenu"]).IsOpen = false;
        var key = view.Picked;
        await Act(() => service!.LoadEnded(key));
    }

    /// <summary>橫幅上按住 1 秒緊急關閉沒有回應的那一支</summary>
    private async void EndHung(object sender, RoutedEventArgs e)
    {
        var key = $"{((FrameworkElement)sender).Tag}";
        await Act(() => service!.LoadEnded(key));
    }

    /// <summary>橫幅上的緩和或取消緩和</summary>
    private async void Ease(object sender, RoutedEventArgs e)
    {
        var key = $"{((FrameworkElement)sender).Tag}";
        await Act(() => service!.LoadEased(key));
    }

    /// <summary>點右邊的分頁</summary>
    private async void PickTab(object sender, SelectionChangedEventArgs e)
    {
        if (!rendering && Tabs.SelectedItem is ListBoxItem item)
        {
            var key = $"{item.Tag}";
            await Act(() => service!.LoadTab(key));
        }
    }

    /// <summary>走勢那一塊的寬變了</summary>
    private async void ChartSized(object sender, SizeChangedEventArgs e)
    {
        var width = Chart.ActualWidth;
        await Act(() => service!.LoadSized(width));
    }

    /// <summary>以系統管理員重新開啟：新的那一份等這一份關掉才接手，所以這一份關掉；操作員在 UAC 按否就留著</summary>
    private void Elevate(object sender, RoutedEventArgs e)
    {
        var fault = service?.LoadElevated() ?? string.Empty;
        if (fault.Length == 0)
        {
            Application.Current.Shutdown();
            return;
        }

        view.Fault = fault;
        FaultBar.Visibility = Visibility.Visible;
        DataContext = null;
        DataContext = view;
    }
}
