using System;
using NetToolTestArea.Utility;

namespace NetToolTestArea.Services
{
    /// <summary>
    /// 視窗 API 服務層
    /// </summary>
    public class WindowApiService
    {
        private WindowApiUtility windowApiUtility;

        public WindowApiService(WindowApiUtility windowApiUtility)
        {
            this.windowApiUtility = windowApiUtility;
        }

        public string QueryControlClassName(IntPtr handle) => windowApiUtility.QueryControlClassName(handle);

        public bool IsButtonClassName(string className) => windowApiUtility.IsButtonClassName(className);

        public void ClickToolbarButton(IntPtr handle) => windowApiUtility.ClickToolbarButton(handle);

        public void ClickStandardButton(IntPtr handle) => windowApiUtility.ClickStandardButton(handle);

        public int QueryToolbarButtonCount(IntPtr toolbarHandle) => windowApiUtility.QueryToolbarButtonCount(toolbarHandle);

        public bool IsClickableClassName(string className) => windowApiUtility.IsClickableClassName(className);

        public IntPtr QueryTopLevelWindow(IntPtr handle) => windowApiUtility.QueryTopLevelWindow(handle);

        public void ClickControl(IntPtr controlHandle, string className) => windowApiUtility.ClickControl(controlHandle, className);
    }
}


