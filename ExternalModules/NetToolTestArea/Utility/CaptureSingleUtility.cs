using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using NetToolTestArea.Model;

namespace NetToolTestArea.Utility
{
    /// <summary>
    /// 抓取單項工具類
    /// </summary>
    public class CaptureSingleUtility
    {
        // 摘要欄位關鍵字
        private const string SUMMARY_KEYWORD = "摘要";
        private const string SUMMARY_KEYWORD_ALT = "文字說明";
        // 摘要內容控制項的類名
        private const string SUMMARY_CONTENT_CLASS_NAME = "AfxWnd100u";
        // 摘要對話框的類名
        private const string SUMMARY_DIALOG_CLASS_NAME = "#32770";
        // InSpec 視窗標題關鍵字
        private const string INSPEC_WINDOW_KEYWORD_VERSION = "InSpec 視窗版";
        private const string INSPEC_WINDOW_KEYWORD_FULL = "InSpec for Windows";

        // Windows API 函數
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        private ClickModeUtility clickModeUtility;

        public CaptureSingleUtility(ClickModeUtility clickModeUtility)
        {
            this.clickModeUtility = clickModeUtility;
        }

        /// <summary>
        /// 讀取 InSpec 視窗中「摘要」欄位底下的內容
        /// </summary>
        public string ReadSummaryContent()
        {
            StringBuilder debugInfo = new StringBuilder();
            debugInfo.AppendLine("=== 調試信息 ===");
            
            try
            {
                // 1. 查找 InSpec 視窗
                debugInfo.AppendLine("步驟 1: 查找 InSpec 視窗...");
                IntPtr inspecWindow = clickModeUtility.FindInSpecWindow();
                if (inspecWindow == IntPtr.Zero)
                {
                    debugInfo.AppendLine("  [失敗] 找不到 InSpec 視窗");
                    throw new Exception(debugInfo.ToString() + "\r\n\r\n請確認：\r\n1. InSpec 應用程式已開啟\r\n2. 視窗標題包含 \"InSpec\" 關鍵字\r\n3. 視窗是可見的");
                }
                
                // 獲取視窗詳細信息
                string windowTitle = Core.WindowApiCore.QueryWindowTitle(inspecWindow);
                string className = Core.WindowApiCore.QueryControlClassName(inspecWindow);
                
                // 檢查是否為正確的視窗
                bool isCorrectWindow = windowTitle.IndexOf(INSPEC_WINDOW_KEYWORD_VERSION, StringComparison.OrdinalIgnoreCase) >= 0;
                bool isFallbackWindow = !isCorrectWindow && windowTitle.IndexOf(INSPEC_WINDOW_KEYWORD_FULL, StringComparison.OrdinalIgnoreCase) >= 0;
                
                if (isCorrectWindow)
                {
                    debugInfo.AppendLine($"  [成功] 找到 InSpec 視窗（正確的視窗）");
                }
                else if (isFallbackWindow)
                {
                    debugInfo.AppendLine($"  [成功] 找到 InSpec 視窗（備用視窗）");
                }
                else
                {
                    debugInfo.AppendLine($"  [警告] 找到包含 \"InSpec\" 的視窗，但可能不是正確的視窗");
                    debugInfo.AppendLine($"    預期視窗標題應包含: \"{INSPEC_WINDOW_KEYWORD_VERSION}\"（主要）或 \"{INSPEC_WINDOW_KEYWORD_FULL}\"（備用）");
                }
                debugInfo.AppendLine($"    句柄: 0x{inspecWindow.ToInt64():X}");
                debugInfo.AppendLine($"    視窗標題: {windowTitle}");
                debugInfo.AppendLine($"    類名: {className}");
                
                // 列出視窗中的所有控制項（用於調試）
                debugInfo.AppendLine("\n步驟 1.1: 列出視窗中的所有控制項（前20個）...");
                List<string> controlList = ListAllControls(inspecWindow, 20);
                if (controlList.Count > 0)
                {
                    debugInfo.AppendLine($"  找到 {controlList.Count} 個控制項：");
                    for (int i = 0; i < controlList.Count && i < 20; i++)
                    {
                        debugInfo.AppendLine($"    {i + 1}. {controlList[i]}");
                    }
                }
                else
                {
                    debugInfo.AppendLine("  未找到任何控制項");
                }

                // 2. 優先查找 #32770 對話框
                debugInfo.AppendLine("步驟 2: 查找 #32770 對話框...");
                IntPtr dialog = FindDialogRecursive(inspecWindow);
                debugInfo.AppendLine($"  方法1 (EnumChildWindows): {(dialog != IntPtr.Zero ? $"找到對話框，句柄: 0x{dialog.ToInt64():X}" : "未找到")}");
                
                if (dialog == IntPtr.Zero)
                {
                    dialog = FindDialogByEnumWindows(inspecWindow);
                    debugInfo.AppendLine($"  方法2 (EnumWindows): {(dialog != IntPtr.Zero ? $"找到對話框，句柄: 0x{dialog.ToInt64():X}" : "未找到")}");
                }
                
                if (dialog != IntPtr.Zero)
                {
                    debugInfo.AppendLine("  [成功] 找到 #32770 對話框");
                    
                    // 在對話框中查找「文字說明」控制項
                    debugInfo.AppendLine("步驟 3: 在對話框中查找「文字說明」控制項...");
                    SearchResult result = new SearchResult();
                    FindTextDescriptionControlRecursive(dialog, result);

                    if (result.LabelControl != IntPtr.Zero)
                    {
                        debugInfo.AppendLine($"  [成功] 找到「文字說明」控制項，句柄: 0x{result.LabelControl.ToInt64():X}");
                        
                        // 向上查找容器並讀取完整內容
                        debugInfo.AppendLine("步驟 4: 向上查找容器並讀取內容...");
                        string containerContent = FindAndReadContainerContent(result.LabelControl);
                        if (!string.IsNullOrEmpty(containerContent) && containerContent.Length > 10)
                        {
                            debugInfo.AppendLine($"  [成功] 讀取到容器內容，長度: {containerContent.Length} 字符");
                            return debugInfo.ToString() + "\r\n\r\n=== 讀取成功 ===\r\n\r\n摘要內容：\r\n" + containerContent;
                        }
                        debugInfo.AppendLine("  [失敗] 容器內容為空或太短");
                    }
                    else
                    {
                        debugInfo.AppendLine("  [失敗] 未找到「文字說明」控制項");
                    }

                    // 如果找不到「文字說明」控制項，直接讀取對話框的完整內容
                    debugInfo.AppendLine("步驟 5: 直接讀取對話框的完整內容...");
                    string dialogContent = clickModeUtility.ReadContainerContent(dialog);
                    if (!string.IsNullOrEmpty(dialogContent) && dialogContent.Length > 10)
                    {
                        debugInfo.AppendLine($"  [成功] 讀取到對話框內容，長度: {dialogContent.Length} 字符");
                        return debugInfo.ToString() + "\r\n\r\n=== 讀取成功 ===\r\n\r\n摘要內容：\r\n" + dialogContent;
                    }
                    debugInfo.AppendLine("  [失敗] 對話框內容為空或太短");
                }
                else
                {
                    debugInfo.AppendLine("  [失敗] 未找到 #32770 對話框");
                }

                // 3. 如果找不到對話框，在主視窗中查找「文字說明」控制項
                debugInfo.AppendLine("步驟 6: 在主視窗中查找「文字說明」控制項...");
                SearchResult mainResult = new SearchResult();
                FindTextDescriptionControlRecursive(inspecWindow, mainResult);

                if (mainResult.LabelControl != IntPtr.Zero)
                {
                    debugInfo.AppendLine($"  [成功] 找到「文字說明」控制項，句柄: 0x{mainResult.LabelControl.ToInt64():X}");
                    
                    // 向上查找容器並讀取完整內容
                    debugInfo.AppendLine("步驟 7: 向上查找容器並讀取內容...");
                    string containerContent = FindAndReadContainerContent(mainResult.LabelControl);
                    if (!string.IsNullOrEmpty(containerContent) && containerContent.Length > 10)
                    {
                        debugInfo.AppendLine($"  [成功] 讀取到容器內容，長度: {containerContent.Length} 字符");
                        return debugInfo.ToString() + "\r\n\r\n=== 讀取成功 ===\r\n\r\n摘要內容：\r\n" + containerContent;
                    }
                    debugInfo.AppendLine("  [失敗] 容器內容為空或太短");
                }
                else
                {
                    debugInfo.AppendLine("  [失敗] 未找到「文字說明」控制項");
                }

                // 4. 如果找不到「文字說明」控制項，直接查找所有容器並讀取內容
                debugInfo.AppendLine("步驟 8: 查找所有容器並讀取內容...");
                string allContainerContent = FindAndReadAllContainers(inspecWindow);
                if (!string.IsNullOrEmpty(allContainerContent) && allContainerContent.Length > 10)
                {
                    debugInfo.AppendLine($"  [成功] 讀取到容器內容，長度: {allContainerContent.Length} 字符");
                    return debugInfo.ToString() + "\r\n\r\n=== 讀取成功 ===\r\n\r\n摘要內容：\r\n" + allContainerContent;
                }
                debugInfo.AppendLine("  [失敗] 未找到容器或容器內容為空");

                // 所有步驟都失敗
                debugInfo.AppendLine("\r\n=== 總結 ===");
                debugInfo.AppendLine("所有查找步驟均失敗，無法讀取摘要內容。");
                throw new Exception(debugInfo.ToString() + "\r\n\r\n請確認：\r\n1. InSpec 視窗已開啟\r\n2. 視窗標題包含 \"InSpec\"\r\n3. 摘要對話框已顯示\r\n4. 摘要內容已填入\r\n\r\n提示：可以使用「點擊模式」直接點擊目標控制項來驗證");
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("=== 調試信息 ==="))
                {
                    throw;
                }
                throw new Exception(debugInfo.ToString() + "\r\n\r\n錯誤: " + ex.Message, ex);
            }
        }

