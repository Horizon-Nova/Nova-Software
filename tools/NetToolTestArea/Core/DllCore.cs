using System;
using System.Runtime.InteropServices;
using System.Text;

namespace NetToolTestArea.Core
{
    /// <summary>
    /// DLL 核心類
    /// </summary>
    public static class DllCore
    {
        private const string DLL_NAME = "InSpecHookDLL.dll";
        private static int targetProcessId = 0;

        // Windows API 常數
        private const uint PROCESS_CREATE_THREAD = 0x0002;
        private const uint PROCESS_QUERY_INFORMATION = 0x0400;
        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        private const uint PROCESS_VM_OPERATION = 0x0008;
        private const uint PROCESS_VM_WRITE = 0x0020;
        private const uint PROCESS_VM_READ = 0x0010;
        private const uint PROCESS_ALL_ACCESS = 0x001F0FFF;
        private const uint MEM_COMMIT = 0x1000;
        private const uint MEM_RESERVE = 0x2000;
        private const uint PAGE_READWRITE = 0x04;
        private const uint MEM_RELEASE = 0x8000;
        private const uint INFINITE = 0xFFFFFFFF;
        private const uint THREAD_ALL_ACCESS = 0x1F03FF;
        private const int WH_GETMESSAGE = 3;

        // Windows API 函數宣告
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        private static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out UIntPtr lpNumberOfBytesWritten);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out UIntPtr lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, out IntPtr lpThreadId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetExitCodeThread(IntPtr hThread, out IntPtr lpExitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        private static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint dwFreeType);

        [DllImport("psapi.dll", SetLastError = true)]
        private static extern bool EnumProcessModules(IntPtr hProcess, IntPtr[] lphModule, uint cb, out uint lpcbNeeded);

