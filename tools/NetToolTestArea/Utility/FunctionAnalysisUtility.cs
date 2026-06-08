using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetToolTestArea.Utility
{
    /// <summary>
    /// 函數分析工具類
    /// </summary>
    public static class FunctionAnalysisUtility
    {
        public sealed class TargetInfo
        {
            public int ProcessId { get; set; }
            public string WindowTitle { get; set; }
            public string ProcessPath { get; set; }
        }

        public static TargetInfo FindInSpecTarget()
        {
            MemoryInjectionUtility memoryInjectionUtility = new MemoryInjectionUtility();
            int processId;
            string windowTitle;
            string processPath;

            bool found = memoryInjectionUtility.TryGetInSpecTarget(out processId, out windowTitle, out processPath);
            if (!found || processId <= 0)
            {
                return null;
            }

            return new TargetInfo
            {
                ProcessId = processId,
                WindowTitle = windowTitle,
                ProcessPath = processPath
            };
        }

        // Windows API 常數
        private const uint PROCESS_QUERY_INFORMATION = 0x0400;
        private const uint PROCESS_VM_READ = 0x0010;
        private const uint LIST_MODULES_ALL = 0x03;
        private const uint LIST_MODULES_32BIT = 0x01;
        private const uint LIST_MODULES_64BIT = 0x02;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct FunctionInfoNative
        {
            public IntPtr address;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string name;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string dllName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string time;
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int ScanFunctionsDelegate(uint processId, [Out] FunctionInfoNative[] buffer, int bufferSize);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);

        // Windows API 結構
        [StructLayout(LayoutKind.Sequential)]
        private struct MODULEINFO
        {
            public IntPtr lpBaseOfDll;
            public uint SizeOfImage;
            public IntPtr EntryPoint;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MODULEENTRY32
        {
            public uint dwSize;
            public uint th32ModuleID;
            public uint th32ProcessID;
            public uint GlblcntUsage;
            public uint ProccntUsage;
            public IntPtr modBaseAddr;
            public uint modBaseSize;
            public IntPtr hModule;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szModule;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szExePath;
        }

        // Windows API 函數宣告
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool EnumProcessModules(IntPtr hProcess, IntPtr[] lphModule, uint cb, out uint lpcbNeeded);

        [DllImport("psapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern uint GetModuleFileNameEx(IntPtr hProcess, IntPtr hModule, StringBuilder lpFilename, uint nSize);

        [DllImport("dbghelp.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool SymInitialize(IntPtr hProcess, string UserSearchPath, bool fInvadeProcess);

        [DllImport("dbghelp.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool SymCleanup(IntPtr hProcess);

        [DllImport("dbghelp.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool SymEnumSymbols(IntPtr hProcess, ulong BaseOfDll, string Mask, SymEnumSymbolsProc EnumSymbolsCallback, IntPtr UserContext);

        [DllImport("dbghelp.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool SymFromName(IntPtr hProcess, string Name, out SYMBOL_INFO Symbol);

        [DllImport("dbghelp.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern ulong SymLoadModuleEx(IntPtr hProcess, IntPtr hFile, string ImageName, string ModuleName, ulong BaseOfDll, int DllSize, IntPtr Data, int Flags);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SYMBOL_INFO
        {
            public uint SizeOfStruct;
            public uint TypeIndex;
            public ulong Reserved1;
            public ulong Reserved2;
            public uint Index;
            public uint Size;
            public ulong ModBase;
            public uint Flags;
            public ulong Value;
            public ulong Address;
            public uint Register;
            public uint Scope;
            public uint Tag;
            public IntPtr NameLen;
            public uint MaxNameLen;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 1024)]
            public string Name;
        }

        private delegate bool SymEnumSymbolsProc(ref SYMBOL_INFO pSymInfo, uint SymbolSize, IntPtr UserContext);

        /// <summary>
        /// 函數資訊
        /// </summary>
        public class FunctionInfo
        {
            public string ModuleName { get; set; }
            public string ModulePath { get; set; }
            public string FunctionName { get; set; }
            public string Address { get; set; }
            public IntPtr BaseAddress { get; set; }
        }

        public sealed class ScanRow
        {
            public string Function { get; set; }
            public string FunctionName { get; set; }
            public string DllName { get; set; }
            public string Address { get; set; }
            public string Time { get; set; }
        }

        public sealed class ScanResult
        {
            public bool Success { get; set; }
            public int ProcessId { get; set; }
            public int ModuleCount { get; set; }
            public int RowCount { get; set; }
            public List<ScanRow> Rows { get; set; }
            public string ErrorMessage { get; set; }
        }

        public static ScanResult ScanInSpecProcessFunctionsForDisplay()
        {
            MemoryInjectionUtility memoryInjectionUtility = new MemoryInjectionUtility();
            string dllPath = memoryInjectionUtility.QueryDefaultFunctionScanDllPath();
            TargetInfo target = FindInSpecTarget();
            if (target == null || target.ProcessId <= 0)
            {
                throw new InvalidOperationException("未找到 InSpec 程序");
            }
            return ScanForDisplay(target.ProcessId, dllPath);
        }

        public static ScanResult ScanForDisplay(int processId, string dllPath)
        {
            if (processId <= 0)
            {
                throw new InvalidOperationException("無效的程序 ID");
            }

            IntPtr hModule = IntPtr.Zero;
            try
            {
                if (!File.Exists(dllPath))
                {
                    throw new FileNotFoundException("DLL 不存在", dllPath);
                }

                hModule = LoadLibrary(dllPath);
                if (hModule == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();
                    throw new System.ComponentModel.Win32Exception(error);
                }

                IntPtr pScanFunctions = GetProcAddress(hModule, "ScanFunctions");
                if (pScanFunctions == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();
                    throw new System.ComponentModel.Win32Exception(error);
                }

                ScanFunctionsDelegate scanFunctions = Marshal.GetDelegateForFunctionPointer<ScanFunctionsDelegate>(pScanFunctions);

                FunctionInfoNative[] buffer = new FunctionInfoNative[6000];
                int count = scanFunctions((uint)processId, buffer, buffer.Length);

                if (count <= 0)
                {
                    throw new InvalidOperationException();
                }

                List<ScanRow> rows = new List<ScanRow>();
                for (int i = 0; i < count; i++)
                {
                    if (buffer[i].address == IntPtr.Zero)
                    {
                        continue;
                    }

                    string name = buffer[i].name ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    rows.Add(new ScanRow
                    {
                        Function = $"0x{buffer[i].address.ToInt64():X16}",
                        FunctionName = name,
                        DllName = buffer[i].dllName ?? string.Empty,
                        Address = $"0x{buffer[i].address.ToInt64():X16}",
                        Time = buffer[i].time ?? string.Empty
                    });
                }

                if (rows.Count == 0)
                {
                    throw new InvalidOperationException();
                }

                return new ScanResult
                {
                    Success = true,
                    ProcessId = processId,
                    ModuleCount = 0,
                    RowCount = rows.Count,
                    Rows = rows
                };
            }
            catch (Exception ex)
            {
                return new ScanResult
                {
                    Success = false,
                    ProcessId = processId,
                    ErrorMessage = ex.ToString()
                };
            }
            finally
            {
                if (hModule != IntPtr.Zero)
                {
                    FreeLibrary(hModule);
                }
            }
        }

        public sealed class RecordingResult
        {
            public TimeSpan Duration { get; set; }
            public List<string> RecordedActions { get; set; }
        }

        private static ApiMonitorSession currentSession;
        private static bool isRecording;
        private static DateTime recordingStartTime;

        public static bool IsRecording => isRecording;

        public static bool StartRecording(bool enableApiMonitor, string dllPath, Action<string> log)
        {
            if (isRecording)
            {
                return false;
            }

            if (enableApiMonitor)
            {
                try
                {
                    currentSession?.Dispose();
                    currentSession = ApiMonitorSession.Start(dllPath, log);
                }
                catch (Exception ex)
                {
                    log?.Invoke(ex.ToString());
                    currentSession?.Dispose();
                    currentSession = null;
                    return false;
                }
            }

            isRecording = true;
            recordingStartTime = DateTime.Now;
            return true;
        }

        public static RecordingResult StopRecording(bool enableApiMonitor, Action<string> log)
        {
            if (!isRecording)
            {
                return null;
            }

            DateTime end = DateTime.Now;
            TimeSpan duration = end - recordingStartTime;

            if (enableApiMonitor)
            {
                try
                {
                    currentSession?.Dispose();
                }
                catch (Exception ex)
                {
                    log?.Invoke(ex.ToString());
                }
                currentSession = null;
            }

            isRecording = false;
            return new RecordingResult { Duration = duration, RecordedActions = new List<string>() };
        }

        public sealed class ApiMonitorSession : IDisposable
        {
            private readonly Action<string> log;
            private readonly CancellationTokenSource cts;
            private NamedPipeServerStream server;
            private Task readTask;
            private bool disposed;

            private ApiMonitorSession(Action<string> log)
            {
                this.log = log;
                this.cts = new CancellationTokenSource();
            }

            public static ApiMonitorSession Start(string dllPath, Action<string> log)
            {
                if (string.IsNullOrWhiteSpace(dllPath))
                {
                    throw new InvalidOperationException("DLL 路徑為空");
                }

                if (!File.Exists(dllPath))
                {
                    throw new FileNotFoundException("DLL 文件不存在", dllPath);
                }

                TargetInfo target = FindInSpecTarget();
                if (target == null || target.ProcessId <= 0)
                {
                    throw new InvalidOperationException("未找到 InSpec 程序");
                }

                log?.Invoke($"[成功] 已鎖定目標：{(string.IsNullOrWhiteSpace(target.WindowTitle) ? "（無視窗標題）" : target.WindowTitle)}");
                log?.Invoke($"[成功] 目標 PID: {target.ProcessId}");
                if (!string.IsNullOrWhiteSpace(target.ProcessPath))
                {
                    log?.Invoke($"[成功] 目標程式路徑: {target.ProcessPath}");
                }
                log?.Invoke($"[成功] DLL 路徑: {dllPath}");

                if (!Core.DllCore.IsDllInjected(target.ProcessId, "InSpecHookDLL.dll"))
                {
                    MemoryInjectionUtility injectUtility = new MemoryInjectionUtility();
                    var injectResult = injectUtility.TryInjectDllToProcess(target.ProcessId, dllPath, null);
                    if (!injectResult.Success)
                    {
                        throw new InvalidOperationException(injectResult.ErrorMessage ?? "DLL 注入失敗");
                    }
                }
                else
                {
                    Core.DllCore.SetTargetProcessId(target.ProcessId);
                }

                var session = new ApiMonitorSession(log);
                session.StartPipeServer();

                bool hookOk = Core.DllCore.EnableApiHook();
                if (!hookOk)
                {
                    session.Dispose();
                    throw new InvalidOperationException("EnableApiHook 回傳 false");
                }

                return session;
            }

            private void StartPipeServer()
            {
                readTask = Task.Run(async () =>
                {
                    while (!cts.IsCancellationRequested)
                    {
                        NamedPipeServerStream localServer = null;
                        try
                        {
                            PipeSecurity security = new PipeSecurity();
                            security.AddAccessRule(new PipeAccessRule(
                                new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                                PipeAccessRights.ReadWrite,
                                AccessControlType.Allow));

                            localServer = new NamedPipeServerStream(
                                "InSpecHookDLL_LogPipe",
                                PipeDirection.In,
                                1,
                                PipeTransmissionMode.Byte,
                                PipeOptions.Asynchronous,
                                4096,
                                4096,
                                security);

                            server = localServer;
                            log?.Invoke("[成功] 命名管道伺服器已建立，等待 DLL 連線...");

                            await localServer.WaitForConnectionAsync(cts.Token);
                            log?.Invoke("[成功] 命名管道已連線（DLL 日誌開始輸入）");

                            byte[] buffer = new byte[4096];
                            while (!cts.IsCancellationRequested && localServer.IsConnected)
                            {
                                int bytesRead = await localServer.ReadAsync(buffer, 0, buffer.Length, cts.Token);
                                if (bytesRead <= 0)
                                {
                                    break;
                                }

                                string logMessage = Encoding.Unicode.GetString(buffer, 0, bytesRead).TrimEnd('\0', '\r', '\n');
                                if (!string.IsNullOrEmpty(logMessage))
                                {
                                    log?.Invoke($"[DLL] {logMessage}");
                                }
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            return;
                        }
                        catch (Exception ex)
                        {
                            log?.Invoke(ex.ToString());
                        }
                        finally
                        {
                            try
                            {
                                localServer?.Dispose();
                            }
                            catch
                            {
                            }
                            server = null;
                        }
                    }
                }, cts.Token);
            }

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;

                try
                {
                    Core.DllCore.DisableApiHook();
                }
                catch (Exception ex)
                {
                    log?.Invoke(ex.ToString());
                }

                try
                {
                    cts.Cancel();
                }
                catch
                {
                }

                try
                {
                    if (server != null)
                    {
                        server.Dispose();
                        server = null;
                    }
                }
                catch (Exception ex)
                {
                    log?.Invoke(ex.ToString());
                }

                try
                {
                    readTask?.Wait(500);
                }
                catch
                {
                    // ignore
                }

                try
                {
                    cts.Dispose();
                }
                catch
                {
                    // ignore
                }
            }
        }

        /// <summary>
        /// 從檔案讀取匯出函數列表（不需要進程運行）
        /// </summary>
        public static List<string> QueryExportedFunctionsFromFile(string filePath)
        {
            return QueryExportedFunctions(filePath);
        }

        /// <summary>
        /// 掃描進程的所有模組和匯出函數
        /// </summary>
        public static List<FunctionInfo> ScanProcessFunctions(int processId)
        {
            List<FunctionInfo> functions = new List<FunctionInfo>();
            IntPtr hProcess = IntPtr.Zero;

            try
            {
                // 開啟進程
                hProcess = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, false, processId);
                if (hProcess == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();
                    throw new System.ComponentModel.Win32Exception(error, "無法開啟目標進程");
                }

                // 列舉所有模組
                IntPtr[] hMods = new IntPtr[1024];
                uint cbNeeded;
                if (!EnumProcessModules(hProcess, hMods, (uint)(hMods.Length * IntPtr.Size), out cbNeeded))
                {
                    int error = Marshal.GetLastWin32Error();
                    throw new System.ComponentModel.Win32Exception(error, "無法列舉進程模組");
                }

                uint moduleCount = cbNeeded / (uint)IntPtr.Size;

                // 遍歷每個模組
                for (uint i = 0; i < moduleCount; i++)
                {
                    if (hMods[i] == IntPtr.Zero) continue;

                    StringBuilder modulePath = new StringBuilder(260);
                    if (GetModuleFileNameEx(hProcess, hMods[i], modulePath, 260) > 0)
                    {
                        string moduleName = System.IO.Path.GetFileName(modulePath.ToString());
                        string fullPath = modulePath.ToString();

                        // 嘗試讀取 PE 檔案頭來獲取匯出函數
                        try
                        {
                            var exportedFunctions = QueryExportedFunctions(fullPath);
                            foreach (var funcName in exportedFunctions)
                            {
                                functions.Add(new FunctionInfo
                                {
                                    ModuleName = moduleName,
                                    ModulePath = fullPath,
                                    FunctionName = funcName,
                                    Address = "N/A", // 運行時地址需要通過其他方式獲取
                                    BaseAddress = hMods[i]
                                });
                            }
                        }
                        catch
                        {
                            // 如果無法讀取匯出函數，至少記錄模組名稱
                            functions.Add(new FunctionInfo
                            {
                                ModuleName = moduleName,
                                ModulePath = fullPath,
                                FunctionName = "(無法讀取匯出函數)",
                                Address = "N/A",
                                BaseAddress = hMods[i]
                            });
                        }
                    }
                }
            }
            finally
            {
                if (hProcess != IntPtr.Zero)
                {
                    CloseHandle(hProcess);
                }
            }

            return functions;
        }

        /// <summary>
        /// 從 PE 檔案讀取匯出函數列表
        /// </summary>
        private static List<string> QueryExportedFunctions(string filePath)
        {
            List<string> functions = new List<string>();

            if (string.IsNullOrWhiteSpace(filePath) || !System.IO.File.Exists(filePath))
            {
                return functions;
            }

            try
            {
                // 方法 1: 嘗試使用 dumpbin（如果 Visual Studio 已安裝）
                functions = GetExportedFunctionsUsingDumpbin(filePath);
                if (functions.Count > 0)
                {
                    return functions;
                }
            }
            catch
            {
                // 如果 dumpbin 失敗，嘗試直接讀取 PE 檔案
            }

            try
            {
                // 方法 2: 直接讀取 PE 檔案頭
                functions = QueryExportedFunctionsFromPE(filePath);
            }
            catch
            {
                // 如果都失敗，返回空列表
            }

            return functions;
        }

        /// <summary>
        /// 使用 dumpbin 工具讀取匯出函數
        /// </summary>
        private static List<string> GetExportedFunctionsUsingDumpbin(string filePath)
        {
            List<string> functions = new List<string>();

            try
            {
                // 查找 dumpbin.exe（通常在 Visual Studio 安裝目錄）
                string dumpbinPath = FindDumpbinPath();
                if (string.IsNullOrWhiteSpace(dumpbinPath))
                {
                    return functions;
                }

                System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = dumpbinPath,
                    Arguments = $"/exports \"{filePath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (System.Diagnostics.Process process = System.Diagnostics.Process.Start(psi))
                {
                    if (process == null) return functions;

                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();

                    // 解析 dumpbin 輸出
                    bool inExportSection = false;
                    foreach (string line in output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
                    {
                        if (line.Contains("ordinal") && line.Contains("name"))
                        {
                            inExportSection = true;
                            continue;
                        }

                        if (inExportSection)
                        {
                            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("Summary"))
                            {
                                break;
                            }

                            // 解析函數名稱（格式：序號 RVA 名稱）
                            string[] parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 4)
                            {
                                // 通常格式：序號 RVA 名稱 [其他]
                                string funcName = parts[3];
                                if (!string.IsNullOrWhiteSpace(funcName) && !functions.Contains(funcName))
                                {
                                    functions.Add(funcName);
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // 忽略錯誤
            }

            return functions;
        }

        /// <summary>
        /// 查找 dumpbin.exe 路徑
        /// </summary>
        private static string FindDumpbinPath()
        {
            // 常見的 Visual Studio 安裝路徑
            string[] possiblePaths = new[]
            {
                @"C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Tools\MSVC\*\bin\Hostx86\x86\dumpbin.exe",
                @"C:\Program Files\Microsoft Visual Studio\2022\Professional\VC\Tools\MSVC\*\bin\Hostx86\x86\dumpbin.exe",
                @"C:\Program Files\Microsoft Visual Studio\2022\Enterprise\VC\Tools\MSVC\*\bin\Hostx86\x86\dumpbin.exe",
                @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\VC\Tools\MSVC\*\bin\Hostx86\x86\dumpbin.exe",
                @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional\VC\Tools\MSVC\*\bin\Hostx86\x86\dumpbin.exe",
                @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Enterprise\VC\Tools\MSVC\*\bin\Hostx86\x86\dumpbin.exe"
            };

            foreach (string pattern in possiblePaths)
            {
                string[] matches = System.IO.Directory.GetFiles(
                    System.IO.Path.GetDirectoryName(pattern) ?? "",
                    System.IO.Path.GetFileName(pattern),
                    System.IO.SearchOption.AllDirectories
                );

                if (matches.Length > 0 && System.IO.File.Exists(matches[0]))
                {
                    return matches[0];
                }
            }

            return null;
        }

        /// <summary>
        /// 直接從 PE 檔案讀取匯出函數（簡化版本）
        /// </summary>
        private static List<string> QueryExportedFunctionsFromPE(string filePath)
        {
            List<string> functions = new List<string>();

            try
            {
                using (System.IO.FileStream fs = new System.IO.FileStream(filePath, System.IO.FileMode.Open, System.IO.FileAccess.Read))
                {
                    using (System.IO.BinaryReader reader = new System.IO.BinaryReader(fs))
                    {
                        // 讀取 DOS 頭
                        fs.Position = 0;
                        if (reader.ReadUInt16() != 0x5A4D) // "MZ"
                        {
                            return functions; // 不是有效的 PE 檔案
                        }

                        // 讀取 PE 頭偏移
                        fs.Position = 0x3C;
                        uint peOffset = reader.ReadUInt32();

                        // 讀取 PE 簽名
                        fs.Position = peOffset;
                        if (reader.ReadUInt32() != 0x00004550) // "PE\0\0"
                        {
                            return functions; // 不是有效的 PE 檔案
                        }

                        // 讀取 COFF 頭
                        ushort machine = reader.ReadUInt16();
                        ushort numberOfSections = reader.ReadUInt16();
                        // ... 其他欄位

                        // 讀取可選頭（Optional Header）
                        uint optionalHeaderSize = reader.ReadUInt32();
                        // ... 簡化處理，實際需要完整解析

                        // 讀取匯出表需要更多複雜的解析
                        // 這裡先返回空列表，建議使用 dumpbin
                    }
                }
            }
            catch
            {
                // 忽略錯誤
            }

            return functions;
        }
    }
}

