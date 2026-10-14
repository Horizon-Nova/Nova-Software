using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using HelixStudio.Contract;
using Models.HelixStudio;

namespace InSpecTool;

/// <summary>InSpecTool 的量測頁：左邊條碼與欄位，右邊量測程式與這次的結果</summary>
public partial class Measure : UserControl, ITool, IData
{
    /// <summary>平台載入後塞進來的保險箱；這支工具用不到，只為符合合約</summary>
    public ISecrets Secrets { get; set; } = null!;

    /// <summary>平台載入後塞進來的資料庫連線</summary>
    public string Connection { get; set; } = string.Empty;

    private readonly List<FieldItem> fields = [];

    private readonly List<ProgramItem> programs = [];

    /// <summary>vw_inspec_measure 的 setting 那一列：這一頁開起來時讀一次</summary>
    private vw_inspec_measure setup = new();

    private readonly DispatcherTimer scan = new();

    private FileSystemWatcher? watcher;

    private Manual? manual;

    private List<ProgramItem> index = [];

    private string indexRoot = string.Empty;

    private bool running;

    private bool typing = true;

    /// <summary>備好這一頁</summary>
    public Measure()
    {
        InitializeComponent();

        Fields.ItemsSource = fields;
        Programs.ItemsSource = programs;

        scan.Tick += (_, _) => Parse();

        // 平台的核取方塊要的勾（lucide check）
        DataContext = new { CheckPaths = new List<string> { "M20 6 9 17l-5-5" } };
    }

    #region 量測

    /// <summary>畫面掛上來：讀設定與欄位、開始看 InSpec 自己寫入的檔，條碼框取得焦點</summary>
    /// <param name="sender">這一頁</param>
    /// <param name="e">掛上來這一次</param>
    private void Ready(object sender, RoutedEventArgs e)
    {
        // 讀設定（一列）與欄位（照順序）；設定在這裡讀一次，改了設定重新開這支工具
        using var db = Data();
        var rows = db.vw_inspec_measures.AsNoTracking().ToList();
        setup = rows.Single(one => one.type == "setting");

        fields.Clear();
        fields.AddRange(rows.Where(one => one.type == "field").Select(one => new FieldItem
        {
            Name = $"{one.name}",
            Prompt = $"{one.prompt}",
            Mode = $"{one.mode}",
            Fixed = one.editable != true,
            Hint = one.confirm == true ? "執行時先確認再填" : null,
        }));
        Fields.Items.Refresh();

        // 最近執行：欄名照欄位結構，一次一列，新的在上
        RecentHeads.ItemsSource = fields.Select(one => one.Name).ToList();
        Recent.ItemsSource = rows.Where(one => one.type == "run").OrderBy(one => one.sort).ToList();

        ShowNext.IsChecked = setup.show_result;
        ShowAfter.IsChecked = setup.show_result;

        // 左欄上次拖到多寬；沒拖過就照版面比例
        if (setup.left_width is double left)
        {
            LeftColumn.Width = new GridLength(left);
            RightColumn.Width = new GridLength(1, GridUnitType.Star);
        }

        // 操作員自己用 InSpec 跑時，InSpec 會寫到這個檔；寫入就跳出歸檔視窗
        Directory.CreateDirectory(Path.GetDirectoryName(InputFile)!);
        watcher?.Dispose();
        watcher = new FileSystemWatcher(Path.GetDirectoryName(InputFile)!, Path.GetFileName(InputFile))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
            EnableRaisingEvents = true,
        };
        watcher.Changed += (_, _) => Dispatcher.BeginInvoke(Written, DispatcherPriority.Background);
        watcher.Created += (_, _) => Dispatcher.BeginInvoke(Written, DispatcherPriority.Background);

        // 平台關著時 InSpec 寫進去、還沒歸檔的那一筆：一打開就接著跳出歸檔視窗
        Dispatcher.BeginInvoke(Written, DispatcherPriority.Background);

        Barcode.Focus();
        Barcode.SelectAll();

