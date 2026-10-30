using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using InSpecTool.Logic;
using InSpecTool.Store;
using Microsoft.Win32;

namespace InSpecTool.Service;

/// <summary>InSpecTool（Demo 版）的串接：查出來的列交給運算，運算的結果交回畫面或寫回去</summary>
/// <remarks>不碰畫面；一個使用者動作一支方法，步驟依序寫在裡面。InSpec 的檔案、行程與對話框也在這裡打交道，要問操作員的交給畫面給的那一支。</remarks>
/// <param name="store">這支工具唯一碰資料庫的型別</param>
public class InspecService(InspecStore store)
{
    /// <summary>搜尋資料夾裡的每一支量測程式：掃一次記起來，換資料夾或按重新查找才重掃</summary>
    private List<InspecLogic.ProgramItem> index = [];

    /// <summary>記起來的那一份是哪一個資料夾掃的</summary>
    private string indexRoot = string.Empty;

    #region 量測

    /// <summary>打開量測頁：每一項設定、左邊的每一個欄位、最近執行</summary>
    public InspecLogic.MeasureView LoadMeasure() =>
        InspecLogic.Measure(store.QueryMeasureItems());

    /// <summary>掃條碼、打條碼停手之後：每一個欄位的新值，含不允許的字元就擋下</summary>
    /// <param name="barcode">條碼框裡的字</param>
    /// <param name="fields">左邊的每一個欄位</param>
    public InspecLogic.ParsedView LoadParsed(string barcode, IReadOnlyList<InspecLogic.FieldItem> fields) =>
        InspecLogic.Parsed(barcode, fields);

    /// <summary>改了一個欄位：條碼跟著欄位的值</summary>
    /// <param name="fields">左邊的每一個欄位</param>
    public string LoadJoined(IReadOnlyList<InspecLogic.FieldItem> fields) =>
        InspecLogic.Joined(fields);

    /// <summary>掃條碼、改欄位、清除、重新查找之後找量測程式：檔名含 SETUP 與料號，新的在上</summary>
    /// <param name="setup">量測頁打開時讀的設定</param>
    /// <param name="fields">左邊的每一個欄位</param>
    /// <param name="again">按了重新查找：丟掉記起來的那一份重掃</param>
    public async Task<InspecLogic.ProgramsView> LoadPrograms(InspecLogic.MeasureView setup, IReadOnlyList<InspecLogic.FieldItem> fields, bool again)
    {
        // 料號那一欄現在的值與搜尋資料夾在不在
        var part = fields.FirstOrDefault(one => InspecLogic.Is(one, InspecLogic.PartWords))?.Value.Trim() ?? string.Empty;
        var root = setup.SearchFolder;
        var exists = Directory.Exists(root);

        // 有料號才掃；資料夾掃一次記起來，換資料夾或按重新查找才重掃；在背景掃，畫面不卡
        if (part.Length > 0 && exists && (again || !string.Equals(indexRoot, root, StringComparison.OrdinalIgnoreCase)))
        {
            index = await Task.Run(() => Directory.EnumerateFiles(root, "*.iwp", SearchOption.AllDirectories)
                .Select(one => new FileInfo(one))
                .Select(one => new InspecLogic.ProgramItem { Name = one.Name, Path = one.FullName, At = one.LastWriteTime, Changed = one.LastWriteTime.ToString("yyyy-MM-dd HH:mm") })
                .ToList());
            indexRoot = root;
        }

        // 符合的每一支，找不到照實說是哪一種
        return InspecLogic.Programs(index, fields, root, exists);
    }

