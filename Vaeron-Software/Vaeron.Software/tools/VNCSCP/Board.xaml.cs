using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using HelixStudio.Contract;
using Models.HelixStudio;

namespace VNCSCP;

/// <summary>VNCSCP 的畫面：左邊機台與最近送出，右邊來源；勾機台、按送出，透過各台 UltraVNC 的檔案傳輸送過去</summary>
public partial class Board : UserControl, ITool, IData
{
    /// <summary>平台載入後塞進來的保險箱：每台的 VNC 密碼鎖在這裡（machine:{id}）</summary>
    public ISecrets Secrets { get; set; } = null!;

    /// <summary>平台載入後塞進來的資料庫連線</summary>
    public string Connection { get; set; } = string.Empty;

    /// <summary>每一台機台（vw_vncscp 的 machine 列，加上畫面上的勾與正在跑的那一件）</summary>
    private readonly List<MachineItem> machines = [];

    /// <summary>畫面上看得到的機台：照排序、照搜尋</summary>
    private readonly ObservableCollection<MachineItem> rows = [];

    /// <summary>右下角的通知</summary>
    private readonly ObservableCollection<NoteItem> notes = [];

    /// <summary>來源的每一個檔：新的在前</summary>
    private List<FileItem> files = [];

    /// <summary>來源資料夾；挑檔案時是空的</summary>
    private string folder = string.Empty;

    private string sortKey = "diff";

    private bool sortDesc = true;

    /// <summary>正在做的事：空的、send（送出）、test（測試連線）</summary>
    private string busy = string.Empty;

    /// <summary>這一批的結果：成功的、失敗的、按了取消時還在跑的台數</summary>
    private Batch? batch;

    private CancellationTokenSource? stop;

    private bool adding;

    /// <summary>新增機台哪一格錯了：name / ip / port；空的是沒錯</summary>
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
    private List<FileItem>? drawn;

    private static readonly StringComparer Words = StringComparer.Create(CultureInfo.GetCultureInfo("zh-TW"), false);

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
            if (busy == "send" || folder.Length == 0) return;
            files = Scan(folder);
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
        var source = Reload();

        // 第一次掛上來時用上次的來源資料夾；之後回到這一頁時沿用畫面上的來源，重讀一次
        if (!started && source.Length > 0 && Directory.Exists(source)) folder = source;
        started = true;
        if (folder.Length > 0) files = Scan(folder);

