using System;
using NetToolTestArea.Core;

namespace NetToolTestArea.Utility
{
    /// <summary>
    /// 視窗 API 工具類
    /// </summary>
    public class WindowApiUtility
    {
        public string QueryControlClassName(IntPtr handle) => WindowApiCore.QueryControlClassName(handle);

        public bool IsButtonClassName(string className) => WindowApiCore.IsButtonClassName(className);

        public void ClickToolbarButton(IntPtr handle) => WindowApiCore.ClickToolbarButton(handle);

        public void ClickStandardButton(IntPtr handle) => WindowApiCore.ClickStandardButton(handle);

        public int QueryToolbarButtonCount(IntPtr toolbarHandle) => WindowApiCore.QueryToolbarButtonCount(toolbarHandle);

        public bool IsClickableClassName(string className) => WindowApiCore.IsClickableClassName(className);

        public IntPtr QueryTopLevelWindow(IntPtr handle) => WindowApiCore.QueryTopLevelWindow(handle);

        public void ClickControl(IntPtr controlHandle, string className)
        {
            if (className == "ToolbarWindow32" || !IsButtonClassName(className))
                ClickToolbarButton(controlHandle);
            else
                ClickStandardButton(controlHandle);
        }
    }
}

