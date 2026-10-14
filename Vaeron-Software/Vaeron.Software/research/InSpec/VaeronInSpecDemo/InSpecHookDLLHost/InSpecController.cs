using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace InSpecHookDLLHost;

/// <summary>
/// Drives InSpec.exe by injecting InSpecHookDLL.dll and invoking its exported functions
/// via CreateRemoteThread. Ports the PowerShell injector scripts used to verify this
/// technique live against InSpec into a reusable C# form — same Win32 calls, same order.
/// </summary>
public sealed class InSpecController
{
    private const string ProcessName = "InSpec";
    private const string DllFileName = "InSpecHookDLL.dll";
    private const string CaptureFilePath = @"C:\InSpecHookDLL_paint.txt";

    // 原廠官方外部控制程式（手冊附錄 B）。iscmd 是本機命名事件 client，要求 InSpec.exe 在跑。
    public const string InSpecExeDefaultPath = @"C:\Program Files (x86)\Micro-Vu Corporation\InSpec for Windows\InSpec.exe";
    private static readonly string IscmdPath = Path.Combine(
        Path.GetDirectoryName(InSpecExeDefaultPath)!, "iscmd.exe");

    private const uint PROCESS_ALL_ACCESS = 0x1F0FFF;
    private const uint MEM_COMMIT = 0x1000;
    private const uint MEM_RELEASE = 0x8000;
    private const uint PAGE_READWRITE = 0x04;
    private const uint INFINITE = 0xFFFFFFFF;

    public int? ProcessId { get; private set; }
    public bool IsInjected { get; private set; }

    // InSpec 檔案選單「開啟舊檔(&O)... Ctrl+O」的真實 command id，2026-07-29 用 DumpMainMenu
    // 診斷匯出實機查到，不是猜測值。開啟舊檔對話框的檔名 Edit 控制項 id=1148、開啟鈕 id=1，
    // 兩者是標準共用對話框範本的固定 id（即使視覺上是現代 Explorer 風格），同一輪查到。
    private const uint WM_COMMAND = 0x0111;
    private const uint WM_SETTEXT = 0x000C;
    private const uint BM_CLICK = 0x00F5;
    private const int IDC_FILE_OPEN_COMMAND = 57601;
    private const int OpenDialogFilenameEditId = 1148;
    private const int OpenDialogOpenButtonId = 1;

    /// <summary>
    /// Opens filePath in the already-running InSpec instance via its own File→Open menu
    /// command and the resulting dialog's filename field — pure cross-process window
    /// messaging (FindWindow/PostMessage/SendMessage), no code injection needed for this.
    /// </summary>
    public async Task<bool> OpenFileInInSpecAsync(string filePath)
    {
        var mainHwnd = NativeMethods.FindWindow("InSpecAppFrame", null);
        if (mainHwnd == IntPtr.Zero) return false;

        // WM_COMMAND 觸發的處理常式會在 InSpec 自己的執行緒內跑出一個 modal 對話框迴圈，
        // 用 PostMessage（非同步）送出，不要用 SendMessage 等它「處理完」——那要等到
        // 使用者關閉對話框才會回來，會把呼叫方卡住。
        NativeMethods.PostMessage(mainHwnd, WM_COMMAND, new IntPtr(IDC_FILE_OPEN_COMMAND), IntPtr.Zero);

        IntPtr dialogHwnd = IntPtr.Zero;
        for (var i = 0; i < 20; i++)
        {
            dialogHwnd = NativeMethods.FindWindow(null, "開啟舊檔");
            if (dialogHwnd != IntPtr.Zero) break;
            await Task.Delay(150);
        }
        if (dialogHwnd == IntPtr.Zero) return false;

        var editHwnd = NativeMethods.GetDlgItem(dialogHwnd, OpenDialogFilenameEditId);
        var openButtonHwnd = NativeMethods.GetDlgItem(dialogHwnd, OpenDialogOpenButtonId);
        if (editHwnd == IntPtr.Zero || openButtonHwnd == IntPtr.Zero) return false;

        NativeMethods.SendMessageText(editHwnd, WM_SETTEXT, IntPtr.Zero, filePath);
        NativeMethods.SendMessage(openButtonHwnd, BM_CLICK, IntPtr.Zero, IntPtr.Zero);
        return true;
    }