        /// <summary>
        /// 從包含「文字說明」的控制項向上查找容器並讀取完整內容
        /// </summary>
        private string FindAndReadContainerContent(IntPtr textDescriptionControl)
        {
            IntPtr current = textDescriptionControl;
            const int maxDepth = 10;
            int depth = 0;
            string bestContent = string.Empty;
            IntPtr bestContainer = IntPtr.Zero;

            while (current != IntPtr.Zero && depth < maxDepth)
            {
                IntPtr parent = Core.WindowApiCore.QueryParentWindow(current);
                if (parent == IntPtr.Zero)
                {
                    break;
                }

                if (Core.WindowApiCore.IsContainerType(parent))
                {
                    string containerContent = clickModeUtility.ReadContainerContent(parent);
                    if (!string.IsNullOrEmpty(containerContent) && containerContent.Length > bestContent.Length)
                    {
                        bestContent = containerContent;
                        bestContainer = parent;
                    }
                }

                current = parent;
                depth++;
            }

            return bestContainer != IntPtr.Zero && !string.IsNullOrEmpty(bestContent) ? bestContent : string.Empty;
        }

        /// <summary>
        /// 查找所有容器並讀取內容（不依賴「文字說明」標籤）
        /// </summary>
        private string FindAndReadAllContainers(IntPtr parentWindow)
        {
            string bestContent = string.Empty;
            List<IntPtr> containers = new List<IntPtr>();

            EnumChildWindows(parentWindow, (hWnd, lParam) =>
            {
                if (Core.WindowApiCore.IsContainerType(hWnd))
                {
                    containers.Add(hWnd);
                }

                string childContent = FindAndReadAllContainers(hWnd);
                if (!string.IsNullOrEmpty(childContent) && childContent.Length > bestContent.Length)
                {
                    bestContent = childContent;
                }

                return true;
            }, IntPtr.Zero);

            foreach (IntPtr container in containers)
            {
                string containerContent = clickModeUtility.ReadContainerContent(container);
                if (!string.IsNullOrEmpty(containerContent) && containerContent.Length > bestContent.Length)
                {
                    bestContent = containerContent;
                }
            }

            return bestContent;
        }