        [DllImport("psapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern uint GetModuleFileNameEx(IntPtr hProcess, IntPtr hModule, StringBuilder lpFilename, uint nSize);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr SetWindowsHookEx(int idHook, IntPtr lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("ntdll.dll", SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        private static extern int NtCreateThreadEx(out IntPtr hThread, uint dwDesiredAccess, IntPtr lpThreadAttributes, IntPtr hProcess, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, IntPtr lpThreadId, IntPtr lpClientId, IntPtr lpUnknown1, IntPtr lpUnknown2);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint GetDllVersionDelegate();

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        private delegate void GetDllVersionStringDelegate(StringBuilder buffer, int bufferSize);

        // ========== 公開方法：DLL 注入 ==========

        /// <summary>
        /// 注入 DLL 到目標進程
        /// </summary>
        public static void InjectDLL(int processId, string dllPath) => InjectDLL(processId, dllPath, null);

        /// <summary>
        /// 注入 DLL 到目標進程（帶日誌回調）
        /// </summary>
        public static void InjectDLL(int processId, string dllPath, Action<string> logCallback)
        {
            IntPtr hProcess = IntPtr.Zero;
            IntPtr pRemoteMemory = IntPtr.Zero;
            IntPtr hThread = IntPtr.Zero;

            void Log(string message)
            {
                System.Diagnostics.Debug.WriteLine($"[DllCore] {message}");
                logCallback?.Invoke(message);
            }

            try
            {
                Log($"開始注入 DLL 到進程 PID: {processId}");
                Log($"DLL 路徑: {dllPath}");

                try
                {
                    DiagnoseLocalDllLoad(dllPath, Log);
                }
                catch (Exception diagEx)
                {
                    Log($"✗ 詳細檢測執行時發生例外（將忽略並繼續注入）：{diagEx.GetType().Name} - {diagEx.Message}");
                }

                int lastError = 0;
                System.Text.StringBuilder errorDetails = new System.Text.StringBuilder();

                Log("步驟 1: 嘗試開啟目標進程...");

                uint accessRights = PROCESS_CREATE_THREAD | PROCESS_QUERY_INFORMATION | PROCESS_VM_OPERATION | PROCESS_VM_WRITE | PROCESS_VM_READ;

                Log($"  嘗試 1: 標準權限");
                hProcess = OpenProcess(accessRights, false, processId);
                if (hProcess == IntPtr.Zero)
                {
                    lastError = Marshal.GetLastWin32Error();
                    errorDetails.AppendLine($"標準權限失敗: 錯誤代碼 {lastError} (0x{lastError:X8})");
                    Log($"  ✗ 標準權限失敗: 錯誤代碼 {lastError} (0x{lastError:X8})");

                    Log($"  嘗試 2: 受限查詢權限");
                    accessRights = PROCESS_CREATE_THREAD | PROCESS_QUERY_LIMITED_INFORMATION | PROCESS_VM_OPERATION | PROCESS_VM_WRITE | PROCESS_VM_READ;
                    hProcess = OpenProcess(accessRights, false, processId);
                    if (hProcess == IntPtr.Zero)
                    {
                        int error = Marshal.GetLastWin32Error();
                        errorDetails.AppendLine($"受限查詢權限失敗: 錯誤代碼 {error} (0x{error:X8})");
                        lastError = error;
                        Log($"  ✗ 受限查詢權限失敗: 錯誤代碼 {error} (0x{error:X8})");

                        Log($"  嘗試 3: 完全訪問權限");
                        hProcess = OpenProcess(PROCESS_ALL_ACCESS, false, processId);
                        if (hProcess == IntPtr.Zero)
                        {
                            int error2 = Marshal.GetLastWin32Error();
                            errorDetails.AppendLine($"完全訪問權限失敗: 錯誤代碼 {error2} (0x{error2:X8})");
                            lastError = error2;
                            Log($"  ✗ 完全訪問權限失敗: 錯誤代碼 {error2} (0x{error2:X8})");
                        }
                        else
                        {
                            Log($"  ✓ 完全訪問權限成功，進程句柄: 0x{hProcess.ToInt64():X16}");
                        }
                    }
                    else
                    {
                        Log($"  ✓ 受限查詢權限成功，進程句柄: 0x{hProcess.ToInt64():X16}");
                    }
                }
                else
                {
                    Log($"  ✓ 標準權限成功，進程句柄: 0x{hProcess.ToInt64():X16}");
                }

                if (hProcess == IntPtr.Zero)
                {
                    string errorMessage = $"無法開啟目標進程 (PID: {processId})。所有權限級別嘗試均失敗：\n{errorDetails}";
                    Log($"✗ 所有 OpenProcess 嘗試均失敗");
                    throw new System.ComponentModel.Win32Exception(lastError, errorMessage);
                }

                Log($"✓ 步驟 1 完成：成功開啟目標進程");

                Log("步驟 2: 計算 DLL 路徑大小...");
                byte[] dllPathBytes = Encoding.Unicode.GetBytes(dllPath + "\0");
                uint pathSize = (uint)dllPathBytes.Length;
                Log($"  DLL 路徑大小: {pathSize} 位元組");

                Log("步驟 3: 在目標進程中分配記憶體...");
                pRemoteMemory = VirtualAllocEx(hProcess, IntPtr.Zero, pathSize, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);

                if (pRemoteMemory == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();
                    Log($"✗ VirtualAllocEx 失敗: 錯誤代碼 {error} (0x{error:X8})");
                    throw new System.ComponentModel.Win32Exception(error);
                }
                Log($"  ✓ 記憶體分配成功，地址: 0x{pRemoteMemory.ToInt64():X16}");

                Log("步驟 4: 寫入 DLL 路徑到目標進程記憶體...");
                UIntPtr bytesWritten;
                bool writeSuccess = WriteProcessMemory(hProcess, pRemoteMemory, dllPathBytes, pathSize, out bytesWritten);

                if (!writeSuccess || bytesWritten.ToUInt32() != pathSize)
                {
                    int error = Marshal.GetLastWin32Error();
                    Log($"✗ WriteProcessMemory 失敗: 錯誤代碼 {error} (0x{error:X8}), 已寫入: {bytesWritten.ToUInt32()}/{pathSize}");
                    throw new System.ComponentModel.Win32Exception(error);
                }
                Log($"  ✓ 成功寫入 {bytesWritten.ToUInt32()} 位元組");

                Log("步驟 5: 取得 LoadLibraryW 函數地址...");
                IntPtr hKernel32 = GetModuleHandle("kernel32.dll");
                if (hKernel32 == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();
                    Log($"✗ GetModuleHandle 失敗: 錯誤代碼 {error} (0x{error:X8})");
                    throw new System.ComponentModel.Win32Exception(error);
                }
                Log($"  ✓ kernel32.dll 模組句柄: 0x{hKernel32.ToInt64():X16}");

                IntPtr pLoadLibrary = GetProcAddress(hKernel32, "LoadLibraryW");
                if (pLoadLibrary == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();
                    Log($"✗ GetProcAddress 失敗: 錯誤代碼 {error} (0x{error:X8})");
                    throw new System.ComponentModel.Win32Exception(error);
                }
                Log($"  ✓ LoadLibraryW 地址: 0x{pLoadLibrary.ToInt64():X16}");

                Log("步驟 6: 在目標進程中建立遠端執行緒...");
                Log("  嘗試方法 1: CreateRemoteThread...");
                IntPtr threadId = IntPtr.Zero;
                hThread = CreateRemoteThread(hProcess, IntPtr.Zero, 0, pLoadLibrary, pRemoteMemory, 0, out threadId);

                if (hThread == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();
                    Log($"  ✗ CreateRemoteThread 失敗: 錯誤代碼 {error} (0x{error:X8})");

                    try
                    {
                        Log("  嘗試方法 2: NtCreateThreadEx...");
                        int ntStatus = NtCreateThreadEx(out hThread, THREAD_ALL_ACCESS, IntPtr.Zero, hProcess, pLoadLibrary, pRemoteMemory, 0, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

                        if (ntStatus != 0 || hThread == IntPtr.Zero)
                        {
                            Log($"  ✗ NtCreateThreadEx 失敗: NT 狀態碼 {ntStatus} (0x{ntStatus:X8})");
                            throw new System.ComponentModel.Win32Exception(error);
                        }
                        Log($"  ✓ NtCreateThreadEx 成功，執行緒句柄: 0x{hThread.ToInt64():X16}");
                    }
                    catch (Exception ex)
                    {
                        Log($"  ✗ NtCreateThreadEx 調用異常: {ex.GetType().Name} - {ex.Message}");
                        throw new System.ComponentModel.Win32Exception(error);
                    }
                }
                else
                {
                    Log($"  ✓ CreateRemoteThread 成功，執行緒 ID: {threadId.ToInt64()}");
                }

                Log("步驟 7: 等待遠端執行緒完成...");
                uint waitResult = WaitForSingleObject(hThread, INFINITE);
                if (waitResult != 0)
                {
                    int error = Marshal.GetLastWin32Error();
                    Log($"✗ WaitForSingleObject 失敗: 錯誤代碼 {error} (0x{error:X8}), 返回值: {waitResult}");
                    throw new System.ComponentModel.Win32Exception(error);
                }
                Log($"  ✓ 遠端執行緒已完成");

                Log("步驟 8: 檢查 DLL 載入結果...");
                IntPtr exitCode;
                if (!GetExitCodeThread(hThread, out exitCode))
                {
                    int error = Marshal.GetLastWin32Error();
                    Log($"✗ GetExitCodeThread 失敗: 錯誤代碼 {error} (0x{error:X8})");
                    throw new System.ComponentModel.Win32Exception(error);
                }

                if (exitCode == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();
                    Log($"✗ LoadLibraryW 返回 NULL，DLL 載入失敗: 錯誤代碼 {error} (0x{error:X8})");
                    throw new System.ComponentModel.Win32Exception(error);
                }
                Log($"  ✓ DLL 載入成功，模組句柄: 0x{exitCode.ToInt64():X16}");
                Log("✓ 所有步驟完成，DLL 注入成功！");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DllCore] 注入失敗: {ex}");
                throw;
            }
            finally
            {
                if (hThread != IntPtr.Zero)
                {
                    CloseHandle(hThread);
                }
                if (pRemoteMemory != IntPtr.Zero && hProcess != IntPtr.Zero)
                {
                    VirtualFreeEx(hProcess, pRemoteMemory, 0, MEM_RELEASE);
                }
                if (hProcess != IntPtr.Zero)
                {
                    CloseHandle(hProcess);
                }
            }
        }

        /// <summary>
        /// 使用 SetWindowsHookEx 注入 DLL（請求方式，系統主動載入）
        /// </summary>
        public static void InjectDLLByHook(string windowClassName, string dllPath, Action<string> logCallback = null)
        {
            void Log(string message)
            {
                System.Diagnostics.Debug.WriteLine($"[DllCore] {message}");
                logCallback?.Invoke(message);
            }

            IntPtr hHook = IntPtr.Zero;
            IntPtr hModule = IntPtr.Zero;

            try
            {
                Log($"開始使用 SetWindowsHookEx 注入 DLL（請求方式）");
                Log($"目標視窗類別: {windowClassName}");
                Log($"DLL 路徑: {dllPath}");

                Log("步驟 1: 載入 DLL 到當前進程...");
                hModule = LoadLibrary(dllPath);
                if (hModule == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();
                    Log($"✗ LoadLibrary 失敗: 錯誤代碼 {error} (0x{error:X8})");
                    throw new System.ComponentModel.Win32Exception(error);
                }
                Log($"  ✓ DLL 已載入到當前進程，模組句柄: 0x{hModule.ToInt64():X16}");

                Log("步驟 2: 取得 Hook 函數地址...");
                IntPtr hookProc = GetProcAddress(hModule, "GetMsgProc");
                if (hookProc == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();
                    Log($"✗ GetProcAddress(GetMsgProc) 失敗: 錯誤代碼 {error} (0x{error:X8})");
                    throw new System.ComponentModel.Win32Exception(error);
                }
                Log($"  ✓ GetMsgProc 地址: 0x{hookProc.ToInt64():X16}");

                Log("步驟 3: 查找目標視窗...");
                IntPtr hWnd = FindWindow(windowClassName, null);
                if (hWnd == IntPtr.Zero)
                {
                    hWnd = FindWindow(null, windowClassName);
                }

                if (hWnd == IntPtr.Zero)
                {
                    Log($"✗ 未找到目標視窗: {windowClassName}");
                    throw new InvalidOperationException($"未找到目標視窗: {windowClassName}");
                }
                Log($"  ✓ 找到目標視窗，句柄: 0x{hWnd.ToInt64():X16}");

                Log("步驟 4: 取得視窗執行緒 ID...");
                uint threadId;
                uint processId;
                threadId = GetWindowThreadProcessId(hWnd, out processId);
                if (threadId == 0)
                {
                    int error = Marshal.GetLastWin32Error();
                    Log($"✗ GetWindowThreadProcessId 失敗: 錯誤代碼 {error} (0x{error:X8})");
                    throw new System.ComponentModel.Win32Exception(error);
                }
                Log($"  ✓ 視窗執行緒 ID: {threadId}, 進程 ID: {processId}");

                Log("步驟 5: 設置 Windows Hook（WH_GETMESSAGE）...");
                hHook = SetWindowsHookEx(WH_GETMESSAGE, hookProc, hModule, threadId);
                if (hHook == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();
                    Log($"✗ SetWindowsHookEx 失敗: 錯誤代碼 {error} (0x{error:X8})");
                    throw new System.ComponentModel.Win32Exception(error);
                }
                Log($"  ✓ Hook 設置成功，Hook 句柄: 0x{hHook.ToInt64():X16}");
                Log($"  ✓ 系統已自動載入 DLL 到目標進程（請求方式）");

                Log("步驟 6: 觸發消息以確保 DLL 載入...");
                PostMessage(hWnd, 0x0000, IntPtr.Zero, IntPtr.Zero);
                System.Threading.Thread.Sleep(500);
                Log($"  ✓ 消息已發送，等待 DLL 載入完成");

                Log("步驟 7: 卸載 Hook...");
                if (hHook != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(hHook);
                    hHook = IntPtr.Zero;
                    Log($"  ✓ Hook 已卸載");
                }

                Log("✓ 所有步驟完成，DLL 注入成功（請求方式）！");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DllCore] Hook 注入失敗: {ex}");
                throw;
            }
            finally
            {
                if (hHook != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(hHook);
                }
            }
        }

        // ========== 公開方法：遠端函數呼叫 ==========

        /// <summary>
        /// 檢查 DLL 是否已注入到目標進程
        /// </summary>
        public static bool IsDllInjected(int processId, string dllName)
        {
            IntPtr hProcess = IntPtr.Zero;
            try
            {
                hProcess = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, false, processId);
                if (hProcess == IntPtr.Zero)
                {
                    return false;
                }

                IntPtr hModule = QueryDllModuleHandle(hProcess, dllName);
                return hModule != IntPtr.Zero;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (hProcess != IntPtr.Zero)
                {
                    CloseHandle(hProcess);
                }
            }
        }

