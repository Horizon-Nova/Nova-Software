using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using HelixStudio.Contract;
using VNCSCP.Logic;
using VNCSCP.Service;

namespace VNCSCP;

/// <summary>VNCSCP 的畫面：左邊機台與最近送出，右邊來源；勾機台、按送出，透過各台 UltraVNC 的檔案傳輸送過去</summary>
/// <remarks>只呼叫 VncscpService：讀寫資料庫、讀來源的檔、連機台都在它後面；這裡只管畫面上的勾、搜尋、排序與通知。</remarks>
public partial class Board : UserControl, ITool, IData
{
    /// <summary>平台載入後塞進來的保險箱：每台的 VNC 密碼鎖在這裡（machine:{id}）</summary>
    public ISecrets Secrets { get; set; } = null!;

    /// <summary>平台載入後塞進來的資料庫連線</summary>
    public string Connection { get; set; } = string.Empty;

    /// <summary>這一頁的串接；平台塞好連線與保險箱、畫面掛上來時才建</summary>
    private VncscpService? service;

    /// <summary>每一台機台（vw_vncscp 的 machine 列，加上畫面上的勾與正在跑的那一件）</summary>
    private readonly List<MachineItem> machines = [];

    /// <summary>畫面上看得到的機台：照排序、照搜尋</summary>
    private readonly ObservableCollection<MachineItem> rows = [];

    /// <summary>右下角的通知</summary>
    private readonly ObservableCollection<NoteItem> notes = [];

    /// <summary>來源：資料夾（挑檔案時是空的）與每一個檔，新的在前</summary>
    private VncscpLogic.SourceView source = new();

    private string sortKey = "diff";

    private bool sortDesc = true;

    /// <summary>正在做的事：空的、send（送出）、test（測試連線）</summary>
    private string busy = string.Empty;

    private CancellationTokenSource? stop;

    private bool adding;

    /// <summary>新增機台哪一格錯了：ip / port；空的是沒錯</summary>
    private string fault = string.Empty;

    /// <summary>按 Enter 或 Esc 離開遠端路徑時，那一次離開不再存</summary>
    private bool skipLeave;

    private bool started;

    /// <summary>畫面上的時間跟著走：跑的時候每 0.2 秒、平常每秒</summary>
    private readonly DispatcherTimer clock = new() { Interval = TimeSpan.FromSeconds(1) };

    /// <summary>來源資料夾有變動，停 0.5 秒沒有再變才重讀</summary>
    private readonly DispatcherTimer settle = new() { Interval = TimeSpan.FromMilliseconds(500) };

    private FileSystemWatcher? watcher;

    /// <summary>來源清單上次畫的是哪一份；換了才重畫，捲動位置不跳</summary>
    private VncscpLogic.SourceView? drawn;

    /// <summary>備好這一頁</summary>
    public Board()
    {
        InitializeComponent();

        Machines.ItemsSource = rows;
        Notes.ItemsSource = notes;

        clock.Tick += (_, _) => Render();
        settle.Tick += (_, _) =>
        {
            settle.Stop();
            if (busy == "send" || source.Folder.Length == 0 || service is null) return;
            source = service.LoadFolder(source.Folder);
            Render();
        };
    }

    #region 畫面

    /// <summary>畫面掛上來：讀機台與最近送出、讀上次的來源資料夾、開始看資料夾的變動</summary>
    /// <param name="sender">這一頁</param>
    /// <param name="e">掛上來這一次</param>
    private void Ready(object sender, RoutedEventArgs e)
    {
        // 讀 vw_vncscp：設定、機台、最近送出
        service ??= new VncscpService(Connection, Secrets);
        var saved = Reload();

        // 第一次掛上來時用上次的來源資料夾；之後回到這一頁時沿用畫面上的來源，重讀一次
        if (!started && saved.Length > 0 && Directory.Exists(saved)) source = service.LoadFolder(saved);
        else if (source.Folder.Length > 0) source = service.LoadFolder(source.Folder);
        started = true;

        // 資料夾有變動就重讀：改完程式重新建置，差就跟著變
        watcher?.Dispose();
        watcher = null;
        if (source.Folder.Length > 0 && Directory.Exists(source.Folder))
        {
            watcher = new FileSystemWatcher(source.Folder) { IncludeSubdirectories = true, NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size };
            FileSystemEventHandler moved = (_, _) => Dispatcher.BeginInvoke(() => { settle.Stop(); settle.Start(); });
            watcher.Changed += moved;
            watcher.Created += moved;
            watcher.Deleted += moved;
            watcher.Renamed += (_, _) => Dispatcher.BeginInvoke(() => { settle.Stop(); settle.Start(); });
            watcher.EnableRaisingEvents = true;
        }

        clock.Start();
        Render();
    }