    /// <summary>Finds InSpec.exe and injects the DLL if not already loaded. Returns false if InSpec isn't running.</summary>
    public bool ScanAndAttach()
    {
        var proc = Process.GetProcessesByName(ProcessName).FirstOrDefault();
        if (proc is null)
        {
            ProcessId = null;
            IsInjected = false;
            return false;
        }
        return AttachToProcess(proc.Id);
    }

    /// <summary>
    /// Starts InSpec.exe with no window from the moment the process is created — not
    /// show-then-hide. First attempt used UseShellExecute=false + CreateNoWindow=true, which
    /// live-tested FAILED (InSpec's window still appeared) — .NET's ProcessStartInfo.WindowStyle
    /// is documented to only take effect when UseShellExecute=true; with it false, WindowStyle
    /// is silently ignored and the process launches with its normal default show state. Fixed
    /// by using UseShellExecute=true instead (routes through ShellExecuteEx, which does pass
    /// nShowCmd=SW_HIDE through to InSpec's CWinApp::m_nCmdShow), verified live below.
    /// </summary>
    public bool LaunchInSpecHidden(string exePath)
    {
        if (!File.Exists(exePath)) { AppLog.Result("LaunchInSpecHidden", false, $"exe 不存在：{exePath}"); return false; }

        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        var proc = Process.Start(psi);
        if (proc is null) { AppLog.Result("LaunchInSpecHidden", false, "Process.Start 回傳 null"); return false; }
        AppLog.Info("LaunchInSpecHidden", $"已啟動 PID {proc.Id}（UseShellExecute+Hidden），等待初始化…");

        // 給 InSpec 自己的初始化流程一點時間把主視窗建出來（雖然不可見），
        // 太早找 InSpecAppFrame／注入會撲空。
        Thread.Sleep(1500);
        var ok = AttachToProcess(proc.Id);
        AppLog.Result("LaunchInSpecHidden", ok, $"注入={IsInjected}, 啟動後 IsWindowVisible={IsMainWindowVisible()}");
        return ok;
    }

    /// <summary>
    /// 給 InSpecAutoLaunch 用：正常可見啟動 InSpec（不是 LaunchInSpecHidden 的隱藏變體），
    /// 啟動後立刻注入 DLL——這是「InSpec 啟動時就已改造，不依賴我方展示軟體」需求的落地點：
    /// 使用者以後啟動 InSpec 走的是這個 exe，不是直接點 InSpec.exe 本身。
    /// </summary>
    public bool LaunchInSpecVisible(string exePath)
    {
        if (!File.Exists(exePath)) { AppLog.Result("LaunchInSpecVisible", false, $"exe 不存在：{exePath}"); return false; }

        var psi = new ProcessStartInfo { FileName = exePath, UseShellExecute = true };
        var proc = Process.Start(psi);
        if (proc is null) { AppLog.Result("LaunchInSpecVisible", false, "Process.Start 回傳 null"); return false; }
        AppLog.Info("LaunchInSpecVisible", $"已啟動 PID {proc.Id}，等待初始化…");

        Thread.Sleep(1500);
        var ok = AttachToProcess(proc.Id);
        AppLog.Result("LaunchInSpecVisible", ok, $"注入={IsInjected}");
        return ok;
    }

    private bool AttachToProcess(int pid)
    {
        ProcessId = pid;

        using var hProc = NativeMethods.OpenProcess(PROCESS_ALL_ACCESS, false, pid);
        if (hProc.IsInvalid) { AppLog.Result("AttachToProcess", false, $"PID {pid} OpenProcess 失敗"); return false; }

        var remoteBase = FindRemoteModuleBase(hProc, DllFileName);
        if (remoteBase != IntPtr.Zero)
        {
            IsInjected = true;
            AppLog.Result("AttachToProcess", true, $"PID {pid} DLL 已在（不重複注入）");
            return true;
        }

        var dllPath = Path.Combine(AppContext.BaseDirectory, DllFileName);
        if (!File.Exists(dllPath)) { AppLog.Result("AttachToProcess", false, $"DLL 不存在：{dllPath}"); return false; }

        if (!InjectDll(hProc, dllPath)) { AppLog.Result("AttachToProcess", false, $"PID {pid} 注入失敗"); return false; }

        // Give LoadLibraryW a moment to finish before the module shows up in enumeration.
        Thread.Sleep(300);
        IsInjected = FindRemoteModuleBase(hProc, DllFileName) != IntPtr.Zero;
        AppLog.Result("AttachToProcess", IsInjected, $"PID {pid} 注入{(IsInjected ? "成功" : "後找不到模組")}");
        return IsInjected;
    }

