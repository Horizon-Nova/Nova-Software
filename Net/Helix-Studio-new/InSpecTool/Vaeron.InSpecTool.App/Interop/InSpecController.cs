using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Vaeron.Kernel;

namespace Vaeron.InSpecTool.Interop;

/// <summary>準備好的 AutoExport 工作副本路徑。</summary>
public sealed record AutoExportPaths(string WorkIwp, string ExportTxt);

/// <summary>驅動 iscmd /run 的請求。</summary>
public sealed record RunMeasurementRequest(string WorkIwpPath, bool NoWait);

/// <summary>驅動 InSpec.exe：注入 InSpecHookDLL.dll 並透過 CreateRemoteThread 呼叫其匯出函式。
/// 本次只搬「核心量測流程」需要的部分——掃描/連接、透過官方 iscmd + AutoExport 無畫面執行、
/// 顯示/隱藏主視窗；GDI 即時讀畫面、InSpec 前端樣式注入開關、legacy 工具列點擊流程不搬。</summary>
public sealed class InSpecController
{
    private const string ProcessName = "InSpec";
    private const string DllFileName = "InSpecHookDLL.dll";

    /// <summary>原廠官方外部控制程式（手冊附錄 B）。iscmd 是本機命名事件 client，要求 InSpec.exe 在跑。</summary>
    public const string InSpecExeDefaultPath = @"C:\Program Files (x86)\Micro-Vu Corporation\InSpec for Windows\InSpec.exe";
    private static readonly string IscmdPath = Path.Combine(
        Path.GetDirectoryName(InSpecExeDefaultPath)!, "iscmd.exe");

    private const uint PROCESS_ALL_ACCESS = 0x1F0FFF;
    private const uint MEM_COMMIT = 0x1000;
    private const uint PAGE_READWRITE = 0x04;

    public int? ProcessId { get; private set; }
    public bool IsInjected { get; private set; }

    /// <summary>尋找執行中的 InSpec.exe 並在尚未注入時注入。找不到行程回傳 false。</summary>
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

    /// <summary>正常可見啟動 InSpec，啟動後立刻注入 DLL。</summary>
    public bool LaunchInSpecVisible(string exePath)
    {
        if (!File.Exists(exePath)) { AppLog.Fail($"LaunchInSpecVisible | exe 不存在：{exePath}"); return false; }

        var psi = new ProcessStartInfo { FileName = exePath, UseShellExecute = true };
        var proc = Process.Start(psi);
        if (proc is null) { AppLog.Fail("LaunchInSpecVisible | Process.Start 回傳 null"); return false; }
        AppLog.Info($"LaunchInSpecVisible | 已啟動 PID {proc.Id}，等待初始化…");

        Thread.Sleep(1500);
        var ok = AttachToProcess(proc.Id);
        LogOutcome(ok, $"LaunchInSpecVisible | 注入={IsInjected}");
        return ok;
    }

    private bool AttachToProcess(int pid)
    {
        ProcessId = pid;

        using var hProc = NativeMethods.OpenProcess(PROCESS_ALL_ACCESS, false, pid);
        if (hProc.IsInvalid) { AppLog.Fail($"AttachToProcess | PID {pid} OpenProcess 失敗"); return false; }

        var remoteBase = FindRemoteModuleBase(hProc, DllFileName);
        if (remoteBase != IntPtr.Zero)
        {
            IsInjected = true;
            AppLog.Ok($"AttachToProcess | PID {pid} DLL 已在（不重複注入）");
            return true;
        }

        var dllPath = Path.Combine(AppContext.BaseDirectory, DllFileName);
        if (!File.Exists(dllPath)) { AppLog.Fail($"AttachToProcess | DLL 不存在：{dllPath}"); return false; }

        if (!InjectDll(hProc, dllPath)) { AppLog.Fail($"AttachToProcess | PID {pid} 注入失敗"); return false; }

        Thread.Sleep(300);
        IsInjected = FindRemoteModuleBase(hProc, DllFileName) != IntPtr.Zero;
        LogOutcome(IsInjected, $"AttachToProcess | PID {pid} 注入{(IsInjected ? "成功" : "後找不到模組")}");
        return IsInjected;
    }

    public bool HideMainWindow()
    {
        var ok = CallExport("ThreadWrapper_HideMainWindow", IntPtr.Zero);
        LogOutcome(ok, $"HideMainWindow | 呼叫後 IsWindowVisible={IsMainWindowVisible()}");
        return ok;
    }

    public bool ShowMainWindow()
    {
        var ok = CallExport("ThreadWrapper_ShowMainWindow", IntPtr.Zero);
        LogOutcome(ok, $"ShowMainWindow | 呼叫後 IsWindowVisible={IsMainWindowVisible()}");
        return ok;
    }

    /// <summary>直接探測 InSpecAppFrame 主視窗可見性（Win32 IsWindowVisible）。null=視窗不存在。</summary>
    public bool? IsMainWindowVisible()
    {
        var h = NativeMethods.FindWindow("InSpecAppFrame", null);
        return h == IntPtr.Zero ? null : NativeMethods.IsWindowVisible(h);
    }

    public string LastExportPath { get; private set; } = "";