    /// <summary>畫面拿下來：停掉時鐘與資料夾監看；送到一半的照樣送完、照樣記下來</summary>
    /// <param name="sender">這一頁</param>
    /// <param name="e">拿下來這一次</param>
    private void Gone(object sender, RoutedEventArgs e)
    {
        if (busy.Length == 0) clock.Stop();
        settle.Stop();
        watcher?.Dispose();
        watcher = null;
    }

    /// <summary>左欄的高度變了：機台區最多長到最近送出還留 140</summary>
    /// <param name="sender">左欄</param>
    /// <param name="e">高度變了</param>
    private void Fit(object sender, SizeChangedEventArgs e) =>
        MachineScroll.MaxHeight = Math.Max(0, Left.ActualHeight - 40 - (Head.Visibility == Visibility.Visible ? 32 : 0) - 40 - 28 - 140);

    #endregion

    #region 選機台

    /// <summary>點一列：勾或取消這一台（跑的時候不能改）</summary>
    /// <param name="sender">那一列（Tag 是這一台）</param>
    /// <param name="e">放開滑鼠</param>
    private void Flip(object sender, MouseButtonEventArgs e)
    {
        if (busy.Length > 0) return;
        if (e.OriginalSource is DependencyObject hit && Up<TextBox>(hit) is not null) return;
        if (((FrameworkElement)sender).Tag is not MachineItem one) return;

        one.Checked = !one.Checked;
        Render();
    }

    /// <summary>勾或取消一列前面的核取方塊（滑鼠、空白鍵、協助工具都走這裡）</summary>
    /// <param name="sender">核取方塊（Tag 是這一台）</param>
    /// <param name="e">勾了或取消了</param>
    private void Tick(object sender, RoutedEventArgs e)
    {
        var box = (CheckBox)sender;
        if (box.Tag is not MachineItem one || one.Checked == (box.IsChecked == true)) return;

        one.Checked = box.IsChecked == true;
        Render();
    }

    /// <summary>全選：看得到的那幾台全勾；已經全勾了就全取消</summary>
    /// <param name="sender">全選</param>
    /// <param name="e">點一下</param>
    private void CheckAll(object sender, RoutedEventArgs e)
    {
        if (busy.Length == 0)
        {
            var on = rows.Count > 0 && rows.All(one => one.Checked);
            foreach (var one in rows) one.Checked = !on;
        }

        Render();
    }

    /// <summary>全選符合搜尋的那幾台</summary>
    /// <param name="sender">全選符合的</param>
    /// <param name="e">點一下</param>
    private void CheckMatched(object sender, RoutedEventArgs e)
    {
        if (busy.Length > 0) return;
        foreach (var one in rows) one.Checked = true;
        Render();
    }

    /// <summary>再選上次送出的那幾台：清掉搜尋，只勾那幾台</summary>
    /// <param name="sender">再選上次送出的</param>
    /// <param name="e">點一下</param>
    private void CheckLast(object sender, RoutedEventArgs e)
    {
        if (busy.Length > 0) return;
        foreach (var one in machines) one.Checked = one.Row.InLast;
        Query.Text = string.Empty;
        Render();
    }

    /// <summary>搜尋：名稱、IP:Port、遠端路徑任一段包含就符合</summary>
    /// <param name="sender">搜尋框</param>
    /// <param name="e">字變了</param>
    private void Search(object sender, TextChangedEventArgs e) =>
        Render();

    /// <summary>搜尋框按 Esc 清掉</summary>
    /// <param name="sender">搜尋框</param>
    /// <param name="e">按鍵</param>
    private void QueryKey(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        e.Handled = true;
        Query.Text = string.Empty;
    }

    /// <summary>清除搜尋</summary>
    /// <param name="sender">清除鈕</param>
    /// <param name="e">點一下</param>
    private void Unsearch(object sender, RoutedEventArgs e)
    {
        Query.Text = string.Empty;
        Query.Focus();
    }