    /// <summary>執行量測（雙擊量測程式、在清單上按 Enter）：記一列執行，開副本、自動填提示，InSpec 跑完記下結果（Demo 版不歸檔）</summary>
    /// <param name="setup">量測頁打開時讀的設定</param>
    /// <param name="program">選的那一支量測程式</param>
    /// <param name="fields">左邊的每一個欄位</param>
    /// <param name="confirm">「先確認再填」時問操作員：欄位名稱、提示文字、條碼帶出來的值 → 確認後的值</param>
    public async Task<InspecLogic.ResultView> LoadRun(InspecLogic.MeasureView setup, InspecLogic.ProgramItem program,
        IReadOnlyList<InspecLogic.FieldItem> fields, Func<string, string, string, string> confirm)
    {
        // 程式檔要在，而且不是空程式
        var present = File.Exists(program.Path) && new FileInfo(program.Path).Length > 2;

        // 開得了才記一列執行；開不了就把原因蓋在整頁上
        var started = InspecLogic.Started(fields, program, present, DateTime.UtcNow);
        if (started.Rows.Count == 0) return started.Refused;

        store.InsertInspecList(started.Rows);
        var id = started.Rows[0].id;
        var prompts = new List<InspecLogic.PromptItem>();
        var alerts = new List<string>();

        // 執行副本：自動匯出指到 InSpecTool 讀結果的那個檔、打開、Unicode、CRLF；原檔不動
        var folder = Path.Combine(Path.GetTempPath(), "InSpecTool");
        Directory.CreateDirectory(folder);
        var copy = Path.Combine(folder, $"run_{id}.iwp");
        var export = InspecLogic.InputFile;
        File.WriteAllText(export, string.Empty);
        File.WriteAllText(copy, InspecLogic.Prepared(File.ReadAllText(program.Path), export), Encoding.Unicode);

        // InSpec 裝在哪裡：讀它自己的安裝資訊，不寫死
        var home = new[] { RegistryView.Registry32, RegistryView.Registry64 }
            .Select(view => RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view).OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
            .Where(one => one is not null)
            .SelectMany(one => one!.GetSubKeyNames().Select(name => one.OpenSubKey(name)))
            .Where(one => $"{one?.GetValue("DisplayName")}".Contains("InSpec", StringComparison.OrdinalIgnoreCase))
            .Select(one => $"{one!.GetValue("InstallLocation")}")
            .FirstOrDefault(one => File.Exists(Path.Combine(one, "iscmd.exe"))) ?? string.Empty;

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
            var field = started.Snapshot.FirstOrDefault(one => string.Equals(one.Prompt.Trim(), ask, StringComparison.OrdinalIgnoreCase));
            if (field is null || field.Value.Length == 0)
            {
                prompts.Add(new InspecLogic.PromptItem { prompt = ask, value = string.Empty, at = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") });
                return;
            }

            // 「先確認再填」：先讓操作員看一次、可以改
            var value = field.Mode == "confirm" ? confirm(field.Name, ask, field.Value) : field.Value;

            // 打字型填輸入框 499、清單型選 1269、下拉型選 1297，有哪個用哪個；送標準確定（IDOK）
            Native.SendMessage(Native.GetDlgItem(hwnd, 499), 0x000C, IntPtr.Zero, value);
            Native.SendMessage(Native.GetDlgItem(hwnd, 1269), 0x01A2, (IntPtr)(-1), value);
            Native.SendMessage(Native.GetDlgItem(hwnd, 1297), 0x014D, (IntPtr)(-1), value);
            Native.PostMessage(hwnd, 0x0111, (IntPtr)1, Native.GetDlgItem(hwnd, 1));

            prompts.Add(new InspecLogic.PromptItem { prompt = ask, value = value, at = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") });
        };

        var hooks = new[] { 0x8002u, 0x0010u }
            .Select(kind => inspec is null ? IntPtr.Zero : Native.SetWinEventHook(kind, kind, IntPtr.Zero, seen, (uint)inspec.Id, 0, 0))
            .ToList();

        // iscmd /run 不加 /nowait：回來就是 InSpec 跑完；InSpec 在跑完之前就不見了（被關掉或當掉），iscmd 會一直等，這時結束它
        DateTime? finished = null;
        int? code = null;
        var gone = false;
        if (home.Length > 0 && inspec is not null)
        {
            var go = Process.Start(new ProcessStartInfo(Path.Combine(home, "iscmd.exe"), $"/run \"{copy}\"") { UseShellExecute = false, CreateNoWindow = true })!;
            gone = await Task.Run(() =>
            {
                while (!go.WaitForExit(500) && !inspec.HasExited) { }
                return !go.HasExited;
            });

            new[] { go }.Where(_ => gone).ToList().ForEach(one => one.Kill());
            code = gone ? null : go.ExitCode;
            finished = DateTime.UtcNow;
        }

        hooks.Where(one => one != IntPtr.Zero).ToList().ForEach(one => Native.UnhookWinEvent(one));
        GC.KeepAlive(seen);

        // 讀這次的匯出，讀完清空，下一筆不會疊上這一筆
        var exported = Read(export);
        File.WriteAllText(export, string.Empty);

        // 這一次的結果存起來：開跑時記的那一列從 view 整列帶過來；Demo 版不歸檔
        store.InsertInspecList(InspecLogic.Finished(store.QueryMeasureItem(id), id, home, finished, code, gone, exported, prompts, alerts, DateTime.UtcNow));

        // 這一次的結果與多了這一次的最近執行
        return InspecLogic.Result(store.QueryMeasureItems(), id, prompts, setup.ShowResult);
    }

    #endregion

    #region 設定

    /// <summary>打開設定：每一項設定與欄位結構</summary>
    public InspecLogic.SetupView LoadSetup() =>
        InspecLogic.Setup(store.QuerySetupItems());

    #endregion

    #region 基本 CRUD 操作

    /// <summary>改一項設定（選搜尋資料夾、改路徑、選解析間隔、勾跑完顯示結果、拖量測頁中間的分隔）：那一項寫成新的值</summary>
    /// <param name="key">哪一項</param>
    /// <param name="on">開關切成什麼；不是開關是 null</param>
    /// <param name="number">新的數字；不是數字是 null</param>
    /// <param name="path">新的路徑；不是路徑是 null，空字串是清掉</param>
    public void LoadChanged(string key, bool? on, decimal? number, string? path) =>
        store.InsertInspecList(InspecLogic.Changed(store.QuerySetupItem(key), key, on, number, path, DateTime.UtcNow));

    /// <summary>改了欄位結構（名稱、提示、填法、主頁可改、上移、下移、移除）：當場存；回傳存了幾個欄位</summary>
    /// <param name="rows">畫面上欄位結構的每一列</param>
    public int LoadSaved(IReadOnlyList<InspecLogic.RowItem> rows)
    {
        // 現在還在的每一個欄位對畫面上的每一列，算出要寫回去的
        var saved = InspecLogic.Saved(store.QuerySetupItems(), rows, DateTime.UtcNow);

        store.InsertInspecList(saved.Rows);

        // 新增的那幾列寫進去就有編號，記回畫面上那一列，下一次存就是改那一列
        saved.Kept.ForEach(one => one.Row.Id = one.Field.id);

        return saved.Kept.Count;
    }

    #endregion

    #region 對照

    /// <summary>讀一個可能正在被寫的檔：跟寫的人共用，不擋它寫</summary>
    /// <param name="path">那個檔</param>
    private static string Read(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, Encoding.Default, detectEncodingFromByteOrderMarks: true);

        return reader.ReadToEnd();
    }

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
}
