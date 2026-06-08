using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace NetToolTestArea.Utility
{
    /// <summary>
    /// 記憶體注入工具類
    /// </summary>
    public class MemoryInjectionUtility
    {
        public sealed class InjectionResult
        {
            public bool Success { get; set; }
            public string VersionDisplay { get; set; }
            public string ErrorMessage { get; set; }
        }

        public sealed class TestInjectionResult
        {
            public bool Success { get; set; }
            public int ProcessId { get; set; }
            public string ErrorMessage { get; set; }
        }

        public sealed class InSpecTargetInfo
        {
            public int ProcessId { get; set; }
            public string WindowTitle { get; set; }
            public string ProcessPath { get; set; }
        }

        private bool isInjected = false;
        private int targetProcessId = 0;
        private string injectedDllPath = null;

        /// <summary>
        /// 查找 InSpec 進程 ID
        /// </summary>
        public int FindInSpecProcessId()
        {
            int processId;
            string windowTitle;
            string processPath;

            bool found = TryGetInSpecTarget(out processId, out windowTitle, out processPath);
            if (!found)
            {
                return 0;
            }

            targetProcessId = processId;
            return targetProcessId;
        }

        public bool TryGetInSpecTarget(out int processId, out string windowTitle, out string processPath)
        {
            processId = 0;
            windowTitle = null;
            processPath = null;

            Process[] processes = Process.GetProcessesByName("InSpec");
            if (processes == null || processes.Length <= 0)
            {
                return false;
            }

            // 優先選擇「有視窗」且標題符合「InSpec 視窗版 - *」的 InSpec 程序
            const string preferredWindowTitleKeyword = "InSpec 視窗版";

            foreach (var process in processes)
            {
                try
                {
                    if (process == null || process.HasExited || process.MainWindowHandle == IntPtr.Zero)
                    {
                        continue;
                    }

                    string title = process.MainWindowTitle ?? string.Empty;
                    if (title.IndexOf(preferredWindowTitleKeyword, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    processId = process.Id;
                    windowTitle = title;

                    try
                    {
                        processPath = process.MainModule?.FileName;
                    }
                    catch
                    {
                        processPath = null;
                    }

                    return true;
                }
                catch
                {
                    // 忽略個別程序讀取失敗
                }
            }

            return false;
        }

        public string QueryDefaultHookDllPath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Test", "InSpecHookDLL.dll");
        }

        public string QueryDefaultFunctionScanDllPath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Test", "InSpecFunctionScanDLL.dll");
        }

        /// <summary>
        /// 確保 InSpec DLL 已注入
        /// </summary>
        public bool TryEnsureInSpecDllInjected(string dllPath, out int processId, out string errorMessage)
        {
            processId = 0;
            errorMessage = null;

            processId = FindInSpecProcessId();
            if (processId <= 0)
            {
                errorMessage = "未找到 InSpec 程序";
                return false;
            }

            if (string.IsNullOrWhiteSpace(dllPath))
            {
                errorMessage = "DLL 路徑為空";
                return false;
            }

            if (!File.Exists(dllPath))
            {
                errorMessage = $"DLL 檔案不存在：{dllPath}";
                return false;
            }

            // 如果已注入，僅同步狀態
            if (Core.DllCore.IsDllInjected(processId, "InSpecHookDLL.dll"))
            {
                SetInjected(processId, dllPath);
                Core.DllCore.SetTargetProcessId(processId);
                return true;
            }

            try
            {
                bool success = InjectDLL(processId, dllPath);
                if (!success)
                {
                    errorMessage = "DLL 注入失敗";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"DLL 注入失敗：{ex.GetType().Name} - {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// 注入 DLL 到指定進程
        /// </summary>
        public bool InjectDLL(int processId, string dllPath)
        {
            Process.GetProcessById(processId);

            // 先嘗試使用 Hook 方式
            try
            {
                Core.DllCore.InjectDLLByHook("InSpec", dllPath);
                
                targetProcessId = processId;
                injectedDllPath = dllPath;
                isInjected = true;
                
                Core.DllCore.SetTargetProcessId(processId);
                
                System.Threading.Thread.Sleep(1000);
                
                if (!Core.DllCore.IsDllInjected(processId, "InSpecHookDLL.dll"))
                {
                    System.Threading.Thread.Sleep(1000);
                    if (!Core.DllCore.IsDllInjected(processId, "InSpecHookDLL.dll"))
                    {
                        throw new InvalidOperationException();
                    }
                }

                return true;
            }
            catch
            {
                Core.DllCore.InjectDLL(processId, dllPath);
                
                targetProcessId = processId;
                injectedDllPath = dllPath;
                isInjected = true;
                
                Core.DllCore.SetTargetProcessId(processId);
                
                System.Threading.Thread.Sleep(1000);
                
                if (!Core.DllCore.IsDllInjected(processId, "InSpecHookDLL.dll"))
                {
                    System.Threading.Thread.Sleep(1000);
                    if (!Core.DllCore.IsDllInjected(processId, "InSpecHookDLL.dll"))
                    {
                        throw new InvalidOperationException();
                    }
                }

                return true;
            }
        }

        /// <summary>
        /// 嘗試注入 DLL 到指定進程
        /// </summary>
        public InjectionResult TryInjectDllToProcess(int processId, string dllPath, Action<string> logCallback)
        {
            if (processId <= 0)
            {
                return new InjectionResult { Success = false, ErrorMessage = "無效的程序 ID" };
            }

            if (string.IsNullOrWhiteSpace(dllPath))
            {
                return new InjectionResult { Success = false, ErrorMessage = "DLL 路徑為空" };
            }

            if (!File.Exists(dllPath))
            {
                return new InjectionResult { Success = false, ErrorMessage = $"DLL 文件不存在: {dllPath}" };
            }

            try
            {
                Core.DllCore.InjectDLL(processId, dllPath, logCallback);

                System.Threading.Thread.Sleep(1000);
                if (!Core.DllCore.IsDllInjected(processId, "InSpecHookDLL.dll"))
                {
                    System.Threading.Thread.Sleep(1000);
                    if (!Core.DllCore.IsDllInjected(processId, "InSpecHookDLL.dll"))
                    {
                        return new InjectionResult { Success = false, ErrorMessage = "DLL 注入成功但驗證失敗" };
                    }
                }

                Core.DllCore.SetTargetProcessId(processId);
                System.Threading.Thread.Sleep(500);

                SetInjected(processId, dllPath);
                return new InjectionResult { Success = true, VersionDisplay = null };
            }
            catch (Exception ex)
            {
                return new InjectionResult { Success = false, ErrorMessage = $"{ex.GetType().Name} - {ex.Message}" };
            }
        }

        /// <summary>
        /// 嘗試測試注入到 Notepad
        /// </summary>
        public TestInjectionResult TryTestInjectToNotepad(string dllPath, Action<string> logCallback)
        {
            if (string.IsNullOrWhiteSpace(dllPath))
            {
                return new TestInjectionResult { Success = false, ErrorMessage = "請先選擇 DLL 文件" };
            }

            if (!File.Exists(dllPath))
            {
                return new TestInjectionResult { Success = false, ErrorMessage = $"DLL 文件不存在: {dllPath}" };
            }

            Process testProcess = null;
            try
            {
                testProcess = Process.Start("notepad.exe");
                if (testProcess == null)
                {
                    return new TestInjectionResult { Success = false, ErrorMessage = "無法啟動 Notepad 測試進程" };
                }

                System.Threading.Thread.Sleep(500);

                int pid = testProcess.Id;
                Core.DllCore.InjectDLL(pid, dllPath, logCallback);

                System.Threading.Thread.Sleep(1000);
                bool injected = Core.DllCore.IsDllInjected(pid, "InSpecHookDLL.dll");
                if (!injected)
                {
                    return new TestInjectionResult { Success = false, ProcessId = pid, ErrorMessage = "DLL 注入成功但驗證失敗" };
                }

                return new TestInjectionResult { Success = true, ProcessId = pid };
            }
            catch (Exception ex)
            {
                return new TestInjectionResult { Success = false, ErrorMessage = $"{ex.GetType().Name} - {ex.Message}" };
            }
            finally
            {
                // 若失敗則嘗試關閉測試進程，避免留殘
                if (testProcess != null && testProcess.HasExited == false)
                {
                    // 成功時不關閉，沿用原行為
                }
            }
        }

        /// <summary>
        /// 測試 DLL 注入功能（注入到 Notepad 進程）
        /// </summary>
        public int TestDllInjection(string dllPath)
        {
            Process testProcess = Process.Start("notepad.exe");
            if (testProcess == null)
            {
                throw new InvalidOperationException("無法啟動 Notepad 測試進程");
            }

            System.Threading.Thread.Sleep(500);

            try
            {
                Core.DllCore.InjectDLL(testProcess.Id, dllPath);
                
                System.Threading.Thread.Sleep(1000);
                
                bool isInjected = Core.DllCore.IsDllInjected(testProcess.Id, "InSpecHookDLL.dll");
                
                if (isInjected)
                {
                    return testProcess.Id;
                }
                else
                {
                    throw new InvalidOperationException("DLL 注入成功但驗證失敗");
                }
            }
            catch
            {
                try
                {
                    testProcess.Kill();
                }
                catch
                {
                    // 清理失敗不影響主要錯誤傳遞
                }
                throw;
            }
        }

        /// <summary>
        /// 執行腳本
        /// </summary>
        public bool ExecuteScript(string script)
        {
            return Core.DllCore.ExecuteScript(script);
        }

        /// <summary>
        /// 使用函數地址調用遠端函數
        /// </summary>
        public bool CallFunctionByAddress(int processId, IntPtr functionAddress, IntPtr parameter)
        {
            return Core.DllCore.CallRemoteFunctionByAddress(processId, functionAddress, parameter);
        }

        public void SetInjected(int processId, string dllPath)
        {
            targetProcessId = processId;
            injectedDllPath = dllPath;
            isInjected = true;
        }

        /// <summary>
        /// 是否已注入
        /// </summary>
        public bool IsInjected => isInjected;

        /// <summary>
        /// 目標進程 ID
        /// </summary>
        public int TargetProcessId => targetProcessId;
    }
}

