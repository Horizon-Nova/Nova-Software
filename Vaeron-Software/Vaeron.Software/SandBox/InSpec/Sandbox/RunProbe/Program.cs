// 沙盒測試：不吃版本的自動執行、自動輸入、跑完判斷
// 用法：RunProbe <量測程式.iwp> <提示文字=值> [<提示文字=值> ...]
// 1. 讀 .iwp 裡的提示清單
// 2. 複製一份，自動匯出指到這次專用的檔
// 3. 向 Windows 登記「InSpec 打開視窗時通知我」，看到提示對話框就依編號填值、送確定
// 4. iscmd /run（不加 /nowait）在背景跑，它回來的那一刻就是跑完
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

Console.OutputEncoding = Encoding.UTF8;
var started = Stopwatch.StartNew();
void Log(string text) => Console.WriteLine($"[{started.ElapsedMilliseconds,7} ms] {text}");

var install = @"C:\Program Files (x86)\Micro-Vu Corporation\InSpec for Windows";
var source = Path.GetFullPath(args[0]);
var values = args.Skip(1).Select(one => one.Split('=', 2)).ToDictionary(one => one[0], one => one[1]);

// 讀 .iwp 的提示清單
var text = File.ReadAllText(source, Encoding.Unicode);
var prompts = Regex.Matches(text, "^Prmt .*\\(Txt \"([^\"]*)\"\\)", RegexOptions.Multiline).Select(one => one.Groups[1].Value).ToList();
Log($"程式裡的提示 {prompts.Count} 個：{string.Join("、", prompts)}");

// 複製一份，自動匯出指到這次專用的檔
var folder = Path.Combine(Path.GetTempPath(), "RunProbe");
Directory.CreateDirectory(folder);
var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
var copy = Path.Combine(folder, $"run_{stamp}.iwp");
var export = Path.Combine(folder, $"export_{stamp}.txt");
var patched = text
    .Replace("(AutoExpFile \"\")", $"(AutoExpFile \"{export}\")")
    .Replace("(AutoExpFileInf ((Enab 0)", "(AutoExpFileInf ((Enab 1)");
File.WriteAllText(copy, patched, Encoding.Unicode);
Log($"執行副本 {copy}");
Log($"匯出檔   {export}");

// InSpec 沒開就開起來，等主視窗出現
var inspec = Process.GetProcessesByName("InSpec").FirstOrDefault();
if (inspec == null)
{
    inspec = Process.Start(new ProcessStartInfo(Path.Combine(install, "InSpec.exe")) { UseShellExecute = true, WorkingDirectory = install })!;
    Log($"啟動 InSpec，PID {inspec.Id}");
    while (Native.FindTop((uint)inspec.Id, "InSpecAppFrame") == IntPtr.Zero && started.ElapsedMilliseconds < 60000) Thread.Sleep(200);
    Thread.Sleep(3000);
}
Log($"InSpec PID {inspec.Id}，主視窗 0x{Native.FindTop((uint)inspec.Id, "InSpecAppFrame"):X}");

// 登記：InSpec 有視窗出現就通知（不輪詢、不看前景）
var seen = new ConcurrentDictionary<IntPtr, bool>();
var mainThread = Native.GetCurrentThreadId();
Native.WinEventProc onEvent = (hook, kind, hwnd, idObject, idChild, thread, time) =>
{
    if (idObject != 0 || hwnd == IntPtr.Zero) return;
    if (Native.ClassOf(hwnd) != "#32770") return;
    if (!seen.TryAdd(hwnd, true)) return;

    var title = Native.TextOf(hwnd);
    var children = Native.Children(hwnd);
    Log($"對話框出現 0x{hwnd:X}「{title}」事件 0x{kind:X} 控制項：{string.Join(" ", children.Select(one => $"{one.Id}:{one.Class}"))}");

    // 提示對話框：有提示文字 1278 就是
    var label = Native.GetDlgItem(hwnd, 1278);
    if (label == IntPtr.Zero) return;
    var ask = Native.TextOf(label);
    var value = values.Where(one => ask.Contains(one.Key, StringComparison.OrdinalIgnoreCase)).Select(one => one.Value).FirstOrDefault();
    Log($"  提示文字（1278）「{ask}」→ 要填「{value}」");
    if (value == null) return;

    // 打字輸入 499、清單 1269、下拉 1297：有哪個就用哪個
    var edit = Native.GetDlgItem(hwnd, 499);
    var list = Native.GetDlgItem(hwnd, 1269);
    var combo = Native.GetDlgItem(hwnd, 1297);
    if (edit != IntPtr.Zero) Native.SendMessage(edit, 0x000C, IntPtr.Zero, value);
    if (list != IntPtr.Zero) Native.SendMessage(list, 0x01A2 /* LB_SELECTSTRING */, (IntPtr)(-1), value);
    if (combo != IntPtr.Zero) Native.SendMessage(combo, 0x014D /* CB_SELECTSTRING */, (IntPtr)(-1), value);
    Log($"  已填：輸入框 {edit != IntPtr.Zero}、清單 {list != IntPtr.Zero}、下拉 {combo != IntPtr.Zero}");

    // 送標準確定（IDOK = 1），不看按鈕文字
    Native.PostMessage(hwnd, 0x0111, (IntPtr)1, Native.GetDlgItem(hwnd, 1));
    Log("  已送確定（IDOK）");
};
var hookShow = Native.SetWinEventHook(0x8002, 0x8002, IntPtr.Zero, onEvent, (uint)inspec.Id, 0, 0);
var hookDialog = Native.SetWinEventHook(0x0010, 0x0010, IntPtr.Zero, onEvent, (uint)inspec.Id, 0, 0);
Log($"已登記視窗通知 {hookShow != IntPtr.Zero && hookDialog != IntPtr.Zero}");

