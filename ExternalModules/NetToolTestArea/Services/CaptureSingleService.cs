using System;
using System.Collections.Generic;
using NetToolTestArea.Utility;
using NetToolTestArea.Model;

namespace NetToolTestArea.Services
{
    public class CaptureSingleService
    {
        private CaptureSingleUtility captureSingleUtility;

        public CaptureSingleService(CaptureSingleUtility captureSingleUtility)
        {
            this.captureSingleUtility = captureSingleUtility;
        }

        /// <summary>
        /// 查找InSpec視窗
        /// </summary>
        public IntPtr FindInSpecWindow() => captureSingleUtility.FindInSpecWindow();

        public List<MenuItemInfo> QueryMenuItems(IntPtr inspecWindow) => captureSingleUtility.QueryMenuItems(inspecWindow);
    }
}