        /// <summary>
        /// 呼叫遠端進程中的函數
        /// </summary>
        public static bool CallRemoteFunction(int processId, string dllName, string functionName, IntPtr parameter)
        {
            IntPtr hProcess = IntPtr.Zero;
            IntPtr hThread = IntPtr.Zero;

            try
            {
                hProcess = OpenProcess(PROCESS_CREATE_THREAD | PROCESS_QUERY_INFORMATION | PROCESS_VM_OPERATION | PROCESS_VM_WRITE | PROCESS_VM_READ, false, processId);

                if (hProcess == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();
                    throw new System.ComponentModel.Win32Exception(error);
                }

                IntPtr hModule = QueryDllModuleHandle(hProcess, dllName);
                if (hModule == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();
                    if (error == 0)
                    {
                        error = 126;
                    }
                    throw new System.ComponentModel.Win32Exception(error);
                }

                IntPtr pRemoteFunction = QueryRemoteFunctionAddress(hProcess, hModule, functionName);

                IntPtr threadId;
                hThread = CreateRemoteThread(hProcess, IntPtr.Zero, 0, pRemoteFunction, parameter, 0, out threadId);

                if (hThread == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();
                    throw new System.ComponentModel.Win32Exception(error);
                }

                uint waitResult = WaitForSingleObject(hThread, 5000);
                if (waitResult != 0)
                {
                    if (waitResult == 0xFFFFFFFF)
                    {
                        int error = Marshal.GetLastWin32Error();
                        throw new System.ComponentModel.Win32Exception(error);
                    }
                    throw new TimeoutException();
                }

                IntPtr exitCode;
                if (GetExitCodeThread(hThread, out exitCode))
                {
                    return exitCode != IntPtr.Zero;
                }

                return false;
            }
            finally
            {
                if (hThread != IntPtr.Zero)
                {
                    CloseHandle(hThread);
                }
                if (hProcess != IntPtr.Zero)
                {
                    CloseHandle(hProcess);
                }
            }
        }

