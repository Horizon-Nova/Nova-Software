using System;
using System.Collections.Generic;
using NetToolTestArea.Utility;
using NetToolTestArea.Model;

namespace NetToolTestArea.Services
{
    public class ClickWindowService
    {
        private ClickModeUtility clickModeUtility;

        public ClickWindowService(ClickModeUtility clickModeUtility)
        {
            this.clickModeUtility = clickModeUtility;
        }

        /// <summary>
        /// 啟用點擊視窗模式
        /// </summary>
        public void Enable(Action<ControlInfo> onWindowSelected) => clickModeUtility.StartClickMode(onWindowSelected);

        /// <summary>
        /// 停用點擊視窗模式
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

