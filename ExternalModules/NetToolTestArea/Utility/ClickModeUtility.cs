using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using NetToolTestArea.Model;

namespace NetToolTestArea.Utility
{
    /// <summary>
    /// 點擊模式工具類
    /// </summary>
    public class ClickModeUtility
    {
        // Windows API 常數
        private const int WH_MOUSE_LL = 14;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int GWL_STYLE = -16;
        private const int WS_VISIBLE = 0x10000000;

        // InSpec 視窗標題關鍵字
        private const string INSPEC_WINDOW_KEYWORD = "InSpec";
        private const string INSPEC_WINDOW_KEYWORD_FULL = "InSpec for Windows";
        private const string INSPEC_WINDOW_KEYWORD_VERSION = "InSpec 視窗版";

        // 排除的文字（用於文字過濾）
        private static readonly HashSet<string> ExcludedTexts = new HashSet<string>
        {
            "啟用點擊模式",
            "停用點擊模式",
            "就緒",
            "NetTool",
            "測試區域"
        };

        // Windows API 函數
        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(POINT Point);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        private IntPtr hookId = IntPtr.Zero;
        private LowLevelMouseProc proc;
        private Action<ControlInfo> onControlSelected;

        public bool IsClickModeActive => hookId != IntPtr.Zero;

        /// <summary>
        /// 開始點擊模式
        /// </summary>
        public void StartClickMode(Action<ControlInfo> onControlSelected)
        {
            if (hookId != IntPtr.Zero)
            {
                StopClickMode();
            }

            this.onControlSelected = onControlSelected;
            proc = HookCallback;
            hookId = SetWindowsHookEx(WH_MOUSE_LL, proc, GetModuleHandle(null), 0);
        }

        /// <summary>
        /// 停止點擊模式
        /// </summary>
        public void StopClickMode()
        {
            if (hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(hookId);
                hookId = IntPtr.Zero;
            }
            onControlSelected = null;
        }

        /// <summary>
        /// 鼠標鉤子回調
        /// </summary>
        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_LBUTTONDOWN)
            {
                GetCursorPos(out POINT point);
                IntPtr hWnd = WindowFromPoint(point);

                if (hWnd != IntPtr.Zero)
                {
                    ControlInfo info = QueryControlInfo(hWnd);
                    onControlSelected?.Invoke(info);
                }
            }