    /// <summary>Adds a real button to InSpec's own toolbar; clicking it opens InSpec's own popup
    /// (created inside InSpec's process) that writes straight into InSpec's live prompt flow —
    /// no external app involved for that action, demonstrating direct front-end injection.</summary>
    public bool EnableLiveEditButton() => CallExport("ThreadWrapper_EnableLiveEditButton", IntPtr.Zero);

    public bool DisableLiveEditButton() => CallExport("ThreadWrapper_DisableLiveEditButton", IntPtr.Zero);

    /// <summary>啟用摘要打勾覆蓋層（既有功能，此前從未接進本 UI）。覆蓋層是 InSpec 行程內的真實
    /// 子視窗，蓋在其摘要面板上——這是「前端注入」能力的展示核心，樣式可用下列 SetOverlay* 切換。</summary>
    public bool EnableOverlay() => CallExport("ThreadWrapper_EnableSummaryPatch", IntPtr.Zero);

    public bool DisableOverlay() => CallExport("ThreadWrapper_DisableSummaryPatch", IntPtr.Zero);

    public enum OverlaySymbol { Check = 0, Cross = 1, CustomText = 2 }

    public bool SetOverlaySymbol(OverlaySymbol mode) => CallExport("ThreadWrapper_SetOverlaySymbolMode", (IntPtr)(int)mode);

    public bool SetOverlayColor(byte r, byte g, byte b) => CallExport("ThreadWrapper_SetOverlayColor", (IntPtr)(r | (g << 8) | (b << 16)));

    public bool SetOverlayCustomText(string text) => CallExportWithString("ThreadWrapper_SetOverlayCustomText", text);

    /// <summary>X/Y/Z 座標就地編輯（真實 Static 控制項 id=19000/19001/19002，非 GDI 猜測座標）。
    /// 摘要面板本身不可編輯，這裡才是使用者要求的「可編輯數值」目標。</summary>
    public bool EnableXyzEdit() => CallExport("ThreadWrapper_EnableXyzEdit", IntPtr.Zero);

    public bool DisableXyzEdit() => CallExport("ThreadWrapper_DisableXyzEdit", IntPtr.Zero);

    /// <summary>在 InSpec 工具列注入「重新載入」按鈕；點下後蒙住整個 InSpec 版面播放載入動畫（≥2 秒），
    /// 同時觸發真實的 InSpec「元素→重新量測」動作。</summary>
    public bool EnableReloadButton() => CallExport("ThreadWrapper_EnableReloadButton", IntPtr.Zero);

    public bool DisableReloadButton() => CallExport("ThreadWrapper_DisableReloadButton", IntPtr.Zero);

    /// <summary>直接觸發重新載入蒙版＋真實重新量測，不需要先點 InSpec 工具列上的按鈕。</summary>
    public bool TriggerReload() => CallExport("ThreadWrapper_TriggerReload", IntPtr.Zero);

    // ============================================================
    // 無畫面正解（2026-07-29 架構轉向，見 blueprint §5-B）：
    // 驅動走原廠 iscmd /run，讀值走 InSpec 自身 AutoExport 匯出檔，兩條都不經螢幕。
    // ============================================================

    public string LastExportPath { get; private set; } = "";

    /// <summary>官方 iscmd /run 驅動 InSpec 開檔＋執行（已實機驗證）。noWait=true 加 /nowait 非同步。</summary>
    public Task<bool> RunViaIscmdAsync(string iwpPath, bool noWait)
        => noWait
            ? RunIscmdAsync("/run", iwpPath, "/nowait")
            : RunIscmdAsync("/run", iwpPath);

    /// <summary>官方 iscmd /halt 停止執行中的程式。</summary>
    public Task<bool> HaltViaIscmdAsync() => RunIscmdAsync("/halt");