        /// <summary>
        /// 輔助類：用於存儲查找結果
        /// </summary>
        private class SearchResult
        {
            public IntPtr LabelControl { get; set; } = IntPtr.Zero;
            public IntPtr ContentControl { get; set; } = IntPtr.Zero;
            public string BestContent { get; set; } = string.Empty;
        }

        /// <summary>
        /// 遞迴查找包含「文字說明」的控制項
        /// </summary>
        private void FindTextDescriptionControlRecursive(IntPtr parentWindow, SearchResult result)
        {
            EnumChildWindows(parentWindow, (hWnd, lParam) =>
            {
                string classNameStr = Core.WindowApiCore.QueryControlClassName(hWnd);
                
                if (classNameStr == SUMMARY_CONTENT_CLASS_NAME)
                {
                    string text = Core.WindowApiCore.ReadControlText(hWnd);
                    if (!string.IsNullOrEmpty(text) && 
                        !text.Contains("啟用點擊模式") && 
                        !text.Contains("讀取摘要內容") &&
                        !text.Contains("停用點擊模式") &&
                        !text.Contains("就緒") &&
                        !text.Contains("NetTool") &&
                        !text.Contains("測試區域"))
                    {
                        if (text.Contains(SUMMARY_KEYWORD_ALT))
                        {
                            if (result.LabelControl == IntPtr.Zero || text.Length > Core.WindowApiCore.ReadControlText(result.LabelControl).Length)
                            {
                                result.LabelControl = hWnd;
                            }
                        }
                        else if (text.Length > 10)
                        {
                            if (string.IsNullOrEmpty(result.BestContent) || text.Length > result.BestContent.Length)
                            {
                                result.BestContent = text;
                                result.ContentControl = hWnd;
                            }
                        }
                    }
                }
                
                FindTextDescriptionControlRecursive(hWnd, result);
                return true;
            }, IntPtr.Zero);
        }

