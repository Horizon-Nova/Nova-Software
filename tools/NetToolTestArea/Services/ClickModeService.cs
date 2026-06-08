using System;
using System.Collections.Generic;
using NetToolTestArea.Utility;
using NetToolTestArea.Model;

namespace NetToolTestArea.Services
{
    public class ClickModeService
    {
        private ClickModeUtility clickModeUtility;

        public ClickModeService(ClickModeUtility clickModeUtility)
        {
            this.clickModeUtility = clickModeUtility;
        }

        /// <summary>
        /// 啟用點擊模式
        /// </summary>
        public void Enable(Action<ControlInfo> onControlSelected) => clickModeUtility.StartClickMode(onControlSelected);

        /// <summary>
        /// 停用點擊模式
        /// </summary>
        public void Disable() => clickModeUtility.StopClickMode();

        /// <summary>
        /// 檢查是否啟用
        /// </summary>
        public bool IsActive => clickModeUtility.IsClickModeActive;

        /// <summary>
        /// 查找InSpec視窗
        /// </summary>
        public IntPtr FindInSpecWindow() => clickModeUtility.FindInSpecWindow();

        public ControlInfo QueryControlInfo(IntPtr handle) => clickModeUtility.QueryControlInfo(handle);

        /// <summary>
        /// 從控制項向上查找容器
        /// </summary>
        public IntPtr FindContainerFromControl(IntPtr control) => clickModeUtility.FindContainerFromControl(control);

        /// <summary>
        /// 查找容器內的所有可點擊控制項
        /// </summary>
        public List<ControlInfo> FindButtonsInContainer(IntPtr container) => clickModeUtility.FindButtonsInContainer(container);

        public bool CheckIfControlBelongsToInSpec(IntPtr controlHandle, IntPtr inspecWindow) => clickModeUtility.CheckIfControlBelongsToInSpec(controlHandle, inspecWindow);
    }
}

