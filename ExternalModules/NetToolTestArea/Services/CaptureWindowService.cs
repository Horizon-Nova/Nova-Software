using System;
using System.Collections.Generic;
using NetToolTestArea.Utility;
using NetToolTestArea.Model;

namespace NetToolTestArea.Services
{
    public class CaptureWindowService
    {
        private ClickModeUtility clickModeUtility;

        public CaptureWindowService(ClickModeUtility clickModeUtility)
        {
            this.clickModeUtility = clickModeUtility;
        }

        /// <summary>
        /// 啟用抓取視窗模式
        /// </summary>
        public void Enable(Action<ControlInfo> onControlSelected) => clickModeUtility.StartClickMode(onControlSelected);

        /// <summary>
        /// 停用抓取視窗模式
        /// </summary>
        public void Disable() => clickModeUtility.StopClickMode();

        /// <summary>
        /// 檢查是否啟用
        /// </summary>
        public bool IsActive => clickModeUtility.IsClickModeActive;

        public List<ControlInfo> QueryAllControls(IntPtr windowHandle) => clickModeUtility.QueryAllControls(windowHandle);

        public ControlInfo QueryControlInfo(IntPtr handle) => clickModeUtility.QueryControlInfo(handle);
    }
}

