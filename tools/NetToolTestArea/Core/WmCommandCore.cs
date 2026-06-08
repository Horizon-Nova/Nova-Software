using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace NetToolTestArea.Core
{
    public static class WmCommandCore
    {
        private const uint WM_COMMAND = 0x0111;
        private const uint BM_CLICK = 0x00F5;
        
        // InSpec Run 按鈕的 Command ID（從 output.txt 分析得出）
        private const int INSPEC_RUN_COMMAND_ID = 0x000080B5;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern int GetDlgCtrlID(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetDlgItem(IntPtr hDlg, int nIDDlgItem);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        public static int QueryControlCommandId(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero)
            {
                return -1;
            }

            try
            {
                int id = GetDlgCtrlID(hWnd);
                return id > 0 ? id : -1;
            }
            catch
            {
                return -1;
            }
        }

        /// <summary>
        /// 通過 WM_COMMAND ID 查找對話框中的控件（使用 GetDlgItem）
        /// </summary>
        public static IntPtr FindControlByCommandId(IntPtr dialogHandle, int commandId)
        {
            if (dialogHandle == IntPtr.Zero || commandId <= 0)
            {
                return IntPtr.Zero;
            }

            try
            {
                IntPtr controlHandle = GetDlgItem(dialogHandle, commandId);
                return controlHandle != IntPtr.Zero ? controlHandle : IntPtr.Zero;
            }
            catch
            {
                return IntPtr.Zero;
            }
        }

        /// <summary>
        /// 發送 WM_COMMAND 訊息（使用 SendMessage）
        /// </summary>
        public static bool SendWMCommand(IntPtr hWnd, int commandId)
        {
            if (hWnd == IntPtr.Zero || commandId <= 0)
            {
                return false;
            }

            try
            {
                IntPtr result = SendMessage(hWnd, WM_COMMAND, new IntPtr(commandId), IntPtr.Zero);
                return result != IntPtr.Zero;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 發送 WM_COMMAND 訊息（使用 PostMessage，避免被 modal 對話框阻塞）
        /// </summary>
        public static bool PostWMCommand(IntPtr hWnd, int commandId)
        {
            if (hWnd == IntPtr.Zero || commandId <= 0)
            {
                return false;
            }

            try
            {
                return PostMessage(hWnd, WM_COMMAND, new IntPtr(commandId), IntPtr.Zero);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 對 dialog 內的 Button 送 BM_CLICK 和 WM_COMMAND，確保真正觸發按鈕行為
        /// </summary>
        public static bool ClickDialogButtonById(IntPtr dialogHandle, int buttonId)
        {
            if (dialogHandle == IntPtr.Zero || buttonId <= 0)
            {
                return false;
            }

            IntPtr btn = FindControlByCommandId(dialogHandle, buttonId);
            if (btn == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                SetForegroundWindow(dialogHandle);
                System.Threading.Thread.Sleep(100);

                PostMessage(btn, BM_CLICK, IntPtr.Zero, IntPtr.Zero);
                PostMessage(dialogHandle, WM_COMMAND, new IntPtr(buttonId), btn);

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 發送 WM_COMMAND 訊息（支持 lParam）
        /// </summary>
        public static IntPtr SendWMCommandWithLParam(IntPtr hWnd, int commandId, IntPtr lParam)
        {
            if (hWnd == IntPtr.Zero || commandId <= 0)
            {
                return IntPtr.Zero;
            }

            try
            {
                return SendMessage(hWnd, WM_COMMAND, new IntPtr(commandId), lParam);
            }
            catch
            {
                return IntPtr.Zero;
            }
        }

        /// <summary>
        /// 查找 InSpec 主窗口句柄
        /// </summary>
        public static IntPtr FindInSpecMainWindow()
        {
            IntPtr foundWindow = IntPtr.Zero;
            const string INSPEC_WINDOW_KEYWORD = "InSpec 視窗版";

            EnumWindows((hWnd, lParam) =>
            {
                if (!IsWindowVisible(hWnd))
                {
                    return true;
                }

                int length = GetWindowTextLength(hWnd);
                if (length > 0)
                {
                    StringBuilder title = new StringBuilder(length + 1);
                    GetWindowText(hWnd, title, title.Capacity);
                    string windowTitle = title.ToString();

                    // 排除自己的窗口
                    if (windowTitle.IndexOf("NetTool", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        windowTitle.IndexOf("測試區域", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }

                    // 查找包含 "InSpec 視窗版" 的窗口
                    if (windowTitle.IndexOf(INSPEC_WINDOW_KEYWORD, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        // 確認是 InSpec 進程的窗口
                        uint processId;
                        GetWindowThreadProcessId(hWnd, out processId);
                        try
                        {
                            Process process = Process.GetProcessById((int)processId);
                            if (process.ProcessName.Equals("InSpec", StringComparison.OrdinalIgnoreCase))
                            {
                                foundWindow = hWnd;
                                return false; // 停止枚舉
                            }
                        }
                        catch
                        {
                            // 忽略進程讀取失敗
                        }
                    }
                }
                return true;
            }, IntPtr.Zero);

            return foundWindow;
        }

        /// <summary>
        /// 觸發 InSpec Run 按鈕（自動查找窗口並發送 WM_COMMAND）
        /// </summary>
        public static bool TriggerInSpecRun(out string errorMessage)
        {
            errorMessage = null;

            // 1. 查找 InSpec 主窗口
            IntPtr mainWindow = FindInSpecMainWindow();
            if (mainWindow == IntPtr.Zero)
            {
                errorMessage = "未找到 InSpec 主窗口。請確認 InSpec 應用程式已開啟。";
                return false;
            }

            // 2. 嘗試找到 Run 按鈕句柄（作為 lParam）
            IntPtr runButtonHandle = FindControlByCommandId(mainWindow, INSPEC_RUN_COMMAND_ID);
            
            // 3. 發送 WM_COMMAND（使用與 output.txt 相同的格式）
            IntPtr lParam = runButtonHandle != IntPtr.Zero ? runButtonHandle : IntPtr.Zero;
            IntPtr result = SendWMCommandWithLParam(mainWindow, INSPEC_RUN_COMMAND_ID, lParam);

            if (result == IntPtr.Zero)
            {
                errorMessage = $"發送 WM_COMMAND 失敗。返回值: {result}";
                return false;
            }

            return true;
        }
    }
}

