using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using NetToolTestArea.Model;

namespace NetToolTestArea.Core
{
    public static class WindowApiCore
    {
        // Windows API 常數
        private const uint WM_GETTEXT = 0x000D;
        private const uint WM_GETTEXTLENGTH = 0x000E;
        private const uint BM_CLICK = 0x00F5;
        private const uint WM_LBUTTONDOWN = 0x0201;
        private const uint WM_LBUTTONUP = 0x0202;
        private const uint GA_ROOT = 2;

        // Windows API 函數
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetParent(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc enumProc, IntPtr lParam);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        public static string QueryControlClassName(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero)
            {
                return string.Empty;
            }

            StringBuilder className = new StringBuilder(256);
            GetClassName(hWnd, className, className.Capacity);
            return className.ToString();
        }

        public static string QueryWindowTitle(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero)
            {
                return string.Empty;
            }

            int length = GetWindowTextLength(hWnd);
            if (length <= 0)
            {
                return string.Empty;
            }

            StringBuilder title = new StringBuilder(length + 1);
            GetWindowText(hWnd, title, title.Capacity);
            return title.ToString();
        }

        public static string ReadControlText(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero)
            {
                return string.Empty;
            }

            try
            {
                IntPtr textLengthPtr = SendMessage(hWnd, WM_GETTEXTLENGTH, IntPtr.Zero, IntPtr.Zero);
                int textLength = textLengthPtr.ToInt32();

                if (textLength <= 0 || textLength >= 10000)
                {
                    return string.Empty;
                }

                IntPtr textBuffer = Marshal.AllocHGlobal((textLength + 1) * 2);
                try
                {
                    SendMessage(hWnd, WM_GETTEXT, (IntPtr)(textLength + 1), textBuffer);
                    return Marshal.PtrToStringUni(textBuffer, textLength) ?? string.Empty;
                }
                finally
                {
                    Marshal.FreeHGlobal(textBuffer);
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        public static IntPtr QueryParentWindow(IntPtr hWnd)
        {
            return hWnd == IntPtr.Zero ? IntPtr.Zero : GetParent(hWnd);
        }

        public static IntPtr QueryTopLevelWindow(IntPtr hWnd)
        {
            return hWnd == IntPtr.Zero ? IntPtr.Zero : GetAncestor(hWnd, GA_ROOT);
        }

        public static void ClickStandardButton(IntPtr buttonHandle)
        {
            if (buttonHandle != IntPtr.Zero)
            {
                SendMessage(buttonHandle, BM_CLICK, IntPtr.Zero, IntPtr.Zero);
            }
        }

        public static void ClickToolbarButton(IntPtr buttonHandle)
        {
            if (buttonHandle == IntPtr.Zero || !GetWindowRect(buttonHandle, out RECT rect))
            {
                return;
            }

            int centerX = (rect.Left + rect.Right) / 2;
            int centerY = (rect.Top + rect.Bottom) / 2;
            int clientX = centerX - rect.Left;
            int clientY = centerY - rect.Top;
            uint lParam = (uint)((clientY << 16) | (clientX & 0xFFFF));

            SendMessage(buttonHandle, WM_LBUTTONDOWN, new IntPtr(1), new IntPtr(lParam));
            System.Threading.Thread.Sleep(50);
            SendMessage(buttonHandle, WM_LBUTTONUP, IntPtr.Zero, new IntPtr(lParam));
        }

        public static bool IsButtonControl(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero)
            {
                return false;
            }

            string className = QueryControlClassName(hWnd);
            return IsButtonClassName(className);
        }

        public static bool IsButtonClassName(string className)
        {
            if (string.IsNullOrEmpty(className))
            {
                return false;
            }

            string lowerClassName = className.ToLower();
            return lowerClassName.Contains("button") || className == "ToolbarWindow32";
        }

        public static bool IsClickableControl(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero)
            {
                return false;
            }

            string className = QueryControlClassName(hWnd);
            return IsClickableClassName(className);
        }

        public static bool IsClickableClassName(string className)
        {
            if (string.IsNullOrEmpty(className))
            {
                return false;
            }

            string lowerClassName = className.ToLower();
            
            // 使用統一的關鍵字列表判斷，避免死邏輯
            string[] clickableKeywords = { "button", "toolbar", "tool", "menu", "link", "label" };
            string[] clickableClassNames = { "ToolbarWindow32", "SysTabControl32", "Static", "SysLink", "SysHeader32" };
            
            return clickableKeywords.Any(keyword => lowerClassName.Contains(keyword)) ||
                   clickableClassNames.Contains(className);
        }

        public static bool IsContainerType(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero)
            {
                return false;
            }

            string className = QueryControlClassName(hWnd);
            return className == "#32770" ||
                   className == "SysTabControl32" ||
                   className == "AfxMDIFrame100u" ||
                   className == "AfxFrameOrView100u";
        }

        public static void EnumChildWindows(IntPtr hWndParent, Func<IntPtr, bool> enumProc)
        {
            if (hWndParent == IntPtr.Zero || enumProc == null)
            {
                return;
            }

            EnumWindowsProc proc = (hWnd, lParam) => enumProc(hWnd);
            EnumChildWindows(hWndParent, proc, IntPtr.Zero);
        }

        // 工具列按鈕相關常數
        private const uint TB_GETBUTTONCOUNT = 0x0418;
        private const uint TB_GETBUTTON = 0x0417;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct TBBUTTON
        {
            public int iBitmap;
            public int idCommand;
            public byte fsState;
            public byte fsStyle;
            public byte bReserved1;
            public byte bReserved2;
            public IntPtr dwData;
            public IntPtr iString;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct TBBUTTONINFO
        {
            public int cbSize;
            public int dwMask;
            public int idCommand;
            public int iImage;
            public byte fsState;
            public byte fsStyle;
            public short cx;
            public IntPtr lParam;
            public IntPtr pszText;
            public int cchText;
        }

        public static int QueryToolbarButtonCount(IntPtr toolbarHandle)
        {
            if (toolbarHandle == IntPtr.Zero)
            {
                return 0;
            }

            IntPtr result = SendMessage(toolbarHandle, TB_GETBUTTONCOUNT, IntPtr.Zero, IntPtr.Zero);
            return result.ToInt32();
        }

        public static Model.ToolbarButtonInfo QueryToolbarButton(IntPtr toolbarHandle, int buttonIndex)
        {
            if (toolbarHandle == IntPtr.Zero || buttonIndex < 0)
            {
                return null;
            }

            TBBUTTON button = new TBBUTTON();
            IntPtr buttonPtr = Marshal.AllocHGlobal(Marshal.SizeOf(button));
            try
            {
                IntPtr result = SendMessage(toolbarHandle, TB_GETBUTTON, new IntPtr(buttonIndex), buttonPtr);
                if (result.ToInt32() == 0)
                {
                    return null;
                }

                button = (TBBUTTON)Marshal.PtrToStructure(buttonPtr, typeof(TBBUTTON))!;

                return new Model.ToolbarButtonInfo
                {
                    Index = buttonIndex,
                    CommandId = button.idCommand,
                    Text = string.Empty,
                    ImageIndex = button.iBitmap
                };
            }
            finally
            {
                Marshal.FreeHGlobal(buttonPtr);
            }
        }

        public static List<Model.ToolbarButtonInfo> QueryToolbarButtons(IntPtr toolbarHandle)
        {
            List<Model.ToolbarButtonInfo> buttons = new List<Model.ToolbarButtonInfo>();

            if (toolbarHandle == IntPtr.Zero)
            {
                return buttons;
            }

            try
            {
                int buttonCount = QueryToolbarButtonCount(toolbarHandle);
                for (int i = 0; i < buttonCount; i++)
                {
                    var buttonInfo = QueryToolbarButton(toolbarHandle, i);
                    if (buttonInfo != null)
                    {
                        buttons.Add(buttonInfo);
                    }
                }
            }
            catch
            {
                // 忽略錯誤，避免崩潰
            }

            return buttons;
        }

        public static void ClickToolbarButtonByIndex(IntPtr toolbarHandle, int buttonIndex)
        {
            if (toolbarHandle == IntPtr.Zero || buttonIndex < 0)
            {
                return;
            }

            var buttonInfo = QueryToolbarButton(toolbarHandle, buttonIndex);
            if (buttonInfo == null)
            {
                return;
            }

            WmCommandCore.PostWMCommand(toolbarHandle, buttonInfo.CommandId);
        }

        // 選單相關 Windows API
        [DllImport("user32.dll")]
        private static extern IntPtr GetMenu(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int GetMenuItemCount(IntPtr hMenu);

        [DllImport("user32.dll")]
        private static extern IntPtr GetSubMenu(IntPtr hMenu, int nPos);

        [DllImport("user32.dll")]
        private static extern int GetMenuItemID(IntPtr hMenu, int nPos);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetMenuString(IntPtr hMenu, uint uIDItem, StringBuilder lpString, int nMaxCount, uint uFlag);

        private const uint MF_BYPOSITION = 0x00000400;

        public static List<Model.MenuItemInfo> QueryMenuItems(IntPtr windowHandle)
        {
            List<Model.MenuItemInfo> menuItems = new List<Model.MenuItemInfo>();

            if (windowHandle == IntPtr.Zero)
            {
                return menuItems;
            }

            try
            {
                IntPtr hMenu = GetMenu(windowHandle);
                if (hMenu == IntPtr.Zero)
                {
                    return menuItems;
                }

                int menuCount = GetMenuItemCount(hMenu);
                if (menuCount <= 0)
                {
                    return menuItems;
                }

                for (int menuIdx = 0; menuIdx < menuCount; menuIdx++)
                {
                    StringBuilder menuText = new StringBuilder(256);
                    GetMenuString(hMenu, (uint)menuIdx, menuText, 255, MF_BYPOSITION);
                    string menuName = menuText.ToString();

                    QuerySubMenuItemsRecursive(hMenu, menuIdx, menuName, menuItems);
                }
            }
            catch
            {
                // 忽略錯誤，避免崩潰
            }

            return menuItems;
        }

        private static void QuerySubMenuItemsRecursive(IntPtr hMenu, int menuIdx, string parentPath, List<Model.MenuItemInfo> menuItems)
        {
            if (hMenu == IntPtr.Zero)
            {
                return;
            }

            try
            {
                IntPtr hSubMenu = GetSubMenu(hMenu, menuIdx);
                if (hSubMenu == IntPtr.Zero)
                {
                    return;
                }

                int subCount = GetMenuItemCount(hSubMenu);
                if (subCount <= 0)
                {
                    return;
                }

                for (int itemIdx = 0; itemIdx < subCount; itemIdx++)
                {
                    StringBuilder itemText = new StringBuilder(256);
                    int textLen = GetMenuString(hSubMenu, (uint)itemIdx, itemText, 255, MF_BYPOSITION);
                    if (textLen <= 0)
                    {
                        continue;
                    }

                    string itemTextStr = itemText.ToString();
                    string currentPath = string.IsNullOrEmpty(parentPath) ? itemTextStr : $"{parentPath} > {itemTextStr}";

                    IntPtr childSubMenu = GetSubMenu(hSubMenu, itemIdx);
                    if (childSubMenu != IntPtr.Zero)
                    {
                        QuerySubMenuItemsRecursive(hSubMenu, itemIdx, currentPath, menuItems);
                    }
                    else
                    {
                        int commandId = GetMenuItemID(hSubMenu, itemIdx);
                        if (commandId >= 0)
                        {
                            menuItems.Add(new Model.MenuItemInfo
                            {
                                MenuIndex = menuIdx,
                                ItemIndex = itemIdx,
                                CommandId = commandId,
                                Text = itemTextStr,
                                MenuPath = currentPath
                            });
                        }
                    }
                }
            }
            catch
            {
                // 忽略錯誤，避免崩潰
            }
        }

        /// <summary>
        /// 查找選單項的 WM_COMMAND ID（根據選單路徑文字）
        /// </summary>
        public static int? FindMenuItemCommandId(IntPtr windowHandle, string menuPath)
        {
            if (windowHandle == IntPtr.Zero || string.IsNullOrEmpty(menuPath))
            {
                return null;
            }

            List<Model.MenuItemInfo> items = QueryMenuItems(windowHandle);
            string searchPath = menuPath.Trim();

            foreach (var item in items)
            {
                if (item.MenuPath.IndexOf(searchPath, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    item.Text.IndexOf(searchPath, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return item.CommandId;
                }
            }

            return null;
        }
    }
}