    private static async Task<bool> RunIscmdAsync(params string[] args)
    {
        var argline = string.Join(" ", args);
        if (!File.Exists(IscmdPath))
        {
            AppLog.Result($"iscmd {argline}", false, $"iscmd.exe 不存在：{IscmdPath}");
            return false;
        }
        var psi = new ProcessStartInfo
        {
            FileName = IscmdPath,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        // ArgumentList 每個元素就是一個獨立 argv，.NET 自行處理含空格/特殊字元的引號，不手動加引號。
        foreach (var a in args) psi.ArgumentList.Add(a);
        try
        {
            var proc = Process.Start(psi);
            if (proc is null) { AppLog.Result($"iscmd {argline}", false, "Process.Start 回傳 null"); return false; }
            await proc.WaitForExitAsync();
            var ok = proc.ExitCode == 0; // 非零＝InSpec 沒在跑或參數不認得（手冊附錄 B）
            AppLog.Result($"iscmd {argline}", ok, $"exit code={proc.ExitCode}");
            return ok;
        }
        catch (Exception ex)
        {
            AppLog.Error($"iscmd {argline}", ex);
            return false;
        }
    }

    /// <summary>
    /// 準備一份啟用 AutoExport 的 .iwp 工作副本（不動客戶原檔），指向一個乾淨的匯出檔路徑。
    /// 回傳 (工作副本路徑, 匯出檔路徑)。.iwp 是 UTF-16LE 括號式腳本，AutoExport 設定就在 Prog 標頭：
    /// (AutoExpFile "路徑") 與 (AutoExpFileInf ((Enab 0/1) ...))，把 Enab 0→1、填入路徑即可。
    /// </summary>
    public (string workIwp, string exportTxt) PrepareAutoExportCopy(string sourceIwp)
    {
        var workDir = Path.Combine(Path.GetTempPath(), "InSpecHost");
        Directory.CreateDirectory(workDir);
        var workIwp = Path.Combine(workDir, "run_" + Path.GetFileNameWithoutExtension(sourceIwp) + ".iwp");
        var exportTxt = Path.Combine(workDir, "export_" + Path.GetFileNameWithoutExtension(sourceIwp) + ".txt");

        var text = File.ReadAllText(sourceIwp, Encoding.Unicode);
        // 匯出路徑在 .iwp 內用反斜線，需跳脫成雙反斜線以免括號式腳本誤解（比照 InitialDir 寫法）
        var iwpPathForFile = exportTxt.Replace("\\", "\\\\");
        var hadFileField = text.Contains("(AutoExpFile \"\")");
        var hadEnab0 = text.Contains("(AutoExpFileInf ((Enab 0)");
        text = ReplaceFirst(text, "(AutoExpFile \"\")", $"(AutoExpFile \"{iwpPathForFile}\")");
        text = ReplaceFirst(text, "(AutoExpFileInf ((Enab 0)", "(AutoExpFileInf ((Enab 1)");
        File.WriteAllText(workIwp, text, Encoding.Unicode);

        // 乾淨起點：AutoExport 是 append 模式，先刪舊檔避免讀到上一輪殘留
        if (File.Exists(exportTxt)) File.Delete(exportTxt);
        LastExportPath = exportTxt;
        AppLog.Info("PrepareAutoExportCopy",
            $"src={Path.GetFileName(sourceIwp)}; 啟用AutoExport(檔路徑欄位存在={hadFileField}, 原Enab0={hadEnab0}); " +
            $"work={workIwp}; export={exportTxt}");
        if (!hadEnab0)
            AppLog.Warn("PrepareAutoExportCopy", "來源 .iwp 找不到 (AutoExpFileInf ((Enab 0)——可能已是 Enab 1 或格式不同，未改到");
        return (workIwp, exportTxt);
    }

    private static string ReplaceFirst(string haystack, string oldValue, string newValue)
    {
        var i = haystack.IndexOf(oldValue, StringComparison.Ordinal);
        return i < 0 ? haystack : haystack.Substring(0, i) + newValue + haystack.Substring(i + oldValue.Length);
    }

    /// <summary>讀 AutoExport 匯出檔（Tab 分隔文字），回傳每一行。檔案不存在或空回傳空清單。</summary>
    public IReadOnlyList<string> ReadExportFile(string exportTxt)
    {
        try
        {
            if (!File.Exists(exportTxt))
            {
                AppLog.Warn("ReadExportFile", $"檔案不存在：{exportTxt}");
                return Array.Empty<string>();
            }
            var bytes = new FileInfo(exportTxt).Length;
            // InSpec 匯出可能是 ANSI 或 UTF-16（依 .iwp 的 Unicode 旗標），先試 UTF-8/預設，退回 Unicode
            var raw = File.ReadAllText(exportTxt);
            if (raw.Contains('\0')) raw = File.ReadAllText(exportTxt, Encoding.Unicode);
            var lines = raw.Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0).ToList();
            AppLog.Info("ReadExportFile", $"{Path.GetFileName(exportTxt)}: {bytes} bytes, {lines.Count} 行");
            return lines;
        }
        catch (IOException ex) { AppLog.Error("ReadExportFile", ex); return Array.Empty<string>(); }
    }