    /// <summary>點欄名：照那一欄排；再點一下反過來</summary>
    /// <param name="sender">欄名（CommandParameter 是哪一欄）</param>
    /// <param name="e">點一下</param>
    private void Sort(object sender, RoutedEventArgs e)
    {
        var key = $"{((Button)sender).CommandParameter}";
        if (key == sortKey) sortDesc = !sortDesc;
        else
        {
            sortKey = key;
            sortDesc = key is "diff" or "ping";
        }

        Render();
    }

    #endregion

    #region 遠端路徑

    /// <summary>遠端路徑：Enter 存、Esc 不存</summary>
    /// <param name="sender">遠端路徑框（Tag 是這一台）</param>
    /// <param name="e">按鍵</param>
    private void PathKey(object sender, KeyEventArgs e)
    {
        var box = (TextBox)sender;
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            SavePath(box);
            skipLeave = true;
            Keyboard.ClearFocus();
        }

        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            box.Text = ((MachineItem)box.Tag).Path;
            skipLeave = true;
            Keyboard.ClearFocus();
        }
    }

    /// <summary>離開遠端路徑框就存</summary>
    /// <param name="sender">遠端路徑框（Tag 是這一台）</param>
    /// <param name="e">離開這一格</param>
    private void PathLeave(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (skipLeave)
        {
            skipLeave = false;
            return;
        }

        SavePath((TextBox)sender);
    }

    /// <summary>存一台的遠端路徑：換了位置，機台那份與上次送達就是那個位置的（沒送過是 —）</summary>
    /// <param name="box">遠端路徑框（Tag 是這一台）</param>
    private void SavePath(TextBox box)
    {
        // 只是點進去又離開：字沒動就不找資料庫
        var one = (MachineItem)box.Tag;
        if (box.Text.Trim() == one.Path)
        {
            box.Text = one.Path;
            return;
        }

        // 寫進那一台
        var text = service!.LoadPathChanged(one.Id, box.Text);

        // 重讀，說一聲
        Reload();
        box.Text = one.Path;
        if (text.Length > 0) Say(new NoteItem { Text = text });
        Render();
    }

    #endregion

    #region 新增機台

    /// <summary>打開新增機台那一列，游標放在名稱</summary>
    /// <param name="sender">新增機台</param>
    /// <param name="e">點一下</param>
    private void OpenAdd(object sender, RoutedEventArgs e)
    {
        adding = true;
        Render();
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () => NewName.Focus());
    }

    /// <summary>收起新增機台，清掉打到一半的</summary>
    /// <param name="sender">叉叉</param>
    /// <param name="e">點一下</param>
    private void CloseAdd(object sender, RoutedEventArgs e)
    {
        adding = false;
        fault = string.Empty;
        NewName.Text = NewIp.Text = NewPort.Text = NewPwShown.Text = string.Empty;
        NewPw.Password = string.Empty;
        if (NewPwShown.Visibility == Visibility.Visible) FlipPw(ShowPw, e);
        Render();
    }

    /// <summary>新增機台那一列：Enter 加入、Esc 收起</summary>
    /// <param name="sender">新增機台那一列</param>
    /// <param name="e">按鍵</param>
    private void AddKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            AddMachine(sender, e);
        }

        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            CloseAdd(sender, e);
        }
    }

    /// <summary>加入一台：IP 與 Port 都要填、Port 是 1 到 65535、同一個 IP:Port 不能加兩次；密碼鎖進保險箱</summary>
    /// <param name="sender">加入</param>
    /// <param name="e">點一下或 Enter</param>
    private void AddMachine(object sender, RoutedEventArgs e)
    {
        // 照填的值寫進去
        var saved = service!.LoadAdded(new VncscpLogic.MachineInput
        {
            name = NewName.Text,
            ip = NewIp.Text,
            port = NewPort.Text,
            password = NewPwShown.Visibility == Visibility.Visible ? NewPwShown.Text : NewPw.Password,
        });

        // 填錯了：錯的那一格紅框、游標放回去、下面寫怎麼改
        fault = saved.Field;
        if (fault.Length > 0)
        {
            AddFaultText.Text = saved.Fault;
            Render();
            (fault == "port" ? NewPort : NewIp).Focus();
            return;
        }

        // 清空這一列，游標回到名稱，好接著加下一台
        NewName.Text = NewIp.Text = NewPort.Text = NewPwShown.Text = string.Empty;
        NewPw.Password = string.Empty;
        Reload();
        Say(new NoteItem { Text = saved.Text });
        Render();
        NewName.Focus();
    }

    /// <summary>IP 或 Port 改了：那一格的錯先拿掉；Port 空著時現出 5900 的提示</summary>
    /// <param name="sender">IP 或 Port</param>
    /// <param name="e">字變了</param>
    private void Unfault(object sender, TextChangedEventArgs e)
    {
        PortHint.Visibility = NewPort.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        if ((fault == "ip" && sender == NewIp) || (fault == "port" && sender == NewPort))
        {
            fault = string.Empty;
            Render();
        }
    }

    /// <summary>顯示或藏起密碼</summary>
    /// <param name="sender">眼睛</param>
    /// <param name="e">點一下</param>
    private void FlipPw(object sender, RoutedEventArgs e)
    {
        var show = NewPwShown.Visibility != Visibility.Visible;
        if (show) NewPwShown.Text = NewPw.Password;
        else NewPw.Password = NewPwShown.Text;

        NewPwShown.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        NewPw.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
        PwIcon.Content = FindResource(show ? "IconEyeOff" : "IconEye");
        ShowPw.ToolTip = show ? "隱藏密碼" : "顯示密碼";
    }

    #endregion

    #region 來源

    /// <summary>選資料夾：整個資料夾（含子資料夾）都送；記下來，下次開起來還是它</summary>
    /// <param name="sender">選擇資料夾</param>
    /// <param name="e">點一下</param>
    private void PickFolder(object sender, RoutedEventArgs e)
    {
        if (busy == "send") return;

        // 挑資料夾
        using var pick = new System.Windows.Forms.FolderBrowserDialog { Description = "選擇來源資料夾", UseDescriptionForTitle = true, SelectedPath = Directory.Exists(source.Folder) ? source.Folder : string.Empty };
        if (pick.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

        // 讀裡面的檔，記下這個資料夾
        source = service!.LoadFolderPicked(pick.SelectedPath);

        // 重新看這個資料夾的變動
        Ready(this, e);
        Say(new NoteItem { Text = source.Text });
        Render();
    }

    /// <summary>選檔案：只送挑的那幾個</summary>
    /// <param name="sender">選擇檔案</param>
    /// <param name="e">點一下</param>
    private void PickFiles(object sender, RoutedEventArgs e)
    {
        if (busy == "send") return;

        var pick = new OpenFileDialog { Title = "選擇要送的檔案", Multiselect = true };
        if (pick.ShowDialog() != true || pick.FileNames.Length == 0) return;

        watcher?.Dispose();
        watcher = null;
        source = service!.LoadFilesPicked(pick.FileNames);
        Say(new NoteItem { Text = source.Text });
        Render();
    }

    #endregion

    #region 送出與測試

    /// <summary>送到勾選的機台：每台送到自己的遠端路徑、一台一條連線同時送；沒填路徑的那台直接記下原因</summary>
    /// <param name="sender">送到勾選的</param>
    /// <param name="e">點一下</param>
    private async void Send(object sender, RoutedEventArgs e)
    {
        var picked = machines.Where(one => one.Checked).Select(one => one.Id).ToList();
        if (busy.Length > 0 || source.Files.Count == 0 || picked.Count == 0) return;

        busy = "send";
        stop = new CancellationTokenSource();

        // 送之前重讀來源、同時送：開始時換上重讀的來源、每台掛上正在跑的那一件，每台結束就重讀、重畫那一台
        var done = await service!.LoadSent(picked, source, (sending, jobs) =>
        {
            source = sending;
            foreach (var job in jobs)
                foreach (var one in machines.Where(m => m.Id == job.MachineId)) one.Job = job;
            Reload();
            clock.Interval = TimeSpan.FromMilliseconds(200);
            Render();
        }, job =>
        {
            foreach (var one in machines.Where(m => m.Id == job.MachineId)) one.Job = null;
            Reload();
            Render();
        }, stop.Token);

        // 一批結束：換上重讀的來源，說結果
        busy = string.Empty;
        stop.Dispose();
        stop = null;
        clock.Interval = TimeSpan.FromSeconds(1);
        source = done.Source;
        foreach (var note in done.Notices) Say(new NoteItem { Text = note.Text, Error = note.Error });
        Render();
    }

    /// <summary>測試連線：勾選的每台連一次、登入一次，記下耗時</summary>
    /// <param name="sender">測試連線</param>
    /// <param name="e">點一下</param>
    private async void Test(object sender, RoutedEventArgs e)
    {
        var picked = machines.Where(one => one.Checked).Select(one => one.Id).ToList();
        if (busy.Length > 0 || picked.Count == 0) return;

        busy = "test";
        stop = new CancellationTokenSource();

        // 同時連：開始時每台掛上正在跑的那一件，每台結束就重讀、重畫那一台
        var done = await service!.LoadTested(picked, jobs =>
        {
            foreach (var job in jobs)
                foreach (var one in machines.Where(m => m.Id == job.MachineId)) one.Job = job;
            clock.Interval = TimeSpan.FromMilliseconds(200);
            Render();
        }, job =>
        {
            foreach (var one in machines.Where(m => m.Id == job.MachineId)) one.Job = null;
            Reload();
            Render();
        }, stop.Token);

        // 一批結束：說結果
        busy = string.Empty;
        stop.Dispose();
        stop = null;
        clock.Interval = TimeSpan.FromSeconds(1);
        foreach (var note in done) Say(new NoteItem { Text = note.Text, Error = note.Error });
        Render();
    }

    /// <summary>取消：停下每一台；送到一半的照實記下送了多少，測試中的記下等了多久</summary>
    /// <param name="sender">取消或取消測試</param>
    /// <param name="e">點一下</param>
    private void Cancel(object sender, RoutedEventArgs e)
    {
        if (busy.Length == 0 || stop is null) return;
        stop.Cancel();
    }

    #endregion

    #region 通知

    /// <summary>出一則通知：最多留 3 則；資訊 4 秒後自己收起，錯誤留到按關閉</summary>
    /// <param name="note">這一則</param>
    private void Say(NoteItem note)
    {
        note.Ink = (Brush)FindResource(note.Error ? "Danger" : "Accent");
        note.Icon = FindResource(note.Error ? "IconAlert" : "IconCheck");
        note.Timer.Tick += (_, _) => notes.Remove(note);
        notes.Add(note);
        while (notes.Count > 3)
        {
            notes[0].Timer.Stop();
            notes.RemoveAt(0);
        }

        if (!note.Error) note.Timer.Start();
    }

    /// <summary>關掉一則通知</summary>
    /// <param name="sender">關閉（Tag 是這一則）</param>
    /// <param name="e">點一下</param>
    private void DropNote(object sender, RoutedEventArgs e)
    {
        var note = (NoteItem)((FrameworkElement)sender).Tag;
        note.Timer.Stop();
        notes.Remove(note);
    }



    #endregion

    #region 讀與畫

    /// <summary>重讀主畫面：機台照編號併進畫面上的那一台（勾與正在跑的留著），最近送出整份換掉；回傳上次的來源資料夾</summary>
    private string Reload()
    {
        var view = service!.LoadBoard();

        // 機台：新的加進來、不在的拿掉、在的換成資料庫的值
        machines.RemoveAll(one => view.Machines.All(row => row.Id != one.Id));
        foreach (var row in view.Machines)
        {
            var one = machines.FirstOrDefault(m => m.Id == row.Id);
            if (one is null) machines.Add(one = new MachineItem { Id = row.Id });
            one.Row = row;
            one.Name = row.Name;
            one.Address = row.Address;
            one.Path = row.Path;
        }

        // 最近送出與測試：新的在前
        Log.ItemsSource = view.Log;

        return view.Source;
    }

    /// <summary>把現在的狀態攤到畫面上：工具列、每一台、新增機台、最近送出、來源、送出鈕</summary>
    private void Render()
    {
        if (service is null) return;
        var locked = busy.Length > 0;
        var screen = service.LoadScreen([.. machines.Select(one => new VncscpLogic.MachineState { Row = one.Row, Checked = one.Checked, Job = one.Job })],
            source.Files, source.Folder, Query.Text, sortKey, sortDesc, busy);

        // 搜尋與排序：看得到的那幾台
        var shown = screen.Shown.Select(id => machines.First(one => one.Id == id)).ToList();
        if (!shown.SequenceEqual(rows))
        {
            rows.Clear();
            foreach (var one in shown) rows.Add(one);
        }

        // 工具列
        var has = screen.Has;
        All.Visibility = QueryFrame.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
        NoMachine.Visibility = has ? Visibility.Collapsed : Visibility.Visible;
        All.IsChecked = screen.AllChecked;
        All.IsEnabled = !locked;
        AllCount.Text = $"{screen.ShownCount}";
        QueryHint.Visibility = Query.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        ClearQuery.Visibility = screen.Querying ? Visibility.Visible : Visibility.Collapsed;
        MatchButton.Visibility = has && screen.Querying ? Visibility.Visible : Visibility.Collapsed;
        MatchButton.IsEnabled = !locked && screen.ShownCount > 0;
        MatchCount.Text = $"{screen.ShownCount}";
        LastButton.Visibility = has && screen.LastCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        LastButton.IsEnabled = !locked;
        LastButton.ToolTip = screen.LastTitle;
        LastCount.Text = $"{screen.LastCount}";
        StopTest.Visibility = busy == "test" ? Visibility.Visible : Visibility.Collapsed;
        TestButton.IsEnabled = !locked && screen.Picked > 0;
        TestCount.Text = $"{screen.Picked}";

        // 欄名：排序的那一欄有箭頭
        Head.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
        foreach (var head in ((Grid)Head.Child).Children.OfType<Button>())
        {
            var on = $"{head.CommandParameter}" == sortKey;
            var label = $"{head.Content}".TrimEnd(' ', '↑', '↓');
            head.Content = on ? $"{label} {(sortDesc ? "↓" : "↑")}" : label;
            head.Tag = on ? "on" : null;
        }

        // 每一台
        var pointer = locked ? Cursors.No : Cursors.Hand;
        foreach (var one in machines)
        {
            var each = screen.Machines[one.Id];
            one.Pointer = pointer;
            one.Free = each.Free;
            one.Editable = each.Editable;
            one.Sending = each.Sending;
            one.Idle = each.Idle;
            one.PathTitle = each.PathTitle;
            one.Diff = each.Diff;
            one.DiffTitle = each.DiffTitle;
            one.Reply = each.Reply;
            one.ReplyTitle = each.ReplyTitle;
            one.Failed = each.Failed;
            one.Cancelled = each.Cancelled;
            one.Bar = 88 * each.Done;
            one.Progress = each.Progress;
        }

        // 新增機台、第一次使用
        AddOpen.Visibility = !adding && has ? Visibility.Visible : Visibility.Collapsed;
        AddOpen.IsEnabled = !locked;
        AddPanel.Visibility = adding ? Visibility.Visible : Visibility.Collapsed;
        AddFault.Visibility = fault.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        IpFrame.Tag = fault == "ip" ? "fault" : null;
        PortFrame.Tag = fault == "port" ? "fault" : null;
        First.Visibility = !adding && !has ? Visibility.Visible : Visibility.Collapsed;
        LogHead.BorderThickness = has || adding ? new Thickness(0, 0, 0, 1) : new Thickness(0, 1, 0, 1);
        NoLog.Visibility = Log.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        Fit(Left, null!);

        // 來源
        FilesButton.IsEnabled = FolderButton.IsEnabled = busy != "send";
        Where.Text = screen.Where;
        FileSummary.Text = screen.FileSummary;
        LocalTime.Text = screen.LocalTime;
        if (drawn != source)
        {
            drawn = source;
            FileList.ItemsSource = source.Shown;
        }
        NoFiles.Visibility = source.Files.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        // 送出鈕；送的時候換成整體進度與取消
        IdlePanel.Visibility = busy == "send" ? Visibility.Collapsed : Visibility.Visible;
        BusyPanel.Visibility = busy == "send" ? Visibility.Visible : Visibility.Collapsed;
        Overall.Text = screen.Overall;
        SendNote.Text = screen.SendNote;
        SendButton.IsEnabled = screen.CanSend;
        SendCount.Text = $"{screen.Picked}";
    }

    /// <summary>往上找第一個某種型別的元素</summary>
    /// <param name="from">從這裡開始</param>
    private static T? Up<T>(DependencyObject? from) where T : DependencyObject
    {
        while (from is not null and not T) from = from is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(from) : LogicalTreeHelper.GetParent(from);
        return from as T;
    }

    #endregion

    #region 模型

    /// <summary>會通知畫面的一包值：設了不一樣的值才通知</summary>
    public abstract class Bag : INotifyPropertyChanged
    {
        private readonly Dictionary<string, object?> values = [];

        /// <summary>某個值變了</summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>取一個值</summary>
        /// <param name="name">哪一個</param>
        protected T Get<T>([CallerMemberName] string name = "") =>
            values.TryGetValue(name, out var value) && value is T typed ? typed : default!;

        /// <summary>設一個值；不一樣才通知</summary>
        /// <param name="value">新的值</param>
        /// <param name="name">哪一個</param>
        protected void Set(object? value, [CallerMemberName] string name = "")
        {
            if (values.TryGetValue(name, out var before) && Equals(before, value)) return;
            values[name] = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    /// <summary>一台機台：vw_vncscp 的 machine 列，加上畫面上的勾、正在跑的那一件與攤到畫面上的字</summary>
    public class MachineItem : Bag
    {
        /// <summary>machine 那一列的編號</summary>
        public int Id { get; set; }

        /// <summary>vw_vncscp 的 machine 列</summary>
        public VncscpLogic.MachineRow Row { get; set; } = new();

        /// <summary>正在跑的那一件；沒有是 null</summary>
        public VncscpLogic.Job? Job { get; set; }

        /// <summary>名稱；空的寫 —</summary>
        public string Name { get => Get<string>(); set => Set(value); }

        /// <summary>IP:Port</summary>
        public string Address { get => Get<string>(); set => Set(value); }

        /// <summary>現在的遠端路徑</summary>
        public string Path { get => Get<string>() ?? string.Empty; set => Set(value); }

        /// <summary>遠端路徑的提示：路徑本身，空的寫還沒填</summary>
        public string PathTitle { get => Get<string>(); set => Set(value); }

        /// <summary>勾了沒</summary>
        public bool Checked { get => Get<bool>(); set => Set(value); }

        /// <summary>這一列的游標：跑的時候不能點</summary>
        public Cursor Pointer { get => Get<Cursor>(); set => Set(value); }

        /// <summary>能不能勾：跑的時候不能</summary>
        public bool Free { get => Get<bool>(); set => Set(value); }

        /// <summary>遠端路徑能不能改：跑的時候不能</summary>
        public bool Editable { get => Get<bool>(); set => Set(value); }

        /// <summary>正在送</summary>
        public bool Sending { get => Get<bool>(); set => Set(value); }

        /// <summary>沒在送：顯示差與上次回應</summary>
        public bool Idle { get => Get<bool>(); set => Set(value); }

        /// <summary>進度條的長度（滿是 88）</summary>
        public double Bar { get => Get<double>(); set => Set(value); }

        /// <summary>進度的字</summary>
        public string Progress { get => Get<string>(); set => Set(value); }

        /// <summary>差：本機新／機台新多久、相同、—</summary>
        public string Diff { get => Get<string>(); set => Set(value); }

        /// <summary>差的提示：機台那份與上次送達</summary>
        public string DiffTitle { get => Get<string>(); set => Set(value); }

        /// <summary>上次回應的字</summary>
        public string Reply { get => Get<string>(); set => Set(value); }

        /// <summary>上次回應的提示</summary>
        public string ReplyTitle { get => Get<string>(); set => Set(value); }

        /// <summary>上次回應寫的是沒成功的原因：靠左，失敗紅、取消灰（顏色在 XAML 照值決定）</summary>
        public bool Failed { get => Get<bool>(); set => Set(value); }

        /// <summary>那一次是使用者按了取消</summary>
        public bool Cancelled { get => Get<bool>(); set => Set(value); }
    }

    /// <summary>一則通知</summary>
    public class NoteItem
    {
        /// <summary>內容</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>錯誤（留到按關閉）還是資訊（4 秒收起）</summary>
        public bool Error { get; set; }

        /// <summary>左邊色條與圖示的顏色</summary>
        public Brush Ink { get; set; } = Brushes.Transparent;

        /// <summary>圖示：勾或驚嘆號</summary>
        public object? Icon { get; set; }

        /// <summary>資訊的 4 秒</summary>
        public DispatcherTimer Timer { get; } = new() { Interval = TimeSpan.FromSeconds(4) };
    }

    #endregion
}