        Search();
    }

    /// <summary>畫面拿掉：不再看 InSpec 寫入的檔</summary>
    /// <param name="sender">這一頁</param>
    /// <param name="e">拿掉這一次</param>
    private void Gone(object sender, RoutedEventArgs e)
    {
        watcher?.Dispose();
        watcher = null;
    }

    /// <summary>條碼框有字進來：停手「條碼解析間隔」之後才解析</summary>
    /// <param name="sender">條碼框</param>
    /// <param name="e">這一次改字</param>
    private void Typed(object sender, TextChangedEventArgs e)
    {
        if (!typing) return;

        scan.Stop();
        scan.Interval = TimeSpan.FromMilliseconds(setup.scan_interval ?? 0);
        scan.Start();
    }

    /// <summary>改了一個欄位：條碼跟著欄位的值，重新找量測程式（欄位格子不重畫，點下一格一次就進得去）</summary>
    /// <param name="sender">那一格</param>
    /// <param name="e">離開這一格</param>
    private void Edited(object sender, RoutedEventArgs e)
    {
        // 條碼就是每一格的值依序以 | 接起來；後面沒填的不補 |，全空就是空的
        var joined = string.Join("|", fields.Select(one => one.Value.Trim())).TrimEnd('|');

        typing = false;
        Barcode.Text = joined.Length == 0 ? string.Empty : $"|{joined}";
        typing = true;

        Search();
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
        var value = $"{Math.Round(LeftColumn.ActualWidth)}";

        using var db = Data();
        db.inspecs.Where(one => one.type == "setting" && one.setting_key == "leftWidth")
            .ExecuteUpdate(set => set.SetProperty(one => one.value, value).SetProperty(one => one.updated_at, DateTime.UtcNow));

        setup.left_width = Math.Round(LeftColumn.ActualWidth);
    }

    /// <summary>清除條碼與欄位</summary>
    /// <param name="sender">清除</param>
    /// <param name="e">這一次點擊</param>
    private void Clear(object sender, RoutedEventArgs e)
    {
        typing = false;
        Barcode.Clear();
        typing = true;

        fields.ForEach(one => one.Value = string.Empty);
        BarcodeFault.Visibility = Visibility.Collapsed;

        Search();
        Barcode.Focus();
    }

    /// <summary>重新查找：丟掉資料夾快取，重新找一次</summary>
    /// <param name="sender">重新查找</param>
    /// <param name="e">這一次點擊</param>
    private void Again(object sender, RoutedEventArgs e)
    {
        indexRoot = string.Empty;

        Search();
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

    /// <summary>執行量測：開檔、自動填提示、InSpec 跑完才歸檔</summary>
    /// <param name="sender">雙擊的那一列，或按 Enter 的清單</param>
    /// <param name="e">這一次操作</param>
    private async void Run(object sender, RoutedEventArgs e)
    {
        // 選了哪一支；正在跑就不再送
        if (Programs.SelectedItem is not ProgramItem program || running) return;

        // InSpec 寫進去、還沒記下來的那一筆先記下來，這一次開跑清空那個檔時它才不會不見
        Written();

        // 欄位：「直接填」的不可空白
        var missing = fields.Where(one => one.Mode == "fill" && string.IsNullOrWhiteSpace(one.Value)).Select(one => one.Name).ToList();
        if (missing.Count > 0)
        {
            Tell($"請先填好 {string.Join("、", missing)}", []);
            return;
        }

        // 檔案要在，而且不是空程式
        if (!File.Exists(program.Path) || new FileInfo(program.Path).Length <= 2)
        {
            Tell($"「{program.Name}」不存在或是空的程式", []);
            return;
        }

        // 跑的時候左邊不能改、不能再按
        running = true;
        Inputs.IsEnabled = false;
        AgainButton.IsEnabled = false;
        Programs.IsEnabled = false;
        ResultLayer.Visibility = Visibility.Collapsed;

        // 這次執行的欄位值另存一份，跑的途中改欄位不影響這一次
        var snapshot = fields.Select(one => new FieldItem { Name = one.Name, Prompt = one.Prompt, Mode = one.Mode, Value = one.Value.Trim() }).ToList();
        var prompts = new List<PromptItem>();
        var alerts = new List<string>();

        // 記一列執行
        using var db = Data();
        var run = new inspec
        {
            type = "run",
            program = program.Path,
            fields = JsonSerializer.Serialize(snapshot.ToDictionary(one => one.Name, one => one.Value)),
            prompts = "[]",
            started_at = DateTime.UtcNow,
            created_at = DateTime.UtcNow,
            updated_at = DateTime.UtcNow,
        };
        db.inspecs.Add(run);
        await db.SaveChangesAsync();

        // 執行副本：自動匯出指到 InSpec 寫入的那個檔（跟操作員自己跑同一個出口）、打開、Unicode、CRLF；原檔不動
        // 跑完 InSpec 還開著這份副本，操作員再按一次執行，結果一樣寫進那個檔，跳出歸檔視窗
        var folder = Path.Combine(Path.GetTempPath(), "InSpecTool");
        Directory.CreateDirectory(folder);
        var copy = Path.Combine(folder, $"run_{run.id}.iwp");
        var export = InputFile;
        File.WriteAllText(export, string.Empty);

        var text = Regex.Replace(File.ReadAllText(program.Path), "\r?\n", "\r\n");
        text = Regex.Replace(text, "\\(AutoExpFile \"[^\"]*\"\\)", $"(AutoExpFile \"{export}\")");
        text = Regex.Replace(text, "\\(AutoExpFileInf \\(\\(Enab \\d\\)", "(AutoExpFileInf ((Enab 1)");
        text = Regex.Replace(text, "(\\(AutoExpFileInf \\(\\(Enab 1\\) \\(ReTm \\d\\) \\(Conf \\d\\) \\(FrmInf \\(\\()RunSep \\d\\)", "$1RunSep 1)");
        text = Regex.Replace(text, "(\\(AutoExpFileInf \\(\\(Enab 1\\) \\(ReTm \\d\\) \\(Conf \\d\\) \\(FrmInf \\(\\(RunSep \\d\\) )\\(Unicode \\d\\)", "$1(Unicode 1)");
        File.WriteAllText(copy, text, Encoding.Unicode);

        // InSpec 裝在哪裡：讀它自己的安裝資訊，不寫死
        var home = new[] { RegistryView.Registry32, RegistryView.Registry64 }
            .Select(view => RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view).OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
            .Where(one => one is not null)
            .SelectMany(one => one!.GetSubKeyNames().Select(name => one.OpenSubKey(name)))
            .Where(one => $"{one?.GetValue("DisplayName")}".Contains("InSpec", StringComparison.OrdinalIgnoreCase))
            .Select(one => $"{one!.GetValue("InstallLocation")}")
            .FirstOrDefault(one => File.Exists(Path.Combine(one, "iscmd.exe"))) ?? string.Empty;

        if (home.Length == 0) run.note = "這台電腦找不到 InSpec 的安裝位置，沒有執行。";

        // InSpec 沒開就開起來，等它的主視窗出現
        var inspec = Process.GetProcessesByName("InSpec").FirstOrDefault();
        if (home.Length > 0 && inspec is null)
        {
            inspec = Process.Start(new ProcessStartInfo(Path.Combine(home, "InSpec.exe")) { UseShellExecute = true, WorkingDirectory = home })!;
            var waited = Stopwatch.StartNew();
            while (inspec.MainWindowHandle == IntPtr.Zero && waited.Elapsed < TimeSpan.FromSeconds(60))
            {
                await Task.Delay(200);
                inspec.Refresh();
            }
            await Task.Delay(3000);
        }

        // InSpec 有視窗出現就通知：提示依控制項編號處理，其他對話框把文字記下來
        var handled = new HashSet<IntPtr>();
        Native.WinEventProc seen = (hook, kind, hwnd, idObject, idChild, thread, time) =>
        {
            if (idObject != 0 || hwnd == IntPtr.Zero || Native.ClassOf(hwnd) != "#32770" || !handled.Add(hwnd)) return;

            // 不是提示（沒有提示文字 1278）：多半是錯誤或警告，記下它說什麼
            var label = Native.GetDlgItem(hwnd, 1278);
            if (label == IntPtr.Zero)
            {
                var words = string.Join(" ", Native.Children(hwnd).Where(one => one.Class == "Static").Select(one => Native.TextOf(one.Handle).Trim()).Where(one => one.Length > 0));
                if (words.Length > 0) alerts.Add(words);
                return;
            }

            // 提示文字和欄位設定的完全相同（不分大小寫）才填
            var ask = Native.TextOf(label).Trim();
            var field = snapshot.FirstOrDefault(one => string.Equals(one.Prompt.Trim(), ask, StringComparison.OrdinalIgnoreCase));
            if (field is null || field.Value.Length == 0)
            {
                prompts.Add(new PromptItem { prompt = ask, value = string.Empty, at = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") });
                return;
            }

            // 「先確認再填」：先讓操作員看一次、可以改
            var value = field.Mode == "confirm" ? Confirm.Ask(Window.GetWindow(this), field.Name, ask, field.Value) : field.Value;

            // 打字型填輸入框 499、清單型選 1269、下拉型選 1297，有哪個用哪個；送標準確定（IDOK）
            Native.SendMessage(Native.GetDlgItem(hwnd, 499), 0x000C, IntPtr.Zero, value);
            Native.SendMessage(Native.GetDlgItem(hwnd, 1269), 0x01A2, (IntPtr)(-1), value);
            Native.SendMessage(Native.GetDlgItem(hwnd, 1297), 0x014D, (IntPtr)(-1), value);
            Native.PostMessage(hwnd, 0x0111, (IntPtr)1, Native.GetDlgItem(hwnd, 1));

            prompts.Add(new PromptItem { prompt = ask, value = value, at = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") });
        };

        var hooks = new[] { 0x8002u, 0x0010u }
            .Select(kind => inspec is null ? IntPtr.Zero : Native.SetWinEventHook(kind, kind, IntPtr.Zero, seen, (uint)inspec.Id, 0, 0))
            .ToList();

        // iscmd /run 不加 /nowait：回來就是 InSpec 跑完；InSpec 在跑完之前就不見了（被關掉或當掉），iscmd 會一直等，這時結束它
        if (home.Length > 0 && inspec is not null)
        {
            var go = Process.Start(new ProcessStartInfo(Path.Combine(home, "iscmd.exe"), $"/run \"{copy}\"") { UseShellExecute = false, CreateNoWindow = true })!;
            var gone = await Task.Run(() =>
            {
                while (!go.WaitForExit(500) && !inspec.HasExited) { }
                return !go.HasExited;
            });

            new[] { go }.Where(_ => gone).ToList().ForEach(one => one.Kill());
            run.exit_code = gone ? null : go.ExitCode;
            run.finished_at = DateTime.UtcNow;
            run.note = gone ? "InSpec 在跑完之前就關掉了。" : run.note;
        }

        hooks.Where(one => one != IntPtr.Zero).ToList().ForEach(one => Native.UnhookWinEvent(one));
        GC.KeepAlive(seen);

        // 讀這次的匯出，讀完清空，下一筆不會疊上這一筆；匯出是空的就是沒有內容
        var exported = Read(export);
        File.WriteAllText(export, string.Empty);
        var body = string.Concat(Runs(exported));
        run.export = body.Trim().Length > 0 ? body : null;
        run.prompts = JsonSerializer.Serialize(prompts);

        // 這一次的結果先存起來、畫面先解開；後面歸檔出了錯，結果也不會不見，也不會卡住下一次
        run.updated_at = DateTime.UtcNow;
        await db.SaveChangesAsync();

        running = false;
        Inputs.IsEnabled = true;
        AgainButton.IsEnabled = true;
        Programs.IsEnabled = true;

        // 歸檔路徑：照料號、批號、壓合算
        var relative = Relative(
            snapshot.FirstOrDefault(one => Is(one, PartWords))?.Value ?? string.Empty,
            snapshot.FirstOrDefault(one => Is(one, LotWords))?.Value ?? string.Empty,
            snapshot.FirstOrDefault(one => Is(one, PressWords))?.Value ?? string.Empty);

        // 跑完才歸檔：主要路徑的根資料夾不在（例如網路磁碟斷了）就改寫備份路徑
        var roots = new[] { $"{setup.log_folder}".Trim(), $"{setup.backup_folder}".Trim() }.Where(one => one.Length > 0).ToList();
        var unreachable = roots.Where(one => !Directory.Exists(one)).ToList();
        var targets = roots
            .Where(_ => run.finished_at is not null && run.export is not null && relative.Length > 0)
            .Where(Directory.Exists)
            .Select(root => Path.GetFullPath(Path.Combine(root, relative)))
            .ToList();
        var overlong = targets.Where(one => one.Split(Path.DirectorySeparatorChar).Any(part => part.Length > NameLimit)).ToList();
        var archived = targets.Except(overlong).FirstOrDefault();

        new[] { archived }.Where(one => one is not null).ToList().ForEach(one => Append(one!, run.export!));

        run.archive = archived;
        run.note = string.Join(" ", new[]
        {
            run.note,
            run.exit_code is null ? null : run.export is null ? "InSpec 跑完了，但匯出是空的（程式裡沒有設定要匯出的項目），沒有歸檔。" : null,
            run.export is not null && relative.Length == 0 ? "料號、批號、壓合缺一項，沒有歸檔。" : null,
            archived is not null && unreachable.Count > 0 ? "主要路徑找不到，改寫到備份路徑。" : null,
            run.export is not null && relative.Length > 0 && archived is null ? "系統紀錄資料夾與備份輸出路徑都找不到，沒有歸檔。" : null,
            unreachable.Count > 0 ? $"找不到：{string.Join("、", unreachable)}" : null,
            overlong.Count > 0 ? $"路徑裡有一層超過 Windows 的 {NameLimit} 字上限，沒有歸檔：{overlong.First()}" : null,
            alerts.Count > 0 ? $"InSpec 訊息：{string.Join("；", alerts)}" : null,
        }.Where(one => !string.IsNullOrEmpty(one)));
        run.note = run.note.Length == 0 ? null : run.note;
        run.updated_at = DateTime.UtcNow;
        await db.SaveChangesAsync();

        // 最近執行多了這一次
        Recent.ItemsSource = db.vw_inspec_measures.AsNoTracking().Where(one => one.type == "run").OrderBy(one => one.sort).ToList();

        // 這次的結果：從 vw_inspec_measure 讀這一次的 run；沒歸檔一定顯示，歸檔了照「下次跑完也顯示結果」
        var shown = db.vw_inspec_measures.AsNoTracking().Single(one => one.type == "run" && one.id == run.id);
        if (shown.archived == true && setup.show_result != true) return;

        var rows = new List<FactItem>
        {
            new() { Label = "檔案", Value = shown.archive ?? "—" },
            new() { Label = "開始", Value = $"{shown.started_text}" },
        };
        rows.AddRange(prompts.Select(one => new FactItem { Label = one.prompt, Value = one.value.Length > 0 ? one.value : "—" }));
        rows.AddRange(new[] { shown.note }.Where(one => one is not null).Select(one => new FactItem { Label = "備註", Value = one! }));

        Tell(shown.archived == true ? "已歸檔" : "沒有歸檔", rows, $"{shown.elapsed_text}", shown.archived == true);
        ShowNext.IsChecked = setup.show_result;
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
        var value = on ? "true" : "false";

        ShowNext.IsChecked = on;
        ShowAfter.IsChecked = on;

        using var db = Data();
        db.inspecs.Where(one => one.type == "setting" && one.setting_key == "showResult")
            .ExecuteUpdate(set => set.SetProperty(one => one.value, value).SetProperty(one => one.updated_at, DateTime.UtcNow));

        setup.show_result = on;
    }

    /// <summary>InSpec 自己寫入了：讀出內容，跳出歸檔視窗</summary>
    private void Written()
    {
        // InSpecTool 自己正在跑的那一次，寫入由跑完那一段讀，不跳歸檔視窗
        if (running) return;

        // 讀出內容；檔案還不在或還是空的（InSpec 還沒寫完）就等下一次寫入
        var content = File.Exists(InputFile) ? Read(InputFile) : string.Empty;
        if (content.Trim().Length == 0) return;

        // InSpec 開著的量測程式：標題列「InSpec 視窗版 - 檔名」第一個「 - 」之後就是檔名（檔名自己也可能有「 - 」），到搜尋資料夾找它在哪裡
        var title = Process.GetProcessesByName("InSpec").Select(one => one.MainWindowTitle).FirstOrDefault(one => one.Length > 0) ?? string.Empty;
        var name = title.Contains(" - ") ? title[(title.IndexOf(" - ", StringComparison.Ordinal) + 3)..].Trim() : string.Empty;
        var root = $"{setup.search_folder}".Trim();
        var found = name.Length == 0 || !Directory.Exists(root) ? null : Directory
            .EnumerateFiles(root, "*.iwp", SearchOption.AllDirectories)
            .FirstOrDefault(one => string.Equals(Path.GetFileNameWithoutExtension(one), name, StringComparison.OrdinalIgnoreCase)
                                || string.Equals(Path.GetFileName(one), name, StringComparison.OrdinalIgnoreCase));

        var folder = $"{setup.log_folder}".Trim();
        var written = File.GetLastWriteTimeUtc(InputFile);

        // 程式的自動匯出有開分隔時，InSpec 每跑一次前後各寫一行 :BEGIN、:END；照這兩行切成一次一筆，沒有就整段當一筆
        var blocks = Runs(content);

        // 每一筆先記成一列還沒歸檔的執行，再清空 InSpec 寫入的檔：之後 InSpec 再寫、InSpecTool 自己跑、視窗或平台被關，這些都還在
        using var db = Data();

        // InSpec 開著的是 InSpecTool 開跑時的副本 run_{編號}，而且那一列還沒結束（開跑後平台被關掉）：第一筆接回那一列，不另外新增
        var resumed = new[] { Regex.Match(name, @"^run_(\d+)$") }
            .Where(one => one.Success)
            .Select(one => int.Parse(one.Groups[1].Value))
            .Select(id => db.inspecs.FirstOrDefault(one => one.id == id && one.type == "run" && one.finished_at == null))
            .FirstOrDefault(one => one is not null);

        var items = blocks.Select((block, at) =>
        {
            // 這一筆每一欄的值：提示文字對得上那一欄的回答；沒有就用左邊欄位現在填的值（操作員掃過條碼再自己跑 InSpec）
            string Answered(string prompt) => block.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Split('\t').Select(cell => cell.Trim().Trim('"')).ToList())
                .Select(line => line.FindIndex(cell => string.Equals(cell, prompt, StringComparison.OrdinalIgnoreCase)) is var at and >= 0 && at + 2 < line.Count ? line[at + 2] : string.Empty)
                .FirstOrDefault(one => one.Length > 0) ?? string.Empty;
            var kept = at == 0 && resumed is not null ? JsonSerializer.Deserialize<Dictionary<string, string>>(resumed.fields) ?? [] : [];
            var values = fields.ToDictionary(one => one.Name,
                one => new[] { Answered(one.Prompt), kept.GetValueOrDefault(one.Name, string.Empty), one.Value.Trim() }.FirstOrDefault(two => two.Length > 0) ?? string.Empty);
            string Value(string[][] words) => fields.Where(one => Is(one, words)).Select(one => values[one.Name]).FirstOrDefault(one => one.Length > 0) ?? string.Empty;

            var row = at == 0 && resumed is not null ? resumed : new inspec
            {
                type = "run",
                program = found ?? name,
                prompts = "[]",
                note = "InSpec 自己寫入，還沒歸檔。",
                started_at = written,
                created_at = DateTime.UtcNow,
            };

            row.fields = JsonSerializer.Serialize(values);
            row.export = block;
            row.finished_at = written;
            row.updated_at = DateTime.UtcNow;
            row.note = row == resumed ? "InSpecTool 開跑後平台被關掉，InSpec 跑完才寫入，還沒歸檔。" : row.note;

            new[] { row }.Where(one => one != resumed).ToList().ForEach(one => db.inspecs.Add(one));

            return new { row, block, part = Value(PartWords), lot = Value(LotWords), press = Value(PressWords) };
        }).ToList();
        db.SaveChanges();
        File.WriteAllText(InputFile, string.Empty);

        Recent.ItemsSource = db.vw_inspec_measures.AsNoTracking().Where(one => one.type == "run").OrderBy(one => one.sort).ToList();

        // 歸檔視窗一次處理一筆；開著時又寫進來的接在後面
        var queue = items.Select(one => new Manual.PendingItem
        {
            Id = one.row.id,
            Content = one.block,
            Part = one.part,
            Lot = one.lot,
            Press = one.press,
            Time = written.ToLocalTime(),
            Program = name,
            ProgramFolder = found is null ? string.Empty : Path.GetRelativePath(root, Path.GetDirectoryName(found)!),
        }).ToList();

        if (manual is not null)
        {
            manual.Add(queue, folder);
            return;
        }

        manual = new Manual(Connection, setup, InputFile, found ?? name) { Owner = Window.GetWindow(this) };
        manual.Add(queue, folder);
        manual.Closed += (_, _) => manual = null;
        manual.Show();
    }

    #endregion

    #region 對照

    /// <summary>Windows 一層資料夾名或檔名最長幾個字</summary>
    public const int NameLimit = 255;

    /// <summary>中間那條分隔的寬度，跟 Measure.xaml 的第二欄一樣</summary>
    private const double Splitter = 12;

    /// <summary>操作員自己用 InSpec 跑時，InSpec 寫入的檔</summary>
    public const string InputFile = @"C:\InSpecTool\Input.txt";

    /// <summary>平台的資料庫</summary>
    private HelixStudioDbContext Data() =>
        new(new DbContextOptionsBuilder<HelixStudioDbContext>().UseNpgsql(Connection).Options);

    /// <summary>蓋在整頁上的結果視窗顯示一件事：標題、每一列、右上角的時間；有執行內容才附「下次跑完也顯示結果」</summary>
    /// <param name="title">標題</param>
    /// <param name="rows">每一列</param>
    /// <param name="elapsed">右上角</param>
    /// <param name="done">歸檔了（綠勾）還是沒有（紅色警示）</param>
    private void Tell(string title, List<FactItem> rows, string elapsed = "", bool done = false)
    {
        ResultTitle.Text = title;
        ResultElapsed.Text = elapsed;
        ResultMark.Data = Geometry.Parse(done ? "M20 6 9 17l-5-5" : "M12 8v4 M12 16h.01 M3 12a9 9 0 1 0 18 0a9 9 0 1 0 -18 0");
        ResultMark.Stroke = done ? new SolidColorBrush(Color.FromRgb(0x10, 0x7c, 0x10)) : (Brush)FindResource("Danger");
        ResultRows.ItemsSource = rows;
        ShowNext.Visibility = rows.Count > 0 ? Visibility.Visible : Visibility.Hidden;
        ResultLayer.Visibility = Visibility.Visible;
        CloseButton.Focus();
    }

    /// <summary>解析條碼：以 | 分段依序填入欄位，料號整理成找檔用的樣子，然後找量測程式</summary>
    private void Parse()
    {
        scan.Stop();

        // 開頭的 | 去掉；含 _ 就擋下
        var text = Barcode.Text.Trim().TrimStart('|');
        var blocked = text.Contains('_');
        BarcodeFault.Visibility = blocked ? Visibility.Visible : Visibility.Collapsed;

        var parts = blocked || text.Length == 0 ? [] : text.Split('|', StringSplitOptions.TrimEntries);
        fields.Select((one, at) => (one, at)).ToList().ForEach(each =>
            each.one.Value = each.at < parts.Length ? (Is(each.one, PartWords) ? Normal(parts[each.at]) : parts[each.at]) : string.Empty);

        Search();
    }

    /// <summary>找量測程式：檔名含 SETUP 與料號，新的在上</summary>
    private async void Search()
    {
        var part = fields.FirstOrDefault(one => Is(one, PartWords))?.Value.Trim() ?? string.Empty;
        var root = $"{setup.search_folder}".Trim();

        programs.Clear();

        // 資料夾掃一次記起來，換資料夾或按重新查找才重掃；在背景掃，畫面不卡
        if (part.Length > 0 && Directory.Exists(root) && !string.Equals(indexRoot, root, StringComparison.OrdinalIgnoreCase))
        {
            AgainButton.IsEnabled = false;
            index = await Task.Run(() => Directory.EnumerateFiles(root, "*.iwp", SearchOption.AllDirectories)
                .Select(one => new FileInfo(one))
                .Select(one => new ProgramItem { Name = one.Name, Path = one.FullName, At = one.LastWriteTime, Changed = one.LastWriteTime.ToString("yyyy-MM-dd HH:mm") })
                .ToList());
            indexRoot = root;
            AgainButton.IsEnabled = !running;
            programs.Clear();
        }

        var tokens = new[] { part.ToUpperInvariant(), Normal(part) }.Where(one => one.Length > 0).Distinct().ToList();
        programs.AddRange(part.Length == 0 || !Directory.Exists(root) ? [] : index
            .Where(one => one.Name.ToUpperInvariant().Contains("SETUP") && tokens.Any(token => one.Name.ToUpperInvariant().Contains(token)))
            .OrderByDescending(one => one.At));
        Programs.Items.Refresh();

        // 找不到：照實說是哪一種
        ProgramsEmptyText.Text = root.Length > 0 && !Directory.Exists(root) ? $"找不到搜尋資料夾 {root}" : "查無結果";
        ProgramsEmpty.Visibility = programs.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ProgramCount.Text = $"{programs.Count}";

        // 只有一支就直接選好，按 Enter 就跑
        Programs.SelectedIndex = programs.Count == 1 ? 0 : -1;
    }

    /// <summary>把一段內容附加到歸檔檔：新檔寫 UTF-16LE 加 BOM；寫不進去不吞掉，交給全域的錯誤捕捉記進 event_log</summary>
    /// <param name="target">歸檔檔</param>
    /// <param name="content">要附加的內容</param>
    public static void Append(string target, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);

        var fresh = !File.Exists(target) || new FileInfo(target).Length == 0;
        using var stream = new FileStream(target, FileMode.Append, FileAccess.Write, FileShare.Read);
        var bytes = (fresh ? Encoding.Unicode.GetPreamble() : []).Concat(Encoding.Unicode.GetBytes(content)).ToArray();

        stream.Write(bytes, 0, bytes.Length);
    }

    /// <summary>讀一個可能正在被寫的檔：跟寫的人共用，不擋它寫</summary>
    /// <param name="path">那個檔</param>
    public static string Read(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, Encoding.Default, detectEncodingFromByteOrderMarks: true);

        return reader.ReadToEnd();
    }

    /// <summary>料號整理：轉大寫，最後一個 - 之後只留開頭的連續字母（AB1-C45 → AB1-C）</summary>
    /// <param name="part">原始料號</param>
    public static string Normal(string part)
    {
        var upper = part.Trim().ToUpperInvariant();
        var dash = upper.LastIndexOf('-');
        var letters = dash < 0 ? string.Empty : new string(upper[(dash + 1)..].TakeWhile(char.IsLetter).ToArray());

        return letters.Length == 0 ? upper : upper[..(dash + 1)] + letters;
    }

    /// <summary>這一欄是不是某一種：名稱或提示含有其中一組的每一個字</summary>
    /// <param name="field">那一欄</param>
    /// <param name="sets">幾組字</param>
    public static bool Is(FieldItem field, string[][] sets) =>
        sets.Any(set => new[] { field.Name, field.Prompt }.Any(text => set.All(word => text.Contains(word, StringComparison.OrdinalIgnoreCase))));

    /// <summary>歸檔的相對路徑：{壓合 - 之前}\{料號第一字}\{料號 - 前段}-{後段第一字}\{料號}_{批號}.txt；三個值缺一個就是空字串</summary>
    /// <remarks>InSpecTool 跑完歸檔與歸檔視窗都照這一條算。</remarks>
    /// <param name="part">料號</param>
    /// <param name="lot">批號</param>
    /// <param name="press">壓合</param>
    public static string Relative(string part, string lot, string press)
    {
        var folder = Folder(part, press);
        var l = Safe(lot);

        return folder.Length > 0 && l.Length > 0
            ? Path.Combine(folder, $"{Safe(part)}_{l}.txt")
            : string.Empty;
    }

    /// <summary>歸檔的資料夾：{壓合 - 之前}\{料號第一字}\{料號 - 前段}-{後段第一字}；料號或壓合缺一個就是空字串</summary>
    /// <param name="part">料號</param>
    /// <param name="press">壓合</param>
    public static string Folder(string part, string press)
    {
        var p = Safe(part);
        var s = Safe(press.Split('-')[0]);
        var dash = p.IndexOf('-');
        var group = dash > 0 && dash < p.Length - 1 ? $"{p[..dash]}-{p[dash + 1]}" : p;

        return p.Length > 0 && s.Length > 0 ? Path.Combine(s, p[..1], Safe(group)) : string.Empty;
    }

    /// <summary>InSpec 寫出的內容切成一次一筆：每一次前後各一行 :BEGIN、:END（自動匯出開了分隔）；沒有這兩行就整段當一筆</summary>
    /// <param name="content">InSpec 寫出的內容</param>
    public static List<string> Runs(string content)
    {
        var marked = Regex.Matches(content, @"^:BEGIN\r?\n(.*?)^:END\r?$", RegexOptions.Singleline | RegexOptions.Multiline)
            .Select(one => one.Groups[1].Value)
            .ToList();

        return marked.Count > 0 ? marked : [content];
    }

    /// <summary>檔名不能用的字換成 _</summary>
    /// <param name="value">原來的字</param>
    public static string Safe(string value) =>
        Path.GetInvalidFileNameChars().Aggregate(value.Trim(), (text, bad) => text.Replace(bad, '_'));

    /// <summary>認料號的字</summary>
    public static readonly string[][] PartWords = [["part", "number"], ["料號"]];

    /// <summary>認批號的字</summary>
    public static readonly string[][] LotWords = [["lot"], ["批號"]];

    /// <summary>認壓合的字</summary>
    public static readonly string[][] PressWords = [["press"], ["壓合"]];

    /// <summary>跟 InSpec 打交道用到的 Windows 呼叫</summary>
    private static class Native
    {
        public delegate void WinEventProc(IntPtr hook, uint kind, IntPtr hwnd, int idObject, int idChild, uint thread, uint time);

        private delegate bool EnumProc(IntPtr hwnd, IntPtr l);

        [DllImport("user32")] public static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr module, WinEventProc proc, uint pid, uint tid, uint flags);

        [DllImport("user32")] public static extern bool UnhookWinEvent(IntPtr hook);

        [DllImport("user32")] public static extern IntPtr GetDlgItem(IntPtr dialog, int id);

        [DllImport("user32", CharSet = CharSet.Unicode)] public static extern IntPtr SendMessage(IntPtr hwnd, uint msg, IntPtr w, string l);

        [DllImport("user32", CharSet = CharSet.Unicode)] private static extern IntPtr SendMessage(IntPtr hwnd, uint msg, IntPtr w, StringBuilder l);

        [DllImport("user32")] private static extern IntPtr SendMessage(IntPtr hwnd, uint msg, IntPtr w, IntPtr l);

        [DllImport("user32")] public static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr w, IntPtr l);

        [DllImport("user32", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hwnd, StringBuilder name, int max);

        [DllImport("user32")] private static extern bool EnumChildWindows(IntPtr parent, EnumProc proc, IntPtr l);

        /// <summary>視窗類別名稱</summary>
        public static string ClassOf(IntPtr hwnd)
        {
            var name = new StringBuilder(256);
            GetClassName(hwnd, name, 256);
            return name.ToString();
        }

        /// <summary>別的行程的控制項文字：用 WM_GETTEXT 讀</summary>
        public static string TextOf(IntPtr hwnd)
        {
            var length = (int)SendMessage(hwnd, 0x000E, IntPtr.Zero, IntPtr.Zero);
            var text = new StringBuilder(length + 1);
            SendMessage(hwnd, 0x000D, (IntPtr)(length + 1), text);
            return text.ToString();
        }

        /// <summary>一個對話框裡的每一個控制項</summary>
        public static List<(IntPtr Handle, string Class)> Children(IntPtr parent)
        {
            var found = new List<(IntPtr, string)>();
            EnumChildWindows(parent, (hwnd, _) => { found.Add((hwnd, ClassOf(hwnd))); return true; }, IntPtr.Zero);
            return found;
        }
    }

    #endregion

    #region 模型

    /// <summary>左邊的一個欄位；值改了格子自己更新，不必整排重畫</summary>
    public class FieldItem : INotifyPropertyChanged
    {
        private string value = string.Empty;

        /// <summary>值改了</summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>欄位名稱</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>對應的 InSpec 提示文字</summary>
        public string Prompt { get; set; } = string.Empty;

        /// <summary>fill（直接填）／confirm（先確認再填）</summary>
        public string Mode { get; set; } = "fill";

        /// <summary>主頁上不能改</summary>
        public bool Fixed { get; set; }

        /// <summary>滑鼠移上去的提示；沒有就是 null</summary>
        public string? Hint { get; set; }

        /// <summary>目前的值</summary>
        public string Value
        {
            get => value;
            set
            {
                if (this.value == value) return;
                this.value = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }
    }

    /// <summary>找到的一支量測程式</summary>
    public class ProgramItem
    {
        /// <summary>檔名</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>完整路徑</summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>修改時間</summary>
        public DateTime At { get; set; }

        /// <summary>修改時間，給人看的</summary>
        public string Changed { get; set; } = string.Empty;
    }

    /// <summary>一個提示怎麼處理，存進這次執行的 prompts</summary>
    public class PromptItem
    {
        /// <summary>InSpec 的提示文字</summary>
        public string prompt { get; set; } = string.Empty;

        /// <summary>填了什麼；空的就是沒填</summary>
        public string value { get; set; } = string.Empty;

        /// <summary>什麼時候</summary>
        public string at { get; set; } = string.Empty;
    }

    /// <summary>右邊那一塊的一列</summary>
    public class FactItem
    {
        /// <summary>這一列是什麼</summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>值</summary>
        public string Value { get; set; } = string.Empty;
    }

    #endregion
}