            return CallNextHookEx(hookId, nCode, wParam, lParam);
        }

        public ControlInfo QueryControlInfo(IntPtr hWnd)
        {
            ControlInfo info = new ControlInfo
            {
                Handle = hWnd,
                WindowTitle = Core.WindowApiCore.QueryWindowTitle(hWnd),
                ClassName = Core.WindowApiCore.QueryControlClassName(hWnd),
                Text = Core.WindowApiCore.ReadControlText(hWnd),
                CommandId = Core.WmCommandCore.QueryControlCommandId(hWnd)
            };

            if (string.IsNullOrEmpty(info.Text))
            {
                info.ChildControls = GetChildControls(hWnd);
            }

            return info;
        }

        private List<ControlInfo> GetChildControls(IntPtr parentHandle)
        {
            List<ControlInfo> childControls = new List<ControlInfo>();

            EnumChildWindows(parentHandle, (hWnd, lParam) =>
            {
                string className = Core.WindowApiCore.QueryControlClassName(hWnd);
                string text = Core.WindowApiCore.ReadControlText(hWnd);

                if (!string.IsNullOrEmpty(text) || 
                    className.ToLower().Contains("edit") ||
                    className.ToLower().Contains("static"))
                {
                    childControls.Add(new ControlInfo
                    {
                        Handle = hWnd,
                        ClassName = className,
                        Text = text,
                        CommandId = Core.WmCommandCore.QueryControlCommandId(hWnd)
                    });
                }

                return true;
            }, IntPtr.Zero);

            return childControls;
        }

        public List<ControlInfo> QueryAllControls(IntPtr windowHandle)
        {
            List<ControlInfo> allControls = new List<ControlInfo>();
            GetAllControlsRecursive(windowHandle, allControls, 0);
            return allControls;
        }

        private void GetAllControlsRecursive(IntPtr parentHandle, List<ControlInfo> controls, int depth)
        {
            EnumChildWindows(parentHandle, (hWnd, lParam) =>
            {
                controls.Add(new ControlInfo
                {
                    Handle = hWnd,
                    ClassName = Core.WindowApiCore.QueryControlClassName(hWnd),
                    Text = Core.WindowApiCore.ReadControlText(hWnd),
                    CommandId = Core.WmCommandCore.QueryControlCommandId(hWnd)
                });

                GetAllControlsRecursive(hWnd, controls, depth + 1);
                return true;
            }, IntPtr.Zero);
        }

        /// <summary>
        /// 從控制項向上查找容器
        /// </summary>
        public IntPtr FindContainerFromControl(IntPtr controlHandle)
        {
            const int maxDepth = 10;
            IntPtr current = controlHandle;

            for (int depth = 0; depth < maxDepth && current != IntPtr.Zero; depth++)
            {
                IntPtr parent = Core.WindowApiCore.QueryParentWindow(current);
                if (parent == IntPtr.Zero)
                {
                    break;
                }

                if (Core.WindowApiCore.IsContainerType(parent))
                {
                    return parent;
                }

                current = parent;
            }

            return Core.WindowApiCore.QueryParentWindow(controlHandle);
        }

        /// <summary>
        /// 查找容器內的所有可點擊控制項
        /// </summary>
        public List<ControlInfo> FindButtonsInContainer(IntPtr containerHandle)
        {
            List<ControlInfo> clickableControls = new List<ControlInfo>();

            if (containerHandle == IntPtr.Zero)
            {
                return clickableControls;
            }

            Core.WindowApiCore.EnumChildWindows(containerHandle, (hWnd) =>
            {
                if (Core.WindowApiCore.IsClickableControl(hWnd))
                {
                    clickableControls.Add(QueryControlInfo(hWnd));
                }
                return true;
            });

            return clickableControls;
        }

        public bool CheckIfControlBelongsToInSpec(IntPtr controlHandle, IntPtr inspecWindowHandle)
        {
            return inspecWindowHandle != IntPtr.Zero
                   && (Core.WindowApiCore.QueryTopLevelWindow(controlHandle) == inspecWindowHandle
                       || IsControlBelongsToWindow(controlHandle, inspecWindowHandle));
        }

        private bool IsControlBelongsToWindow(IntPtr controlHandle, IntPtr targetWindowHandle)
        {
            const int maxDepth = 20;
            IntPtr current = controlHandle;

            for (int depth = 0; depth < maxDepth && current != IntPtr.Zero; depth++)
            {
                if (current == targetWindowHandle)
                {
                    return true;
                }

                IntPtr parent = Core.WindowApiCore.QueryParentWindow(current);
                if (parent == IntPtr.Zero || parent == current)
                {
                    break;
                }

                current = parent;
            }

            return false;
        }

        /// <summary>
        /// 查找 InSpec 視窗
        /// </summary>
        public IntPtr FindInSpecWindow()
        {
            IntPtr foundWindow = IntPtr.Zero;
            IntPtr fallbackWindow = IntPtr.Zero;

            EnumWindows((hWnd, lParam) =>
            {
                if (IsWindowVisible(hWnd))
                {
                    int length = GetWindowTextLength(hWnd);
                    if (length > 0)
                    {
                        StringBuilder title = new StringBuilder(length + 1);
                        GetWindowText(hWnd, title, title.Capacity);
                        string windowTitle = title.ToString();

                        bool isExcluded = windowTitle.IndexOf("NetTool", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                         windowTitle.IndexOf("測試區域", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                         windowTitle.IndexOf("Cursor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                         windowTitle.IndexOf("Visual Studio", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                         windowTitle.IndexOf("VS Code", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                         windowTitle.IndexOf(".cs", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                         windowTitle.IndexOf(".java", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                         windowTitle.IndexOf("Chrome", StringComparison.OrdinalIgnoreCase) >= 0;

                        if (isExcluded)
                        {
                            return true;
                        }

                        if (windowTitle.IndexOf(INSPEC_WINDOW_KEYWORD_VERSION, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            foundWindow = hWnd;
                            return false;
                        }

                        if (foundWindow == IntPtr.Zero &&
                            windowTitle.IndexOf(INSPEC_WINDOW_KEYWORD_FULL, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            fallbackWindow = hWnd;
                        }

                        if (foundWindow == IntPtr.Zero &&
                            windowTitle.IndexOf(INSPEC_WINDOW_KEYWORD, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            fallbackWindow = hWnd;
                        }
                    }
                }
                return true;
            }, IntPtr.Zero);

            if (foundWindow != IntPtr.Zero)
            {
                return foundWindow;
            }

            if (fallbackWindow != IntPtr.Zero)
            {
                return fallbackWindow;
            }

            return FindInSpecWindowByClassName();
        }

        /// <summary>
        /// 通過類名查找 InSpec 視窗（備用方法）
        /// </summary>
        private IntPtr FindInSpecWindowByClassName()
        {
            IntPtr foundWindow = IntPtr.Zero;

            EnumWindows((hWnd, lParam) =>
            {
                if (IsWindowVisible(hWnd))
                {
                    string classNameStr = Core.WindowApiCore.QueryControlClassName(hWnd);

                    if (classNameStr == "InSpecAppFrame")
                    {
                        string windowTitle = Core.WindowApiCore.QueryWindowTitle(hWnd);
                        if (!string.IsNullOrEmpty(windowTitle))
                        {
                            if (windowTitle.IndexOf("NetTool", StringComparison.OrdinalIgnoreCase) < 0 &&
                                windowTitle.IndexOf("測試區域", StringComparison.OrdinalIgnoreCase) < 0 &&
                                windowTitle.IndexOf("Cursor", StringComparison.OrdinalIgnoreCase) < 0 &&
                                windowTitle.IndexOf("Visual Studio", StringComparison.OrdinalIgnoreCase) < 0 &&
                                windowTitle.IndexOf("VS Code", StringComparison.OrdinalIgnoreCase) < 0 &&
                                windowTitle.IndexOf(".cs", StringComparison.OrdinalIgnoreCase) < 0 &&
                                windowTitle.IndexOf(".java", StringComparison.OrdinalIgnoreCase) < 0 &&
                                windowTitle.IndexOf("Chrome", StringComparison.OrdinalIgnoreCase) < 0)
                            {
                                foundWindow = hWnd;
                                return false;
                            }
                        }
                        else
                        {
                            foundWindow = hWnd;
                            return false;
                        }
                    }
                }
                return true;
            }, IntPtr.Zero);

            return foundWindow;
        }

        /// <summary>
        /// 讀取容器及其所有子控制項的完整內容
        /// </summary>
        public string ReadContainerContent(IntPtr container)
        {
            if (container == IntPtr.Zero)
            {
                return string.Empty;
            }

            List<string> allTexts = new List<string>();
            CollectControlTexts(container, allTexts, 0);

            HashSet<string> seen = new HashSet<string>();
            return string.Join("\r\n", allTexts
                .Select(text => text.Trim())
                .Where(text => ShouldIncludeText(text, seen))
                .Where(text => !string.IsNullOrEmpty(text)));
        }

        /// <summary>
        /// 遞迴收集所有控制項的文字
        /// </summary>
        private void CollectControlTexts(IntPtr parent, List<string> texts, int depth)
        {
            if (depth > 10 || parent == IntPtr.Zero)
            {
                return;
            }

            EnumChildWindows(parent, (hWnd, lParam) =>
            {
                string text = Core.WindowApiCore.ReadControlText(hWnd);
                if (!string.IsNullOrEmpty(text) && text.Trim().Length > 0)
                {
                    texts.Add(text);
                }

                CollectControlTexts(hWnd, texts, depth + 1);
                return true;
            }, IntPtr.Zero);
        }

        /// <summary>
        /// 判斷是否應該包含該文字
        /// </summary>
        private bool ShouldIncludeText(string text, HashSet<string> seen)
        {
            if (text.Length <= 1 || seen.Contains(text))
            {
                return false;
            }

            seen.Add(text);
            return !ExcludedTexts.Any(excluded => text.Contains(excluded));
        }

        public IntPtr QueryTopLevelWindow(IntPtr hWnd)
        {
            return Core.WindowApiCore.QueryTopLevelWindow(hWnd);
        }
    }
}

