using InSpecHookDLLHost;

// 「InSpec 啟動時就已改造，不依賴我方展示軟體開著」的落地點：
// 使用者以後啟動 InSpec 走的是這支 exe（取代直接雙擊 InSpec.exe 本身），
// 啟動＋注入＋啟用三項前端改造能力一次做完就結束，之後 InSpecHookDLLHost
// 那個展示主控台開不開都不影響 InSpec 本身已經改造過的畫面。
AppLog.Info("InSpecAutoLaunch", "啟動流程開始");

var controller = new InSpecController();
var launched = controller.LaunchInSpecVisible(InSpecController.InSpecExeDefaultPath);
if (!launched)
{
    AppLog.Result("InSpecAutoLaunch", false, "啟動或注入失敗，見上方 log");
    return 1;
}

var overlayOk = controller.EnableOverlay();
var xyzOk = controller.EnableXyzEdit();
var reloadOk = controller.EnableReloadButton();
AppLog.Result("InSpecAutoLaunch", overlayOk && xyzOk && reloadOk,
    $"自動啟用：完成圖示樣式={overlayOk}, XYZ就地編輯={xyzOk}, 重新載入按鈕={reloadOk}");

return 0;