    /// <summary>Toolbar "execute" button (id 1165, index 0) — starts the current prompt/measurement flow.</summary>
    public bool TriggerExecute() => CallExport("ThreadWrapper_ClickToolbar1165Btn0", IntPtr.Zero);

    /// <summary>Fills the open "輸入提示" dialog with value and clicks 確定.</summary>
    public bool FillAndConfirm(string value)
    {
        var ok = CallExportWithString("ThreadWrapper_AutoFillAndConfirmPrompt", value);
        AppLog.Result("FillAndConfirm", ok, $"value=\"{value}\"");
        return ok;
    }

    public bool HideMainWindow()
    {
        var ok = CallExport("ThreadWrapper_HideMainWindow", IntPtr.Zero);
        AppLog.Result("HideMainWindow", ok, $"呼叫後 IsWindowVisible={IsMainWindowVisible()}");
        return ok;
    }

    public bool ShowMainWindow()
    {
        var ok = CallExport("ThreadWrapper_ShowMainWindow", IntPtr.Zero);
        AppLog.Result("ShowMainWindow", ok, $"呼叫後 IsWindowVisible={IsMainWindowVisible()}");
        return ok;
    }

    /// <summary>直接探測 InSpecAppFrame 主視窗可見性（Win32 IsWindowVisible）。null=視窗不存在。</summary>
    public bool? IsMainWindowVisible()
    {
        var h = NativeMethods.FindWindow("InSpecAppFrame", null);
        return h == IntPtr.Zero ? null : NativeMethods.IsWindowVisible(h);
    }

    /// <summary>
    /// Triggers a forced repaint + GDI TextOut capture of the summary grid, then reads back
    /// the resulting log file. Only works while InSpec's main window is visible — a hidden
    /// window never receives WM_PAINT, so the capture call succeeds but yields nothing new.
    /// </summary>
    public bool CaptureSummary(out IReadOnlyList<SummaryLine> lines)
    {
        lines = Array.Empty<SummaryLine>();
        if (!CallExport("CaptureGxwndPaint", IntPtr.Zero)) return false;

        string text;
        try
        {
            // DLL 端用 _wfopen_s(..., L"w, ccs=UTF-8") 寫檔，實際落地編碼是 UTF-8（含 BOM），
            // 不是 UTF-16——先前這裡誤用 Encoding.Unicode 讀取，讀出來的內容全部無法比對成功。
            text = File.ReadAllText(CaptureFilePath, Encoding.UTF8);
        }
        catch (IOException)
        {
            return false;
        }

        lines = ParseCaptureLog(text);
        return true;
    }