        /// <summary>
        /// 遞迴查找對話框
        /// </summary>
        private IntPtr FindDialogRecursive(IntPtr parentWindow)
        {
            IntPtr foundDialog = IntPtr.Zero;

            EnumChildWindows(parentWindow, (hWnd, lParam) =>
            {
                if (foundDialog != IntPtr.Zero)
                {
                    return false;
                }

                string classNameStr = Core.WindowApiCore.QueryControlClassName(hWnd);
                
                if (classNameStr == SUMMARY_DIALOG_CLASS_NAME)
                {
                    foundDialog = hWnd;
                    return false;
                }
                
                IntPtr childDialog = FindDialogRecursive(hWnd);
                if (childDialog != IntPtr.Zero)
                {
                    foundDialog = childDialog;
                    return false;
                }
                
                return true;
            }, IntPtr.Zero);

            return foundDialog;
        }

        /// <summary>
        /// 查找屬於指定父視窗的對話框（使用 EnumWindows 查找所有視窗）
        /// </summary>
        private IntPtr FindDialogByEnumWindows(IntPtr parentWindow)
        {
            IntPtr foundDialog = IntPtr.Zero;

            EnumWindows((hWnd, lParam) =>
            {
                if (!IsWindowVisible(hWnd))
                {
                    return true;
                }

                string classNameStr = Core.WindowApiCore.QueryControlClassName(hWnd);

                if (classNameStr == SUMMARY_DIALOG_CLASS_NAME)
                {
                    IntPtr parent = Core.WindowApiCore.QueryParentWindow(hWnd);
                    if (parent == parentWindow)
                    {
                        foundDialog = hWnd;
                        return false;
                    }

                    IntPtr current = parent;
                    int depth = 0;
                    while (current != IntPtr.Zero && depth < 10)
                    {
                        if (current == parentWindow)
                        {
                            foundDialog = hWnd;
                            return false;
                        }
                        current = Core.WindowApiCore.QueryParentWindow(current);
                        depth++;
                    }
                }

                return true;
            }, IntPtr.Zero);

            return foundDialog;
        }

        /// <summary>
        /// 列出視窗中的所有控制項（用於調試）
        /// </summary>
        private List<string> ListAllControls(IntPtr parentWindow, int maxCount = 50)
        {
            List<string> controlList = new List<string>();
            int count = 0;

            EnumChildWindows(parentWindow, (hWnd, lParam) =>
            {
                if (count >= maxCount)
                {
                    return false;
                }

                string classNameStr = Core.WindowApiCore.QueryControlClassName(hWnd);
                string text = Core.WindowApiCore.ReadControlText(hWnd);
                string displayText = string.IsNullOrEmpty(text) ? "(無內容)" : (text.Length > 30 ? text.Substring(0, 30) + "..." : text);

                controlList.Add($"句柄: 0x{hWnd.ToInt64():X}, 類名: {classNameStr}, 文字: {displayText}");
                count++;

                return true;
            }, IntPtr.Zero);

            return controlList;
        }

        /// <summary>
        /// 查找 InSpec 視窗
        /// </summary>
        public IntPtr FindInSpecWindow() => clickModeUtility.FindInSpecWindow();

        public List<MenuItemInfo> QueryMenuItems(IntPtr inspecWindow) => Core.WindowApiCore.QueryMenuItems(inspecWindow);
    }
}