        /// <summary>
        /// 直接使用函數地址呼叫遠端進程中的函數
        /// </summary>
        public static bool CallRemoteFunctionByAddress(int processId, IntPtr functionAddress, IntPtr parameter)
        {
            IntPtr hProcess = IntPtr.Zero;
            IntPtr hThread = IntPtr.Zero;

            try
            {
                hProcess = OpenProcess(PROCESS_CREATE_THREAD | PROCESS_QUERY_INFORMATION | PROCESS_VM_OPERATION | PROCESS_VM_WRITE | PROCESS_VM_READ, false, processId);

                if (hProcess == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();
                    throw new System.ComponentModel.Win32Exception(error);
                }

                if (functionAddress == IntPtr.Zero)
                {
                    throw new ArgumentException("函數地址不能為零");
                }

                IntPtr threadId;
                hThread = CreateRemoteThread(hProcess, IntPtr.Zero, 0, functionAddress, parameter, 0, out threadId);

                if (hThread == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();
                    throw new System.ComponentModel.Win32Exception(error);
                }

                uint waitResult = WaitForSingleObject(hThread, 5000);
                if (waitResult != 0)
                {
                    if (waitResult == 0xFFFFFFFF)
                    {
                        int error = Marshal.GetLastWin32Error();
                        throw new System.ComponentModel.Win32Exception(error);
                    }
                    throw new TimeoutException();
                }

                IntPtr exitCode;
                if (GetExitCodeThread(hThread, out exitCode))
                {
                    return exitCode.ToInt64() != 0;
                }

                return false;
            }
            finally
            {
                if (hThread != IntPtr.Zero)
                {
                    CloseHandle(hThread);
                }
                if (hProcess != IntPtr.Zero)
                {
                    CloseHandle(hProcess);
                }
            }
        }

        /// <summary>
        /// 呼叫遠端進程中的函數並讀取 DWORD 返回值
        /// </summary>
        public static uint CallRemoteFunctionReturnDword(int processId, string dllName, string functionName, IntPtr parameter)
        {
            IntPtr hProcess = IntPtr.Zero;
            IntPtr hThread = IntPtr.Zero;

            try
            {
                hProcess = OpenProcess(PROCESS_CREATE_THREAD | PROCESS_QUERY_INFORMATION | PROCESS_VM_OPERATION | PROCESS_VM_WRITE | PROCESS_VM_READ, false, processId);

                if (hProcess == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();
                    throw new System.ComponentModel.Win32Exception(error);
                }

                IntPtr hModule = QueryDllModuleHandle(hProcess, dllName);
                if (hModule == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();
                    if (error == 0)
                    {
                        error = 126;
                    }
                    throw new System.ComponentModel.Win32Exception(error);
                }

                IntPtr pRemoteFunction = QueryRemoteFunctionAddress(hProcess, hModule, functionName);

                IntPtr threadId;
                hThread = CreateRemoteThread(hProcess, IntPtr.Zero, 0, pRemoteFunction, parameter, 0, out threadId);

                if (hThread == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();
                    throw new System.ComponentModel.Win32Exception(error);
                }

                uint waitResult = WaitForSingleObject(hThread, 5000);
                if (waitResult != 0)
                {
                    if (waitResult == 0xFFFFFFFF)
                    {
                        int error = Marshal.GetLastWin32Error();
                        throw new System.ComponentModel.Win32Exception(error);
                    }
                    throw new TimeoutException();
                }

                IntPtr exitCode;
                if (GetExitCodeThread(hThread, out exitCode))
                {
                    return (uint)exitCode.ToInt64();
                }

                return 0;
            }
            finally
            {
                if (hThread != IntPtr.Zero)
                {
                    CloseHandle(hThread);
                }
                if (hProcess != IntPtr.Zero)
                {
                    CloseHandle(hProcess);
                }
            }
        }