        // 資料夾有變動就重讀：改完程式重新建置，差就跟著變
        watcher?.Dispose();
        watcher = null;
        if (folder.Length > 0 && Directory.Exists(folder))
        {
            watcher = new FileSystemWatcher(folder) { IncludeSubdirectories = true, NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size };
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
        foreach (var one in machines) one.Checked = one.InLast;
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
    /// <param name="sender">欄名（Tag 是哪一欄）</param>
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
        // 沒變就不存
        var one = (MachineItem)box.Tag;
        var value = box.Text.Trim();
        if (value == one.Path)
        {
            box.Text = one.Path;
            return;
        }

        // 寫進那一台
        using (var db = Data())
        {
            db.vncscps.Where(row => row.type == "machine" && row.id == one.Id)
                .ExecuteUpdate(set => set.SetProperty(row => row.remote_path, value).SetProperty(row => row.updated_at, DateTime.UtcNow));
        }

        // 重讀，說一聲
        Reload();
        box.Text = one.Path;
        Say(new NoteItem
        {
            Text = value.Length == 0 ? $"{one.Title} 的遠端路徑清空了"
                : one.RecvAt is null ? $"{one.Title} 的遠端路徑改成 {value}，這個位置還沒送過"
                : $"{one.Title} 的遠端路徑改成 {value}"
        });
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
        // 先看填得對不對；錯的那一格紅框、游標放回去、下面寫怎麼改
        var name = NewName.Text.Trim();
        var ip = NewIp.Text.Trim();
        var portText = NewPort.Text.Trim();
        var password = NewPwShown.Visibility == Visibility.Visible ? NewPwShown.Text : NewPw.Password;

        void Wrong(string field, TextBox box, string text)
        {
            fault = field;
            AddFaultText.Text = text;
            Render();
            box.Focus();
        }

        if (ip.Length == 0) { Wrong("ip", NewIp, "先填 IP，例如 192.168.10.23"); return; }
        if (portText.Length == 0) { Wrong("port", NewPort, "先填 Port，機台上 UltraVNC 用的那個，例如 5900"); return; }
        if (!int.TryParse(portText, NumberStyles.None, CultureInfo.InvariantCulture, out var port) || port < 1 || port > 65535)
        {
            Wrong("port", NewPort, $"Port「{portText}」不能用：要是 1 到 65535 的數字");
            return;
        }

        var same = machines.FirstOrDefault(one => one.Ip == ip && one.Port == port);
        if (same is not null) { Wrong("ip", NewIp, $"{ip}:{port} 已經在清單裡（{same.Name}）"); return; }

        // 寫進資料庫，密碼鎖進保險箱
        var row = new vncscp { type = "machine", name = name, ip = ip, port = port, remote_path = string.Empty };
        using (var db = Data())
        {
            db.vncscps.Add(row);
            db.SaveChanges();
        }

        Secrets.Keep($"machine:{row.id}", password);

        // 清空這一列，游標回到名稱，好接著加下一台
        fault = string.Empty;
        NewName.Text = NewIp.Text = NewPort.Text = NewPwShown.Text = string.Empty;
        NewPw.Password = string.Empty;
        Reload();
        Say(new NoteItem { Text = $"已加入 {(name.Length > 0 ? name : ip)}（{ip}:{port}）" });
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
        using var pick = new System.Windows.Forms.FolderBrowserDialog { Description = "選擇來源資料夾", UseDescriptionForTitle = true, SelectedPath = Directory.Exists(folder) ? folder : string.Empty };
        if (pick.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

        // 讀裡面的檔，記下這個資料夾
        folder = pick.SelectedPath.TrimEnd('\\') + "\\";
        files = Scan(folder);
        using (var db = Data())
        {
            db.vncscps.Where(row => row.type == "setting" && row.setting_key == "source")
                .ExecuteUpdate(set => set.SetProperty(row => row.value, folder).SetProperty(row => row.updated_at, DateTime.UtcNow));
        }

        // 重新看這個資料夾的變動
        Ready(this, e);
        Say(new NoteItem { Text = $"來源換成 {folder}，{files.Count} 個檔" });
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
        folder = string.Empty;
        files = pick.FileNames.Select(full => new FileInfo(full))
            .Select(info => new FileItem { Full = info.FullName, Relative = info.Name, Bytes = info.Length, Mtime = Micro(info.LastWriteTimeUtc) })
            .OrderByDescending(one => one.Mtime).ThenBy(one => one.Relative, Words).ToList();
        Say(new NoteItem { Text = $"來源換成挑的 {files.Count} 個檔" });
        Render();
    }

    /// <summary>讀一個資料夾（含子資料夾）裡的每一個檔：新的在前；讀不到的子資料夾跳過，資料夾本身不見了就是空的</summary>
    /// <param name="root">資料夾</param>
    private static List<FileItem> Scan(string root)
    {
        try
        {
            return Directory.EnumerateFiles(root, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = 0 })
                .Select(full => new FileInfo(full))
                .Select(info => new FileItem { Full = info.FullName, Relative = System.IO.Path.GetRelativePath(root, info.FullName), Bytes = info.Length, Mtime = Micro(info.LastWriteTimeUtc) })
                .OrderByDescending(one => one.Mtime).ThenBy(one => one.Relative, Words).ToList();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    #endregion

    #region 送出與測試

    /// <summary>送到勾選的機台：每台送到自己的遠端路徑、一台一條連線同時送；沒填路徑的那台直接記下原因</summary>
    /// <param name="sender">送到勾選的</param>
    /// <param name="e">點一下</param>
    private async void Send(object sender, RoutedEventArgs e)
    {
        // 送之前重讀一次來源，送的就是現在的檔
        var picked = machines.Where(one => one.Checked).ToList();
        if (busy.Length > 0 || files.Count == 0 || picked.Count == 0) return;
        if (folder.Length > 0) files = Scan(folder);
        if (files.Count == 0) { Render(); return; }

        var list = files.Select(one => (one.Full, one.Relative)).ToList();
        var total = files.Sum(one => one.Bytes);
        var local = files.Max(one => one.Mtime);
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        busy = "send";
        batch = new Batch { Mode = "send", Count = files.Count, Total = total };
        stop = new CancellationTokenSource();
        var token = stop.Token;

        // 每台一列：開始的時刻、送到哪裡、送什麼；沒填路徑的那台當場結束
        var jobs = new List<(MachineItem Machine, Job Job)>();
        using (var db = Data())
        {
            foreach (var one in picked)
            {
                var path = one.Path.Trim();
                var row = new vncscp
                {
                    type = "send", machine_id = one.Id, batch = id, remote_path = path, file_count = files.Count, total_bytes = total, sent_bytes = 0,
                    local_mtime = local, started_at = now,
                };
                if (path.Length == 0)
                {
                    row.finished_at = now;
                    row.note = "沒有遠端路徑：先在這一列填上要放的資料夾";
                    batch.Failed.Add($"{one.Title}沒送到：{row.note}");
                }

                db.vncscps.Add(row);
                db.SaveChanges();
                if (path.Length > 0) jobs.Add((one, new Job { Mode = "send", Row = row.id, Started = DateTime.UtcNow, Total = total, Count = files.Count, Path = path }));
            }
        }

        foreach (var (one, job) in jobs) one.Job = job;
        Reload();
        clock.Interval = TimeSpan.FromMilliseconds(200);
        Render();

        // 同時送：一台一條連線；每台結束就寫下結果、重畫那一台
        await Task.WhenAll(jobs.Select(async pair =>
        {
            var (one, job) = pair;
            string? note = null;
            DateTime? cancelled = null;
            try
            {
                using var session = await Rfb.Login(one.Ip, one.Port, Secrets.Secret($"machine:{one.Id}"), token);
                job.Connected = session.ConnectedMs;
                job.SendAt = DateTime.UtcNow;
                job.Phase = "send";
                await Rfb.Send(session, list, job.Path, sent => Interlocked.Exchange(ref job.Sent, sent), token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                cancelled = DateTime.UtcNow;
                note = job.Phase == "send" ? $"傳到 {Kb(Interlocked.Read(ref job.Sent))} / {Kb(job.Total)} KB 時取消" : $"連線 {(cancelled.Value - job.Started).TotalSeconds:0.0} 秒時取消";
            }
            catch (Exception error)
            {
                note = error.Message;
            }

            using (var db = Data())
            {
                var sent = Interlocked.Read(ref job.Sent);
                db.vncscps.Where(row => row.id == job.Row).ExecuteUpdate(set => set
                    .SetProperty(row => row.connected_ms, job.Connected)
                    .SetProperty(row => row.sent_bytes, sent)
                    .SetProperty(row => row.finished_at, DateTime.UtcNow)
                    .SetProperty(row => row.cancelled_at, cancelled)
                    .SetProperty(row => row.note, note)
                    .SetProperty(row => row.updated_at, DateTime.UtcNow));
            }

            one.Job = null;
            if (note is null) batch.Ok.Add(one.Title);
            else if (cancelled is null) batch.Failed.Add($"{one.Title}沒送到：{note}");
            Reload();
            Render();
        }));

        // 一批結束：成功的合成一則，失敗的每台一則；按了取消就只說取消了
        var done = batch;
        busy = string.Empty;
        batch = null;
        stop.Dispose();
        stop = null;
        clock.Interval = TimeSpan.FromSeconds(1);
        if (folder.Length > 0) files = Scan(folder);
        if (done.Stopped > 0) Say(new NoteItem { Text = $"已取消，{done.Stopped} 台沒送完：機台上可能只換了部分檔案" });
        else
        {
            if (done.Ok.Count > 0) Say(new NoteItem { Text = $"已送到 {done.Ok.Count} 台：{string.Join("、", done.Ok)}（{done.Count} 個檔，{Kb(done.Total)} KB）" });
            foreach (var text in done.Failed) Say(new NoteItem { Text = text, Error = true });
        }

        Render();
    }

    /// <summary>測試連線：勾選的每台連一次、登入一次，記下耗時</summary>
    /// <param name="sender">測試連線</param>
    /// <param name="e">點一下</param>
    private async void Test(object sender, RoutedEventArgs e)
    {
        var picked = machines.Where(one => one.Checked).ToList();
        if (busy.Length > 0 || picked.Count == 0) return;

        var id = Guid.NewGuid();
        busy = "test";
        batch = new Batch { Mode = "test" };
        stop = new CancellationTokenSource();
        var token = stop.Token;

        // 每台一列：開始的時刻
        var jobs = new List<(MachineItem Machine, Job Job)>();
        using (var db = Data())
        {
            foreach (var one in picked)
            {
                var row = new vncscp { type = "test", machine_id = one.Id, batch = id, started_at = DateTime.UtcNow };
                db.vncscps.Add(row);
                db.SaveChanges();
                jobs.Add((one, new Job { Mode = "test", Row = row.id, Started = DateTime.UtcNow }));
            }
        }

        foreach (var (one, job) in jobs) one.Job = job;
        clock.Interval = TimeSpan.FromMilliseconds(200);
        Render();

        // 同時連：登入成功就斷開，記下花了幾毫秒
        await Task.WhenAll(jobs.Select(async pair =>
        {
            var (one, job) = pair;
            string? note = null;
            DateTime? cancelled = null;
            try
            {
                using var session = await Rfb.Login(one.Ip, one.Port, Secrets.Secret($"machine:{one.Id}"), token);
                job.Connected = session.ConnectedMs;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                cancelled = DateTime.UtcNow;
                note = $"等了 {(cancelled.Value - job.Started).TotalSeconds:0.0} 秒沒有回應，取消";
            }
            catch (Exception error)
            {
                note = error.Message;
            }

            using (var db = Data())
            {
                db.vncscps.Where(row => row.id == job.Row).ExecuteUpdate(set => set
                    .SetProperty(row => row.connected_ms, job.Connected)
                    .SetProperty(row => row.finished_at, DateTime.UtcNow)
                    .SetProperty(row => row.cancelled_at, cancelled)
                    .SetProperty(row => row.note, note)
                    .SetProperty(row => row.updated_at, DateTime.UtcNow));
            }

            one.Job = null;
            if (note is null) batch.Ok.Add($"{one.Title} {job.Connected} ms");
            else if (cancelled is null) batch.Failed.Add($"{one.Title}：{note}");
            Reload();
            Render();
        }));

        // 一批結束：連上的合成一則，連不上的每台一則；按了取消就說幾台還沒回應
        var done = batch;
        busy = string.Empty;
        batch = null;
        stop.Dispose();
        stop = null;
        clock.Interval = TimeSpan.FromSeconds(1);
        var reached = done.Ok.Count > 0 ? $"連上 {done.Ok.Count} 台：{string.Join("、", done.Ok)}" : string.Empty;
        if (done.Stopped > 0) Say(new NoteItem { Text = $"已取消測試：{done.Stopped} 台還沒回應{(reached.Length > 0 ? "；" + reached : string.Empty)}" });
        else
        {
            if (reached.Length > 0) Say(new NoteItem { Text = reached });
            foreach (var text in done.Failed) Say(new NoteItem { Text = text, Error = true });
        }

        Render();
    }

    /// <summary>取消：停下每一台；送到一半的照實記下送了多少，測試中的記下等了多久</summary>
    /// <param name="sender">取消或取消測試</param>
    /// <param name="e">點一下</param>
    private void Cancel(object sender, RoutedEventArgs e)
    {
        if (busy.Length == 0 || batch is null || stop is null) return;
        batch.Stopped = machines.Count(one => one.Job is not null);
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

    /// <summary>滑鼠停在通知上：先不收</summary>
    /// <param name="sender">那一則（Tag 是這一則）</param>
    /// <param name="e">滑鼠進來</param>
    private void HoldNote(object sender, MouseEventArgs e) =>
        ((NoteItem)((FrameworkElement)sender).Tag).Timer.Stop();

    /// <summary>滑鼠離開通知：資訊重新數 4 秒</summary>
    /// <param name="sender">那一則（Tag 是這一則）</param>
    /// <param name="e">滑鼠離開</param>
    private void ResumeNote(object sender, MouseEventArgs e)
    {
        var note = (NoteItem)((FrameworkElement)sender).Tag;
        if (!note.Error) note.Timer.Start();
    }

    #endregion

    #region 讀與畫

    /// <summary>讀 vw_vncscp：機台照 id 併進畫面上的那一台（勾與正在跑的留著），最近送出整份換掉；回傳上次的來源資料夾</summary>
    private string Reload()
    {
        using var db = Data();
        var view = db.vw_vncscps.AsNoTracking().ToList();

        // 機台：新的加進來、不在的拿掉、在的換成資料庫的值
        var found = view.Where(one => one.type == "machine").ToList();
        machines.RemoveAll(one => found.All(row => row.id != one.Id));
        foreach (var row in found)
        {
            var one = machines.FirstOrDefault(m => m.Id == row.id);
            if (one is null) machines.Add(one = new MachineItem { Id = row.id ?? 0 });
            one.Name = row.name ?? "—";
            one.Ip = row.ip ?? string.Empty;
            one.Port = row.port ?? 0;
            one.Title = row.name ?? $"{row.ip}:{row.port}";
            one.Address = $"{row.address}";
            one.Path = row.remote_path ?? string.Empty;
            one.RecvAt = row.recv_mtime is null ? null : Micro(row.recv_mtime.Value);
            one.Recv = row.recv_text ?? "—";
            one.SentAt = row.sent_at;
            one.Sent = row.sent_text ?? "—";
            one.Ping = row.ping_ms;
            one.PingAt = row.ping_at;
            one.FailText = row.fail_text;
            one.FailAt = row.fail_at;
            one.FailError = row.fail_error ?? false;
            one.InLast = row.in_last ?? false;
        }

        // 最近送出與測試：新的在前
        Log.ItemsSource = view.Where(one => one.type == "event").Select(one => new LogItem
        {
            At = $"{one.at_text}",
            Machine = $"{one.name}",
            Action = $"{one.action}",
            What = $"{one.what}",
            Took = $"{one.took_text}",
            Note = one.note ?? "—",
            NoteInk = (Brush)FindResource(one.note is null ? "TextFaint" : one.danger == true ? "Danger" : "TextMuted"),
        }).ToList();

        return view.FirstOrDefault(one => one.type == "setting")?.source ?? string.Empty;
    }

    /// <summary>把現在的狀態攤到畫面上：工具列、每一台、新增機台、最近送出、來源、送出鈕</summary>
    private void Render()
    {
        var now = DateTime.UtcNow;
        var locked = busy.Length > 0;
        DateTime? local = files.Count > 0 ? files.Max(one => one.Mtime) : null;
        var total = files.Sum(one => one.Bytes);
        var picked = machines.Count(one => one.Checked);

        // 搜尋與排序：看得到的那幾台
        var q = Query.Text.Trim();
        bool Fits(MachineItem one) => q.Length == 0 || new[] { one.Name, one.Address, one.Path }.Any(v => v.Contains(q, StringComparison.OrdinalIgnoreCase));
        IComparable Value(MachineItem one) => sortKey switch
        {
            "name" => one.Name,
            "addr" => string.Join(".", one.Ip.Split('.').Select(part => part.PadLeft(3, '0'))) + ":" + one.Port.ToString("00000", CultureInfo.InvariantCulture),
            "path" => one.Path,
            "diff" => one.RecvAt is null ? long.MaxValue : -one.RecvAt.Value.Ticks,
            _ => one.FailText is not null && one.FailError ? long.MaxValue : (long?)one.Ping ?? long.MinValue,
        };
        int Order(MachineItem a, MachineItem b)
        {
            var x = Value(a);
            var y = Value(b);
            var c = x is string s ? Words.Compare(s, (string)y) : x.CompareTo(y);
            return c != 0 ? (sortDesc ? -c : c) : Words.Compare(a.Name, b.Name);
        }

        var shown = machines.Where(Fits).ToList();
        shown.Sort(Order);
        if (!shown.SequenceEqual(rows))
        {
            rows.Clear();
            foreach (var one in shown) rows.Add(one);
        }

        // 工具列
        var has = machines.Count > 0;
        var allOn = shown.Count > 0 && shown.All(one => one.Checked);
        var some = !allOn && shown.Any(one => one.Checked);
        var last = machines.Where(one => one.InLast).ToList();
        All.Visibility = QueryFrame.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
        NoMachine.Visibility = has ? Visibility.Collapsed : Visibility.Visible;
        All.IsChecked = allOn ? true : some ? null : false;
        All.IsEnabled = !locked;
        AllCount.Text = $"{shown.Count}";
        QueryHint.Visibility = Query.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        ClearQuery.Visibility = q.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        MatchButton.Visibility = has && q.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        MatchButton.IsEnabled = !locked && shown.Count > 0;
        MatchCount.Text = $"{shown.Count}";
        LastButton.Visibility = has && last.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        LastButton.IsEnabled = !locked;
        LastButton.ToolTip = string.Join("、", last.Select(one => one.Title));
        LastCount.Text = $"{last.Count}";
        StopTest.Visibility = busy == "test" ? Visibility.Visible : Visibility.Collapsed;
        TestButton.IsEnabled = !locked && picked > 0;
        TestCount.Text = $"{picked}";

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
            var job = one.Job;
            var sending = job is { Mode: "send" };
            one.Pointer = pointer;
            one.Free = !locked;
            one.Editable = job is null;
            one.Sending = sending;
            one.Idle = !sending;
            one.PathTitle = one.Path.Length > 0 ? one.Path : "還沒填遠端路徑";
            one.DiffTitle = $"機台那份 {one.Recv} · 上次送達 {one.Sent}";
            one.Diff = local is null || one.RecvAt is null ? "—"
                : local.Value == one.RecvAt.Value ? "相同"
                : (local.Value > one.RecvAt.Value ? "本機新 " : "機台新 ") + Span((local.Value - one.RecvAt.Value).Duration());

            // 上次回應：測試中寫等了多久；上次沒成功寫原因；不然寫回應的毫秒與多久以前
            var ink = (Brush)FindResource(one.Checked ? "Brand700" : "TextPrimary");
            if (job is { Mode: "test" })
            {
                one.Reply = $"連線中 {(now - job.Started).TotalSeconds:0.0} 秒";
                one.ReplyInk = ink;
                one.ReplyAlign = TextAlignment.Right;
            }
            else if (one.FailText is not null)
            {
                one.Reply = one.FailText;
                one.ReplyInk = (Brush)FindResource(one.FailError ? "Danger" : "TextMuted");
                one.ReplyAlign = TextAlignment.Left;
            }
            else
            {
                one.Reply = one.Ping is null ? "—" : $"{one.Ping} ms · {Ago(one.PingAt ?? now, now)}";
                one.ReplyInk = ink;
                one.ReplyAlign = TextAlignment.Right;
            }

            one.ReplyTitle = one.FailText is not null && job is null ? $"{Ago(one.FailAt ?? now, now)}：{one.FailText}" : one.Reply;

            // 送的時候：連線中寫等了多久；傳送中寫送了多少、多快
            if (sending && job!.Phase == "connect")
            {
                one.Bar = 0;
                one.Progress = $"連線到 {one.Address} · {(now - job.Started).TotalSeconds:0.0} 秒";
            }

            if (sending && job!.Phase == "send")
            {
                var sent = Interlocked.Read(ref job.Sent);
                var secs = Math.Max(0.001, (now - job.SendAt).TotalSeconds);
                one.Bar = job.Total == 0 ? 88 : 88.0 * sent / job.Total;
                one.Progress = $"{Kb(sent)} / {Kb(job.Total)} KB · {Math.Round(sent / 1024.0 / secs)} KB/s · {(now - job.Started).TotalSeconds:0.0} 秒";
            }
        }

        // 新增機台、第一次使用
        AddOpen.Visibility = !adding && has ? Visibility.Visible : Visibility.Collapsed;
        AddOpen.IsEnabled = !locked;
        AddPanel.Visibility = adding ? Visibility.Visible : Visibility.Collapsed;
        AddFault.Visibility = fault.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        NameFrame.Tag = fault == "name" ? "fault" : null;
        IpFrame.Tag = fault == "ip" ? "fault" : null;
        PortFrame.Tag = fault == "port" ? "fault" : null;
        First.Visibility = !adding && !has ? Visibility.Visible : Visibility.Collapsed;
        LogHead.BorderThickness = has || adding ? new Thickness(0, 0, 0, 1) : new Thickness(0, 1, 0, 1);
        NoLog.Visibility = Log.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        Fit(Left, null!);

        // 來源
        FilesButton.IsEnabled = FolderButton.IsEnabled = busy != "send";
        Where.Text = folder.Length > 0 ? folder : "—";
        FileSummary.Text = files.Count > 0 ? $"{files.Count} 個 · {Kb(total)} KB" : "—";
        LocalTime.Text = Time(local, now);
        if (drawn != files)
        {
            drawn = files;
            FileList.ItemsSource = files.Select(one => new FileShown { Name = Middle(one.Relative, 44), Full = one.Relative, Size = Size(one.Bytes), Time = Time(one.Mtime, now) }).ToList();
        }
        NoFiles.Visibility = files.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        // 送出鈕；送的時候換成整體進度與取消
        var running = machines.Where(one => one.Job is { Mode: "send" }).Select(one => one.Job!).ToList();
        IdlePanel.Visibility = busy == "send" ? Visibility.Collapsed : Visibility.Visible;
        BusyPanel.Visibility = busy == "send" ? Visibility.Visible : Visibility.Collapsed;
        var since = running.Count > 0 ? running.Min(one => one.Started) : now;
        Overall.Text = $"{running.Count} 台 · {Kb(running.Sum(one => Interlocked.Read(ref one.Sent)))} / {Kb(running.Sum(one => one.Total))} KB · {(now - since).TotalSeconds:0.0} 秒";
        SendNote.Text = busy == "test" ? "正在測試連線"
            : files.Count == 0 ? "還沒選來源"
            : picked == 0 ? "還沒勾要送的機台"
            : $"{files.Count} 個檔 · {Kb(total)} KB → 各台自己的遠端路徑";
        SendButton.IsEnabled = !locked && files.Count > 0 && picked > 0;
        SendCount.Text = $"{picked}";
    }

    /// <summary>連平台資料庫</summary>
    private HelixStudioDbContext Data() =>
        new(new DbContextOptionsBuilder<HelixStudioDbContext>().UseNpgsql(Connection).Options);

    /// <summary>往上找第一個某種型別的元素</summary>
    /// <param name="from">從這裡開始</param>
    private static T? Up<T>(DependencyObject? from) where T : DependencyObject
    {
        while (from is not null and not T) from = from is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(from) : LogicalTreeHelper.GetParent(from);
        return from as T;
    }

    /// <summary>時間只留到微秒：資料庫存到微秒，兩邊才比得出「相同」</summary>
    /// <param name="at">時間（UTC）</param>
    private static DateTime Micro(DateTime at) =>
        new(at.Ticks - at.Ticks % 10, DateTimeKind.Utc);

    /// <summary>KB，一位小數、千分位</summary>
    /// <param name="bytes">位元組</param>
    private static string Kb(long bytes) =>
        (bytes / 1024.0).ToString("#,##0.0", CultureInfo.InvariantCulture);

    /// <summary>檔案大小：B、KB、MB</summary>
    /// <param name="bytes">位元組</param>
    private static string Size(long bytes) =>
        bytes < 1024 ? $"{bytes} B" : bytes < 1024 * 1024 ? $"{Kb(bytes)} KB" : $"{bytes / 1024.0 / 1024.0:0.00} MB";

    /// <summary>時間：今年的寫 月-日 時:分，不是今年的寫 年-月-日；沒有寫 —</summary>
    /// <param name="at">時間（UTC）</param>
    /// <param name="now">現在（UTC）</param>
    private static string Time(DateTime? at, DateTime now)
    {
        if (at is null) return "—";
        var shown = at.Value.ToLocalTime();
        return shown.Year != now.ToLocalTime().Year ? shown.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : shown.ToString("MM-dd HH:mm", CultureInfo.InvariantCulture);
    }

    /// <summary>一段時間：秒、分、時 分、天 時</summary>
    /// <param name="span">多久</param>
    private static string Span(TimeSpan span)
    {
        var s = (long)Math.Round(span.TotalSeconds);
        if (s < 60) return $"{s} 秒";
        var m = s / 60;
        if (m < 60) return $"{m} 分";
        var h = m / 60;
        return h < 24 ? $"{h} 時 {m % 60} 分" : $"{h / 24} 天 {h % 24} 時";
    }

    /// <summary>多久以前：秒前、分前、時前、天前</summary>
    /// <param name="at">那時（UTC）</param>
    /// <param name="now">現在（UTC）</param>
    private static string Ago(DateTime at, DateTime now)
    {
        var s = Math.Max(1, (long)Math.Round((now - at.ToUniversalTime()).TotalSeconds));
        if (s < 60) return $"{s} 秒前";
        var m = s / 60;
        if (m < 60) return $"{m} 分前";
        var h = m / 60;
        return h < 24 ? $"{h} 時前" : $"{h / 24} 天前";
    }

    /// <summary>名稱太長從中間截，留住副檔名</summary>
    /// <param name="name">名稱</param>
    /// <param name="max">最多幾個字</param>
    private static string Middle(string name, int max)
    {
        if (name.Length <= max) return name;
        var tail = (int)Math.Floor(max * 0.4);
        return name[..(max - tail - 1)] + "…" + name[^tail..];
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
        /// <summary>machine 那一列的 id</summary>
        public int Id { get; set; }

        /// <summary>IP</summary>
        public string Ip { get; set; } = string.Empty;

        /// <summary>UltraVNC 的 Port</summary>
        public int Port { get; set; }

        /// <summary>通知與紀錄裡的稱呼：名稱，空的寫 IP:Port</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>機台那份的修改時間（UTC，到微秒）；送到現在這個遠端路徑的最後一次成功</summary>
        public DateTime? RecvAt { get; set; }

        /// <summary>那一次送完的時刻</summary>
        public DateTime? SentAt { get; set; }

        /// <summary>最後一次登入成功的毫秒</summary>
        public int? Ping { get; set; }

        /// <summary>那一次的開始時刻</summary>
        public DateTime? PingAt { get; set; }

        /// <summary>最後一次沒成功的原因；成功的比它新就是 null</summary>
        public string? FailText { get; set; }

        /// <summary>那一次結束的時刻</summary>
        public DateTime? FailAt { get; set; }

        /// <summary>失敗是 true、取消是 false</summary>
        public bool FailError { get; set; }

        /// <summary>最後一批送出有沒有這一台</summary>
        public bool InLast { get; set; }

        /// <summary>正在跑的那一件；沒有是 null</summary>
        public Job? Job { get; set; }

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

        /// <summary>沒在送：顯示機台那份到上次回應</summary>
        public bool Idle { get => Get<bool>(); set => Set(value); }

        /// <summary>進度條的長度（滿是 88）</summary>
        public double Bar { get => Get<double>(); set => Set(value); }

        /// <summary>進度的字</summary>
        public string Progress { get => Get<string>(); set => Set(value); }

        /// <summary>機台那份的字</summary>
        public string Recv { get => Get<string>(); set => Set(value); }

        /// <summary>差：本機新／機台新多久、相同、—</summary>
        public string Diff { get => Get<string>(); set => Set(value); }

        /// <summary>差的提示：機台那份與上次送達</summary>
        public string DiffTitle { get => Get<string>(); set => Set(value); }

        /// <summary>上次送達的字</summary>
        public string Sent { get => Get<string>(); set => Set(value); }

        /// <summary>上次回應的字</summary>
        public string Reply { get => Get<string>(); set => Set(value); }

        /// <summary>上次回應的提示</summary>
        public string ReplyTitle { get => Get<string>(); set => Set(value); }

        /// <summary>上次回應的顏色：失敗紅、取消灰</summary>
        public Brush ReplyInk { get => Get<Brush>(); set => Set(value); }

        /// <summary>上次回應靠哪邊：毫秒靠右、原因靠左</summary>
        public TextAlignment ReplyAlign { get => Get<TextAlignment>(); set => Set(value); }
    }

    /// <summary>一台正在跑的那一件</summary>
    public class Job
    {
        /// <summary>send 或 test</summary>
        public string Mode { get; set; } = string.Empty;

        /// <summary>connect（連線中）或 send（傳送中）</summary>
        public string Phase { get; set; } = "connect";

        /// <summary>send／test 那一列的 id</summary>
        public int Row { get; set; }

        /// <summary>開始連線的時刻（UTC）</summary>
        public DateTime Started { get; set; }

        /// <summary>開始傳送的時刻（UTC）</summary>
        public DateTime SendAt { get; set; }

        /// <summary>登入花了幾毫秒；沒登入成功是 null</summary>
        public int? Connected { get; set; }

        /// <summary>要送的總位元組</summary>
        public long Total { get; set; }

        /// <summary>要送幾個檔</summary>
        public int Count { get; set; }

        /// <summary>送到哪裡</summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>已經送了多少位元組（傳送的那條執行緒在寫）</summary>
        public long Sent;
    }

    /// <summary>一批送出或測試的結果</summary>
    public class Batch
    {
        /// <summary>send 或 test</summary>
        public string Mode { get; set; } = string.Empty;

        /// <summary>送幾個檔</summary>
        public int Count { get; set; }

        /// <summary>每台要送的總位元組</summary>
        public long Total { get; set; }

        /// <summary>成功的那幾台（測試時附毫秒）</summary>
        public List<string> Ok { get; } = [];

        /// <summary>失敗的每一台：照實寫原因</summary>
        public List<string> Failed { get; } = [];

        /// <summary>按取消時還在跑的台數；沒按是 0</summary>
        public int Stopped { get; set; }
    }

    /// <summary>來源的一個檔</summary>
    public class FileItem
    {
        /// <summary>本機完整路徑</summary>
        public string Full { get; set; } = string.Empty;

        /// <summary>在來源資料夾底下的相對路徑；挑檔案時是檔名</summary>
        public string Relative { get; set; } = string.Empty;

        /// <summary>大小</summary>
        public long Bytes { get; set; }

        /// <summary>修改時間（UTC，到微秒）</summary>
        public DateTime Mtime { get; set; }
    }

    /// <summary>來源清單的一列</summary>
    public class FileShown
    {
        /// <summary>名稱（太長從中間截）</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>完整的相對路徑</summary>
        public string Full { get; set; } = string.Empty;

        /// <summary>大小的字</summary>
        public string Size { get; set; } = string.Empty;

        /// <summary>修改時間的字</summary>
        public string Time { get; set; } = string.Empty;
    }

    /// <summary>最近送出與測試的一列</summary>
    public class LogItem
    {
        /// <summary>時間</summary>
        public string At { get; set; } = string.Empty;

        /// <summary>機台</summary>
        public string Machine { get; set; } = string.Empty;

        /// <summary>動作</summary>
        public string Action { get; set; } = string.Empty;

        /// <summary>內容</summary>
        public string What { get; set; } = string.Empty;

        /// <summary>耗時</summary>
        public string Took { get; set; } = string.Empty;

        /// <summary>錯誤；沒有寫 —</summary>
        public string Note { get; set; } = string.Empty;

        /// <summary>錯誤的顏色：失敗紅、取消灰、沒有淡灰</summary>
        public Brush NoteInk { get; set; } = Brushes.Transparent;
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