    private static IReadOnlyList<SummaryLine> ParseCaptureLog(string text)
    {
        // 真實 log 格式（實機確認，見 C:\InSpecHookDLL_paint.txt）：
        // "TextOutW hdc=0x7E0118C5 (50,1) len=3 "提示:"" —— 函式名後面還有 hdc=0x... 這段，
        // 先前的正規表示式沒有把這段算進去，導致從未比對成功。這裡用 ^TextOutW 錨定行首，
        // 順便排除 "ExtTextOutW"（同一份繪製內容會重複觸發、且內容被拆成片段，不用它）。
        var results = new List<SummaryLine>();
        foreach (var rawLine in text.Split('\n'))
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                rawLine, @"^TextOutW.*?\(50,(\d+)\).*?""(.*)""");
            if (!match.Success) continue;
            results.Add(new SummaryLine(int.Parse(match.Groups[1].Value), match.Groups[2].Value));
        }
        results.Sort((a, b) => a.Y.CompareTo(b.Y));
        return results;
    }

    private bool CallExport(string exportName, IntPtr lpParameter)
    {
        if (ProcessId is not int pid) return false;
        using var hProc = NativeMethods.OpenProcess(PROCESS_ALL_ACCESS, false, pid);
        if (hProc.IsInvalid) return false;

        var funcAddr = ResolveRemoteFunctionAddress(hProc, exportName);
        if (funcAddr == IntPtr.Zero) return false;

        return RunRemoteThread(hProc, funcAddr, lpParameter, out _);
    }

    private bool CallExportWithString(string exportName, string value)
    {
        if (ProcessId is not int pid) return false;
        using var hProc = NativeMethods.OpenProcess(PROCESS_ALL_ACCESS, false, pid);
        if (hProc.IsInvalid) return false;

        var funcAddr = ResolveRemoteFunctionAddress(hProc, exportName);
        if (funcAddr == IntPtr.Zero) return false;

        var bytes = Encoding.Unicode.GetBytes(value + "\0");
        var remoteMem = NativeMethods.VirtualAllocEx(hProc, IntPtr.Zero, (uint)bytes.Length, MEM_COMMIT, PAGE_READWRITE);
        if (remoteMem == IntPtr.Zero) return false;
        try
        {
            if (!NativeMethods.WriteProcessMemory(hProc, remoteMem, bytes, (uint)bytes.Length, out _)) return false;
            return RunRemoteThread(hProc, funcAddr, remoteMem, out _);
        }
        finally
        {
            NativeMethods.VirtualFreeEx(hProc, remoteMem, 0, MEM_RELEASE);
        }
    }

    private static bool RunRemoteThread(SafeProcessHandle hProc, IntPtr funcAddr, IntPtr lpParameter, out uint exitCode)
    {
        exitCode = 0;
        using var hThread = NativeMethods.CreateRemoteThread(hProc, IntPtr.Zero, 0, funcAddr, lpParameter, 0, out _);
        if (hThread.IsInvalid) return false;
        NativeMethods.WaitForSingleObject(hThread, 5000);
        NativeMethods.GetExitCodeThread(hThread, out exitCode);
        return exitCode == 1;
    }

    private static bool InjectDll(SafeProcessHandle hProc, string dllPath)
    {
        var bytes = Encoding.Unicode.GetBytes(dllPath + "\0");
        var remoteMem = NativeMethods.VirtualAllocEx(hProc, IntPtr.Zero, (uint)bytes.Length, MEM_COMMIT, PAGE_READWRITE);
        if (remoteMem == IntPtr.Zero) return false;

        if (!NativeMethods.WriteProcessMemory(hProc, remoteMem, bytes, (uint)bytes.Length, out _)) return false;

        var kernel32 = NativeMethods.GetModuleHandle("kernel32.dll");
        var loadLibraryAddr = NativeMethods.GetProcAddress(kernel32, "LoadLibraryW");
        if (loadLibraryAddr == IntPtr.Zero) return false;

        using var hThread = NativeMethods.CreateRemoteThread(hProc, IntPtr.Zero, 0, loadLibraryAddr, remoteMem, 0, out _);
        if (hThread.IsInvalid) return false;
        NativeMethods.WaitForSingleObject(hThread, 5000);
        return true;
    }

    /// <summary>
    /// Resolves an exported function's address inside the target process WITHOUT hardcoding
    /// an RVA (RVAs shift on every rebuild of the DLL). Loads the same DLL locally purely to
    /// read its export table via GetProcAddress, computes the RVA from that local load, then
    /// adds it to the remote module's base address found in the target process.
    /// </summary>
    private static IntPtr ResolveRemoteFunctionAddress(SafeProcessHandle hProc, string exportName)
    {
        var remoteBase = FindRemoteModuleBase(hProc, DllFileName);
        if (remoteBase == IntPtr.Zero) return IntPtr.Zero;

        var dllPath = Path.Combine(AppContext.BaseDirectory, DllFileName);
        var localHandle = NativeMethods.LoadLibrary(dllPath);
        if (localHandle == IntPtr.Zero) return IntPtr.Zero;
        try
        {
            var localFuncAddr = NativeMethods.GetProcAddress(localHandle, exportName);
            if (localFuncAddr == IntPtr.Zero) return IntPtr.Zero;

            var rva = localFuncAddr.ToInt64() - localHandle.ToInt64();
            return new IntPtr(remoteBase.ToInt64() + rva);
        }
        finally
        {
            NativeMethods.FreeLibrary(localHandle);
        }
    }

    private static IntPtr FindRemoteModuleBase(SafeProcessHandle hProc, string moduleFileName)
    {
        var modules = new IntPtr[1024];
        if (!NativeMethods.EnumProcessModulesEx(hProc, modules, (uint)(modules.Length * IntPtr.Size), out var needed, 3))
            return IntPtr.Zero;

        var count = (int)(needed / IntPtr.Size);
        var sb = new StringBuilder(260);
        for (var i = 0; i < count; i++)
        {
            sb.Clear();
            NativeMethods.GetModuleFileNameEx(hProc, modules[i], sb, 260);
            if (sb.ToString().EndsWith(moduleFileName, StringComparison.OrdinalIgnoreCase))
                return modules[i];
        }
        return IntPtr.Zero;
    }
}

public readonly record struct SummaryLine(int Y, string Text);