// iscmd /run 不加 /nowait：回來就是跑完
var runner = new Thread(() =>
{
    var go = Process.Start(new ProcessStartInfo(Path.Combine(install, "iscmd.exe"), $"/run \"{copy}\"") { UseShellExecute = false, CreateNoWindow = true })!;
    Log($"iscmd /run 送出，PID {go.Id}");
    var finished = go.WaitForExit(600000);
    Log($"iscmd 回來：結束 {finished}，代碼 {(finished ? go.ExitCode : -1)}");
    Log($"匯出檔：存在 {File.Exists(export)}，大小 {(File.Exists(export) ? new FileInfo(export).Length : -1)} bytes");
    if (File.Exists(export)) Log($"匯出內容：{Environment.NewLine}{File.ReadAllText(export)}");
    Thread.Sleep(1500);
    Native.PostThreadMessage(mainThread, 0x0012, IntPtr.Zero, IntPtr.Zero);
});
runner.Start();

// 視窗通知要有訊息迴圈才收得到
while (Native.GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0) { Native.TranslateMessage(ref msg); Native.DispatchMessage(ref msg); }
Native.UnhookWinEvent(hookShow);
Native.UnhookWinEvent(hookDialog);
GC.KeepAlive(onEvent);
Log("結束");

static class Native
{
    public delegate void WinEventProc(IntPtr hook, uint kind, IntPtr hwnd, int idObject, int idChild, uint thread, uint time);
    delegate bool EnumProc(IntPtr hwnd, IntPtr l);
    [StructLayout(LayoutKind.Sequential)] public struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam; public IntPtr lParam; public uint time; public int x; public int y; }

    [DllImport("user32")] public static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr module, WinEventProc proc, uint pid, uint tid, uint flags);
    [DllImport("user32")] public static extern bool UnhookWinEvent(IntPtr hook);
    [DllImport("user32")] public static extern int GetMessage(out MSG msg, IntPtr hwnd, uint min, uint max);
    [DllImport("user32")] public static extern bool TranslateMessage(ref MSG msg);
    [DllImport("user32")] public static extern IntPtr DispatchMessage(ref MSG msg);
    [DllImport("user32")] public static extern bool PostThreadMessage(uint tid, uint msg, IntPtr w, IntPtr l);
    [DllImport("kernel32")] public static extern uint GetCurrentThreadId();
    [DllImport("user32")] public static extern IntPtr GetDlgItem(IntPtr dlg, int id);
    [DllImport("user32")] static extern int GetDlgCtrlID(IntPtr hwnd);
    [DllImport("user32", CharSet = CharSet.Unicode)] public static extern IntPtr SendMessage(IntPtr hwnd, uint msg, IntPtr w, string l);
    [DllImport("user32", CharSet = CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr hwnd, uint msg, IntPtr w, StringBuilder l);
    [DllImport("user32")] static extern IntPtr SendMessage(IntPtr hwnd, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32")] public static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr hwnd, StringBuilder name, int max);
    [DllImport("user32")] static extern bool EnumWindows(EnumProc proc, IntPtr l);
    [DllImport("user32")] static extern bool EnumChildWindows(IntPtr parent, EnumProc proc, IntPtr l);
    [DllImport("user32")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);

    public static string ClassOf(IntPtr hwnd) { var name = new StringBuilder(256); GetClassName(hwnd, name, 256); return name.ToString(); }

    // WM_GETTEXT 跨行程讀得到控制項文字（GetWindowText 對別的行程的控制項讀不到）
    public static string TextOf(IntPtr hwnd)
    {
        var length = (int)SendMessage(hwnd, 0x000E, IntPtr.Zero, IntPtr.Zero);
        var text = new StringBuilder(length + 1);
        SendMessage(hwnd, 0x000D, (IntPtr)(length + 1), text);
        return text.ToString();
    }

    public static List<(int Id, string Class)> Children(IntPtr parent)
    {
        var found = new List<(int, string)>();
        EnumChildWindows(parent, (hwnd, _) => { found.Add((GetDlgCtrlID(hwnd), ClassOf(hwnd))); return true; }, IntPtr.Zero);
        return found;
    }

    public static IntPtr FindTop(uint pid, string cls)
    {
        var hit = IntPtr.Zero;
        EnumWindows((hwnd, _) =>
        {
            GetWindowThreadProcessId(hwnd, out var owner);
            if (owner == pid && ClassOf(hwnd) == cls) { hit = hwnd; return false; }
            return true;
        }, IntPtr.Zero);
        return hit;
    }
}