    /// <summary>準備一份啟用 AutoExport 的 .iwp 工作副本（不動客戶原檔），指向一個乾淨的匯出檔路徑。
    /// .iwp 是 UTF-16LE 括號式腳本，AutoExport 設定就在 Prog 標頭：
    /// (AutoExpFile "路徑") 與 (AutoExpFileInf ((Enab 0/1) ...))，把 Enab 0→1、填入路徑即可。</summary>
    public AutoExportPaths PrepareAutoExportCopy(string sourceIwp)
    {
        var workDir = Path.Combine(Path.GetTempPath(), "InSpecTool");
        Directory.CreateDirectory(workDir);
        var workIwp = Path.Combine(workDir, "run_" + Path.GetFileNameWithoutExtension(sourceIwp) + ".iwp");
        var exportTxt = Path.Combine(workDir, "export_" + Path.GetFileNameWithoutExtension(sourceIwp) + ".txt");

        var text = File.ReadAllText(sourceIwp, Encoding.Unicode);
        var iwpPathForFile = exportTxt.Replace("\\", "\\\\");
        var hadFileField = text.Contains("(AutoExpFile \"\")");
        var hadEnab0 = text.Contains("(AutoExpFileInf ((Enab 0)");
        text = ReplaceFirst(text, "(AutoExpFile \"\")", $"(AutoExpFile \"{iwpPathForFile}\")");
        text = ReplaceFirst(text, "(AutoExpFileInf ((Enab 0)", "(AutoExpFileInf ((Enab 1)");
        File.WriteAllText(workIwp, text, Encoding.Unicode);

        // 乾淨起點：AutoExport 是 append 模式，先刪舊檔避免讀到上一輪殘留
        if (File.Exists(exportTxt)) File.Delete(exportTxt);
        LastExportPath = exportTxt;
        AppLog.Info(
            $"PrepareAutoExportCopy | src={Path.GetFileName(sourceIwp)}; 啟用AutoExport(檔路徑欄位存在={hadFileField}, 原Enab0={hadEnab0}); " +
            $"work={workIwp}; export={exportTxt}");
        if (!hadEnab0)
            AppLog.Warn("PrepareAutoExportCopy | 來源 .iwp 找不到 (AutoExpFileInf ((Enab 0)——可能已是 Enab 1 或格式不同，未改到");
        return new AutoExportPaths(workIwp, exportTxt);
    }

    /// <summary>私有記錄輔助——非公開 API，VAE003 只擋 public 簽名，這裡不受限。</summary>
    private static void LogOutcome(bool ok, string message)
    {
        if (ok) AppLog.Ok(message); else AppLog.Fail(message);
    }

    private static string ReplaceFirst(string haystack, string oldValue, string newValue)
    {
        var i = haystack.IndexOf(oldValue, StringComparison.Ordinal);
        return i < 0 ? haystack : haystack.Substring(0, i) + newValue + haystack.Substring(i + oldValue.Length);
    }

    /// <summary>官方 iscmd /run 驅動 InSpec 開檔＋執行。</summary>
    public Task<Result<bool>> RunViaIscmdAsync(RunMeasurementRequest request)
        => request.NoWait
            ? RunIscmdAsync("/run", request.WorkIwpPath, "/nowait")
            : RunIscmdAsync("/run", request.WorkIwpPath);

    private static async Task<Result<bool>> RunIscmdAsync(params string[] args)
    {
        var argline = string.Join(" ", args);
        if (!File.Exists(IscmdPath))
        {
            AppLog.Fail($"iscmd {argline} | iscmd.exe 不存在：{IscmdPath}");
            return Result<bool>.Ok(false);
        }
        var psi = new ProcessStartInfo
        {
            FileName = IscmdPath,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        try
        {
            var proc = Process.Start(psi);
            if (proc is null) { AppLog.Fail($"iscmd {argline} | Process.Start 回傳 null"); return Result<bool>.Ok(false); }
            await proc.WaitForExitAsync();
            var ok = proc.ExitCode == 0; // 非零＝InSpec 沒在跑或參數不認得（手冊附錄 B）
            LogOutcome(ok, $"iscmd {argline} | exit code={proc.ExitCode}");
            return Result<bool>.Ok(ok);
        }
        catch (Exception ex)
        {
            AppLog.Error($"iscmd {argline}", ex);
            return Result<bool>.Fail(ResultKind.InfrastructureError, ex.Message);
        }
    }

    /// <summary>讀 AutoExport 匯出檔（Tab 分隔文字）。檔案不存在回傳空清單（正常情況，非失敗）。</summary>
    public Result<IReadOnlyList<string>> ReadExportFile(string exportTxt)
    {
        try
        {
            if (!File.Exists(exportTxt))
            {
                AppLog.Warn($"ReadExportFile | 檔案不存在：{exportTxt}");
                return Result<IReadOnlyList<string>>.Ok(Array.Empty<string>());
            }
            var bytes = new FileInfo(exportTxt).Length;
            // InSpec 匯出可能是 ANSI 或 UTF-16（依 .iwp 的 Unicode 旗標），先試預設，退回 Unicode
            var raw = File.ReadAllText(exportTxt);
            if (raw.Contains('\0')) raw = File.ReadAllText(exportTxt, Encoding.Unicode);
            var lines = raw.Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0).ToList();
            AppLog.Info($"ReadExportFile | {Path.GetFileName(exportTxt)}: {bytes} bytes, {lines.Count} 行");
            return Result<IReadOnlyList<string>>.Ok(lines);
        }
        catch (IOException ex)
        {
            AppLog.Error("ReadExportFile", ex);
            return Result<IReadOnlyList<string>>.Fail(ResultKind.InfrastructureError, ex.Message);
        }
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

    /// <summary>解析匯出函式在目標行程內的位址，不硬編 RVA（每次重建 DLL 都會位移）。
    /// 本機也載入同一份 DLL 只為讀它的匯出表算 RVA，再加上目標行程內實際的模組基底位址。</summary>
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