        /// <summary>
        /// 呼叫遠端進程中的 QueryDllVersionString 函數並讀取版本字串
        /// </summary>
        public static string CallRemoteFunctionGetVersionString(int processId, string dllName)
        {
            IntPtr hProcess = IntPtr.Zero;
            IntPtr hThread = IntPtr.Zero;
            IntPtr pRemoteBuffer = IntPtr.Zero;
            IntPtr pRemoteParams = IntPtr.Zero;

            try
            {
                hProcess = OpenProcess(PROCESS_CREATE_THREAD | PROCESS_QUERY_INFORMATION | PROCESS_VM_OPERATION | PROCESS_VM_WRITE | PROCESS_VM_READ, false, processId);

                if (hProcess == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();
                    throw new System.ComponentModel.Win32Exception(error);
                }

                IntPtr hModule = QueryDllModuleHandle(hProcess, dllName);
                if (hModule == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();
                    if (error == 0)
                    {
                        error = 126;
                    }
                    throw new System.ComponentModel.Win32Exception(error);
                }

                const int bufferSize = 64;
                pRemoteBuffer = VirtualAllocEx(hProcess, IntPtr.Zero, (uint)(bufferSize * 2), MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
                if (pRemoteBuffer == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();
                    throw new System.ComponentModel.Win32Exception(error);
                }

                int paramSize = IntPtr.Size + sizeof(int);
                pRemoteParams = VirtualAllocEx(hProcess, IntPtr.Zero, (uint)paramSize, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
                if (pRemoteParams == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();
                    throw new System.ComponentModel.Win32Exception(error);
                }

                byte[] paramBytes = new byte[paramSize];
                IntPtr bufferPtr = pRemoteBuffer;
                if (IntPtr.Size == 8)
                {
                    BitConverter.GetBytes(bufferPtr.ToInt64()).CopyTo(paramBytes, 0);
                    BitConverter.GetBytes(bufferSize).CopyTo(paramBytes, 8);
                }
                else
                {
                    BitConverter.GetBytes(bufferPtr.ToInt32()).CopyTo(paramBytes, 0);
                    BitConverter.GetBytes(bufferSize).CopyTo(paramBytes, 4);
                }

                UIntPtr bytesWritten;
                if (!WriteProcessMemory(hProcess, pRemoteParams, paramBytes, (uint)paramSize, out bytesWritten))
                {
                    int error = Marshal.GetLastWin32Error();
                    throw new System.ComponentModel.Win32Exception(error);
                }

                IntPtr pRemoteFunction = QueryRemoteFunctionAddress(hProcess, hModule, "ThreadWrapper_GetDllVersionString");

                IntPtr threadId;
                hThread = CreateRemoteThread(hProcess, IntPtr.Zero, 0, pRemoteFunction, pRemoteParams, 0, out threadId);

                if (hThread == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();
                    throw new System.ComponentModel.Win32Exception(error);
                }

                uint waitResult = WaitForSingleObject(hThread, 5000);
                if (waitResult != 0)
                {
                    int error = Marshal.GetLastWin32Error();
                    throw new System.ComponentModel.Win32Exception(error);
                }

                IntPtr exitCode;
                if (!GetExitCodeThread(hThread, out exitCode))
                {
                    int error = Marshal.GetLastWin32Error();
                    throw new System.ComponentModel.Win32Exception(error);
                }

                byte[] versionBytes = new byte[bufferSize * 2];
                UIntPtr bytesRead;
                if (!ReadProcessMemory(hProcess, pRemoteBuffer, versionBytes, (uint)versionBytes.Length, out bytesRead))
                {
                    int error = Marshal.GetLastWin32Error();
                    throw new System.ComponentModel.Win32Exception(error);
                }

                return Encoding.Unicode.GetString(versionBytes).TrimEnd('\0');
            }
            finally
            {
                if (hThread != IntPtr.Zero)
                {
                    CloseHandle(hThread);
                }
                if (pRemoteParams != IntPtr.Zero && hProcess != IntPtr.Zero)
                {
                    VirtualFreeEx(hProcess, pRemoteParams, 0, MEM_RELEASE);
                }
                if (pRemoteBuffer != IntPtr.Zero && hProcess != IntPtr.Zero)
                {
                    VirtualFreeEx(hProcess, pRemoteBuffer, 0, MEM_RELEASE);
                }
                if (hProcess != IntPtr.Zero)
                {
                    CloseHandle(hProcess);
                }
            }
        }

        /// <summary>
        /// 呼叫遠端進程中的腳本執行函數
        /// </summary>
        public static bool CallRemoteScript(int processId, string dllName, string script)
        {
            IntPtr hProcess = IntPtr.Zero;
            IntPtr hThread = IntPtr.Zero;
            IntPtr pRemoteScript = IntPtr.Zero;

            try
            {
                uint accessRights = PROCESS_CREATE_THREAD | PROCESS_QUERY_INFORMATION | PROCESS_VM_OPERATION | PROCESS_VM_WRITE | PROCESS_VM_READ;

                hProcess = OpenProcess(accessRights, false, processId);
                if (hProcess == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();
                    throw new System.ComponentModel.Win32Exception(error);
                }

                IntPtr hModule = QueryDllModuleHandle(hProcess, dllName);
                if (hModule == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();
                    if (error == 0)
                    {
                        error = 126;
                    }
                    throw new System.ComponentModel.Win32Exception(error);
                }

                IntPtr hLocalModule = IntPtr.Zero;
                StringBuilder dllPath = new StringBuilder(260);
                string fullDllPath = null;

                if (GetModuleFileNameEx(hProcess, hModule, dllPath, (uint)dllPath.Capacity) > 0)
                {
                    fullDllPath = dllPath.ToString();
                }

                IntPtr hOldModule = GetModuleHandle(dllName);
                if (hOldModule != IntPtr.Zero)
                {
                    FreeLibrary(hOldModule);
                }

                if (!string.IsNullOrEmpty(fullDllPath))
                {
                    hLocalModule = LoadLibrary(fullDllPath);
                }

                if (hLocalModule == IntPtr.Zero)
                {
                    if (!string.IsNullOrEmpty(fullDllPath))
                    {
                        hLocalModule = LoadLibrary(fullDllPath);
                    }
                    else
                    {
                        hLocalModule = LoadLibrary(dllName);
                    }

                    if (hLocalModule == IntPtr.Zero)
                    {
                        int error = Marshal.GetLastWin32Error();
                        throw new System.ComponentModel.Win32Exception(error);
                    }
                }

                IntPtr pFunction = GetProcAddress(hLocalModule, "ThreadWrapper_ExecuteScript");
                if (pFunction == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();
                    throw new System.ComponentModel.Win32Exception(error);
                }

                long offset = pFunction.ToInt64() - hLocalModule.ToInt64();
                IntPtr pRemoteFunction = new IntPtr(hModule.ToInt64() + offset);

                byte[] scriptBytes = Encoding.Unicode.GetBytes(script + "\0");
                uint scriptSize = (uint)scriptBytes.Length;

                pRemoteScript = VirtualAllocEx(hProcess, IntPtr.Zero, scriptSize, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
                if (pRemoteScript == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();
                    throw new System.ComponentModel.Win32Exception(error);
                }

                UIntPtr bytesWritten;
                if (!WriteProcessMemory(hProcess, pRemoteScript, scriptBytes, scriptSize, out bytesWritten))
                {
                    int error = Marshal.GetLastWin32Error();
                    throw new System.ComponentModel.Win32Exception(error);
                }

                IntPtr threadId;
                hThread = CreateRemoteThread(hProcess, IntPtr.Zero, 0, pRemoteFunction, pRemoteScript, 0, out threadId);

                if (hThread == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();
                    throw new System.ComponentModel.Win32Exception(error);
                }

                uint waitResult = WaitForSingleObject(hThread, 5000);
                if (waitResult != 0)
                {
                    if (waitResult == 0xFFFFFFFF)
                    {
                        int error = Marshal.GetLastWin32Error();
                        throw new System.ComponentModel.Win32Exception(error);
                    }
                    throw new TimeoutException();
                }

                IntPtr exitCode;
                if (GetExitCodeThread(hThread, out exitCode))
                {
                    return exitCode != IntPtr.Zero;
                }

                return false;
            }
            catch
            {
                throw;
            }
            finally
            {
                if (hThread != IntPtr.Zero)
                {
                    CloseHandle(hThread);
                }
                if (pRemoteScript != IntPtr.Zero && hProcess != IntPtr.Zero)
                {
                    VirtualFreeEx(hProcess, pRemoteScript, 0, MEM_RELEASE);
                }
                if (hProcess != IntPtr.Zero)
                {
                    CloseHandle(hProcess);
                }
            }
        }

        // ========== 公開方法：InSpecHookDLL 封裝 ==========

        /// <summary>
        /// 設定目標進程 ID（用於 InSpecHookDLL 操作）
        /// </summary>
        public static void SetTargetProcessId(int processId)
        {
            targetProcessId = processId;
        }

        /// <summary>
        /// 取得本機 DLL 版本（直接從本機載入的 DLL）
        /// </summary>
        public static uint QueryDllVersion(int processId)
        {
            try
            {
                IntPtr hLocalModule = GetModuleHandle(DLL_NAME);
                if (hLocalModule == IntPtr.Zero)
                {
                    return 0;
                }

                IntPtr pGetVersion = GetProcAddress(hLocalModule, "QueryDllVersion");
                if (pGetVersion == IntPtr.Zero)
                {
                    return 0;
                }

                GetDllVersionDelegate getVersion = Marshal.GetDelegateForFunctionPointer<GetDllVersionDelegate>(pGetVersion);
                return getVersion();
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 取得本機 DLL 版本字串（直接從本機載入的 DLL）
        /// </summary>
        public static string QueryDllVersionString(int processId)
        {
            try
            {
                IntPtr hLocalModule = GetModuleHandle(DLL_NAME);
                if (hLocalModule == IntPtr.Zero)
                {
                    return null;
                }

                IntPtr pGetVersionString = GetProcAddress(hLocalModule, "QueryDllVersionString");
                if (pGetVersionString == IntPtr.Zero)
                {
                    return null;
                }

                GetDllVersionStringDelegate getVersionString = Marshal.GetDelegateForFunctionPointer<GetDllVersionStringDelegate>(pGetVersionString);
                StringBuilder buffer = new StringBuilder(32);
                getVersionString(buffer, buffer.Capacity);
                return buffer.ToString();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 取得遠端 DLL 版本（從已注入的進程中讀取）
        /// </summary>
        public static uint QueryRemoteDllVersion()
        {
            if (targetProcessId == 0)
            {
                return 0;
            }

            return CallRemoteFunctionReturnDword(targetProcessId, DLL_NAME, "ThreadWrapper_GetDllVersion", IntPtr.Zero);
        }

        /// <summary>
        /// 取得遠端 DLL 版本字串（從已注入的進程中讀取）
        /// </summary>
        public static string QueryRemoteDllVersionString()
        {
            if (targetProcessId == 0)
            {
                return null;
            }

            return CallRemoteFunctionGetVersionString(targetProcessId, DLL_NAME);
        }

        /// <summary>
        /// 執行遠端腳本（透過已注入的 DLL）
        /// </summary>
        public static bool ExecuteScript(string script)
        {
            if (targetProcessId == 0)
            {
                throw new InvalidOperationException();
            }

            return CallRemoteScript(targetProcessId, DLL_NAME, script);
        }

        /// <summary>
        /// 啟用 API Hook（透過已注入的 DLL）
        /// </summary>
        public static bool EnableApiHook()
        {
            if (targetProcessId == 0)
            {
                throw new InvalidOperationException();
            }

            return CallRemoteFunction(targetProcessId, DLL_NAME, "ThreadWrapper_EnableApiHook", IntPtr.Zero);
        }

        /// <summary>
        /// 停用 API Hook（透過已注入的 DLL）
        /// </summary>
        public static bool DisableApiHook()
        {
            if (targetProcessId == 0)
            {
                throw new InvalidOperationException();
            }

            return CallRemoteFunction(targetProcessId, DLL_NAME, "ThreadWrapper_DisableApiHook", IntPtr.Zero);
        }

        // ========== 內部方法 ==========

        /// <summary>
        /// 查找 DLL 模組句柄（在遠端進程中）
        /// </summary>
        /// <summary>
        /// 通過注入的 DLL 調用遠端函數地址
        /// </summary>
        public static bool CallRemoteFunctionByAddressViaDll(int processId, string dllName, string functionName, IntPtr functionAddress)
        {
            IntPtr hProcess = IntPtr.Zero;
            IntPtr hThread = IntPtr.Zero;
            IntPtr pRemoteAddress = IntPtr.Zero;

            try
            {
                hProcess = OpenProcess(PROCESS_CREATE_THREAD | PROCESS_QUERY_INFORMATION | PROCESS_VM_OPERATION | PROCESS_VM_WRITE | PROCESS_VM_READ, false, processId);

                if (hProcess == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();
                    throw new System.ComponentModel.Win32Exception(error);
                }

                IntPtr hModule = QueryDllModuleHandle(hProcess, dllName);
                if (hModule == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();
                    if (error == 0)
                    {
                        error = 126;
                    }
                    throw new System.ComponentModel.Win32Exception(error);
                }

                pRemoteAddress = VirtualAllocEx(hProcess, IntPtr.Zero, (uint)IntPtr.Size, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
                if (pRemoteAddress == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();
                    throw new System.ComponentModel.Win32Exception(error);
                }

                byte[] addressBytes = new byte[IntPtr.Size];
                if (IntPtr.Size == 8)
                {
                    BitConverter.GetBytes(functionAddress.ToInt64()).CopyTo(addressBytes, 0);
                }
                else
                {
                    BitConverter.GetBytes(functionAddress.ToInt32()).CopyTo(addressBytes, 0);
                }

                UIntPtr bytesWritten;
                if (!WriteProcessMemory(hProcess, pRemoteAddress, addressBytes, (uint)addressBytes.Length, out bytesWritten))
                {
                    int error = Marshal.GetLastWin32Error();
                    throw new System.ComponentModel.Win32Exception(error);
                }

                IntPtr pRemoteFunction = QueryRemoteFunctionAddress(hProcess, hModule, functionName);

                IntPtr threadId;
                hThread = CreateRemoteThread(hProcess, IntPtr.Zero, 0, pRemoteFunction, pRemoteAddress, 0, out threadId);

                if (hThread == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();
                    throw new System.ComponentModel.Win32Exception(error);
                }

                uint waitResult = WaitForSingleObject(hThread, 5000);
                if (waitResult != 0)
                {
                    if (waitResult == 0xFFFFFFFF)
                    {
                        int error = Marshal.GetLastWin32Error();
                        throw new System.ComponentModel.Win32Exception(error);
                    }
                    throw new TimeoutException();
                }

                IntPtr exitCode;
                if (GetExitCodeThread(hThread, out exitCode))
                {
                    return exitCode != IntPtr.Zero;
                }

                return false;
            }
            finally
            {
                if (hThread != IntPtr.Zero)
                {
                    CloseHandle(hThread);
                }
                if (pRemoteAddress != IntPtr.Zero && hProcess != IntPtr.Zero)
                {
                    VirtualFreeEx(hProcess, pRemoteAddress, 0, MEM_RELEASE);
                }
                if (hProcess != IntPtr.Zero)
                {
                    CloseHandle(hProcess);
                }
            }
        }

        /// <summary>
        /// 查找遠端進程中的 DLL 模組句柄（公共方法）
        /// </summary>
        public static IntPtr QueryDllModuleHandle(int processId, string dllName)
        {
            IntPtr hProcess = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, false, processId);
            if (hProcess == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }
            
            try
            {
                return QueryDllModuleHandle(hProcess, dllName);
            }
            finally
            {
                if (hProcess != IntPtr.Zero)
                {
                    CloseHandle(hProcess);
                }
            }
        }
        
        private static IntPtr QueryDllModuleHandle(IntPtr hProcess, string dllName)
        {
            IntPtr[] modules = new IntPtr[1024];
            uint needed;

            if (!EnumProcessModules(hProcess, modules, (uint)(modules.Length * IntPtr.Size), out needed))
            {
                return IntPtr.Zero;
            }

            uint moduleCount = needed / (uint)IntPtr.Size;
            StringBuilder modulePath = new StringBuilder(260);

            string dllNameLower = dllName.ToLowerInvariant();
            // 只有當不是 .exe 或 .dll 結尾時才添加 .dll
            if (!dllNameLower.EndsWith(".dll") && !dllNameLower.EndsWith(".exe"))
            {
                dllNameLower += ".dll";
            }

            for (uint i = 0; i < moduleCount; i++)
            {
                modulePath.Clear();
                if (GetModuleFileNameEx(hProcess, modules[i], modulePath, (uint)modulePath.Capacity) > 0)
                {
                    string path = modulePath.ToString();
                    string fileName = System.IO.Path.GetFileName(path).ToLowerInvariant();

                    if (fileName == dllNameLower || path.ToLowerInvariant().EndsWith(dllNameLower))
                    {
                        return modules[i];
                    }
                }
            }

            return IntPtr.Zero;
        }

        private static IntPtr QueryRemoteFunctionAddress(IntPtr hProcess, IntPtr hModule, string functionName)
        {
            byte[] dosHeader = new byte[64];
            UIntPtr bytesRead;

            if (!ReadProcessMemory(hProcess, hModule, dosHeader, 64, out bytesRead) || bytesRead.ToUInt32() < 64)
            {
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }

            if (dosHeader[0] != 0x4D || dosHeader[1] != 0x5A)
            {
                throw new InvalidOperationException("遠程 DLL 不是有效的 PE 文件");
            }

            uint peOffset = BitConverter.ToUInt32(dosHeader, 0x3C);

            byte[] peHeader = new byte[24];
            IntPtr peHeaderAddr = new IntPtr(hModule.ToInt64() + peOffset);
            if (!ReadProcessMemory(hProcess, peHeaderAddr, peHeader, 24, out bytesRead) || bytesRead.ToUInt32() < 24)
            {
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }

            if (peHeader[0] != 0x50 || peHeader[1] != 0x45 || peHeader[2] != 0x00 || peHeader[3] != 0x00)
            {
                throw new InvalidOperationException("遠程 DLL 不是有效的 PE 文件");
            }

            IntPtr optionalHeaderAddr = new IntPtr(peHeaderAddr.ToInt64() + 24);
            byte[] optionalHeader = new byte[100];
            if (!ReadProcessMemory(hProcess, optionalHeaderAddr, optionalHeader, 100, out bytesRead) || bytesRead.ToUInt32() < 100)
            {
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }

            uint exportTableRVA = BitConverter.ToUInt32(optionalHeader, 96);
            if (exportTableRVA == 0)
            {
                throw new InvalidOperationException("遠程 DLL 沒有導出表");
            }

            IntPtr exportTableAddr = new IntPtr(hModule.ToInt64() + exportTableRVA);
            byte[] exportTable = new byte[40];
            if (!ReadProcessMemory(hProcess, exportTableAddr, exportTable, 40, out bytesRead) || bytesRead.ToUInt32() < 40)
            {
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }

            uint numberOfFunctions = BitConverter.ToUInt32(exportTable, 20);
            uint numberOfNames = BitConverter.ToUInt32(exportTable, 24);
            uint addressOfFunctions = BitConverter.ToUInt32(exportTable, 28);
            uint addressOfNames = BitConverter.ToUInt32(exportTable, 32);
            uint addressOfNameOrdinals = BitConverter.ToUInt32(exportTable, 36);

            IntPtr namesAddr = new IntPtr(hModule.ToInt64() + addressOfNames);
            byte[] namesArray = new byte[(int)(numberOfNames * 4)];
            if (!ReadProcessMemory(hProcess, namesAddr, namesArray, (uint)namesArray.Length, out bytesRead))
            {
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }

            IntPtr ordinalsAddr = new IntPtr(hModule.ToInt64() + addressOfNameOrdinals);
            byte[] ordinalsArray = new byte[(int)(numberOfNames * 2)];
            if (!ReadProcessMemory(hProcess, ordinalsAddr, ordinalsArray, (uint)ordinalsArray.Length, out bytesRead))
            {
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }

            IntPtr functionsAddr = new IntPtr(hModule.ToInt64() + addressOfFunctions);
            byte[] functionsArray = new byte[(int)(numberOfFunctions * 4)];
            if (!ReadProcessMemory(hProcess, functionsAddr, functionsArray, (uint)functionsArray.Length, out bytesRead))
            {
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }

            System.Collections.Generic.List<string> exportedFunctions = new System.Collections.Generic.List<string>();

            for (uint i = 0; i < numberOfNames; i++)
            {
                int nameIndex = (int)(i * 4);
                uint nameRVA = BitConverter.ToUInt32(namesArray, nameIndex);
                IntPtr nameAddr = new IntPtr(hModule.ToInt64() + nameRVA);

                byte[] nameBytes = new byte[256];
                UIntPtr nameBytesRead;
                if (ReadProcessMemory(hProcess, nameAddr, nameBytes, 256, out nameBytesRead))
                {
                    int nameLength = Array.IndexOf(nameBytes, (byte)0);
                    if (nameLength < 0) nameLength = 256;

                    string name = System.Text.Encoding.ASCII.GetString(nameBytes, 0, nameLength);
                    exportedFunctions.Add(name);

                    if (name == functionName ||
                        name == "_" + functionName + "@4" ||
                        name == "_" + functionName + "@8" ||
                        name == "_" + functionName + "@12" ||
                        name == "_" + functionName + "@16")
                    {
                        int ordinalIndex = (int)(i * 2);
                        ushort ordinal = BitConverter.ToUInt16(ordinalsArray, ordinalIndex);

                        int functionIndex = (int)(ordinal * 4);
                        uint functionRVA = BitConverter.ToUInt32(functionsArray, functionIndex);

                        return new IntPtr(hModule.ToInt64() + functionRVA);
                    }
                }
            }

            string functionList = string.Join(", ", exportedFunctions);
            throw new InvalidOperationException($"在遠程 DLL 中找不到函數: {functionName}。已導出的函數: {functionList}");
        }

        /// <summary>
        /// 詳細檢測：嘗試在目前進程中載入指定 DLL，並輸出系統錯誤訊息
        /// </summary>
        private static void DiagnoseLocalDllLoad(string dllPath, Action<string> log)
        {
            if (log == null)
            {
                log = _ => { };
            }

            log("=== 詳細檢測：本機 LoadLibrary 測試開始 ===");
            log($"詳細檢測 DLL 路徑: {dllPath}");

            try
            {
                if (string.IsNullOrWhiteSpace(dllPath))
                {
                    log("✗ 詳細檢測：DLL 路徑為空，略過本機載入測試");
                    return;
                }

                if (!System.IO.File.Exists(dllPath))
                {
                    log("✗ 詳細檢測：DLL 檔案不存在，略過本機載入測試");
                    return;
                }

                IntPtr hModule = LoadLibrary(dllPath);
                if (hModule == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();
                    log($"✗ 詳細檢測：在目前進程中 LoadLibrary 失敗，錯誤代碼 {error} (0x{error:X8})");
                    try
                    {
                        var ex = new System.ComponentModel.Win32Exception(error);
                        log($"✗ 詳細檢測：系統錯誤訊息：{ex.Message}");
                    }
                    catch
                    {
                    }
                }
                else
                {
                    log($"✓ 詳細檢測：在目前進程中成功載入 DLL，模組句柄: 0x{hModule.ToInt64():X16}");
                    if (!FreeLibrary(hModule))
                    {
                        int error = Marshal.GetLastWin32Error();
                        log($"⚠ 詳細檢測：FreeLibrary 失敗，錯誤代碼 {error} (0x{error:X8})");
                    }
                }
            }
            finally
            {
                log("=== 詳細檢測：本機 LoadLibrary 測試結束 ===");
            }
        }
    }
}

