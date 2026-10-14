# Sandbox

研究與測試用途，非正式交付物。正式軟體（畫面＋DLL）在 `../VaeronInSpecDemo`。

## scripts/
逆向研究期的一次性工具，非產品的一部分：

| 檔 | 用途 |
|---|---|
| `inject_dump.ps1` | 32-bit PowerShell 注入器：把 `InSpecHookDLL.dll` 注入 InSpec，並以 `CreateRemoteThread` 呼叫指定 RVA 的匯出函式。編輯 `$rva`／`$dllPath` 換要呼叫的函式。用 `C:\Windows\SysWOW64\WindowsPowerShell\v1.0\powershell.exe` 跑。 |
| `inject_fill_param.ps1` | 同上，但示範傳 UTF-16 字串參數（跨行程寫記憶體再傳位址）給 `AutoFillAndConfirmPrompt`。 |
| `probe_visible.ps1` | ✗ **已知失效，不要用**——`FindWindow`/`FindWindowW`（含顯式 EntryPoint、含 32-bit PowerShell 都試過）對 InSpec 隱藏後的 `InSpecAppFrame` 一律回傳「找不到」，但同一支視窗用 `EnumWindows` 列舉可以找到且 `IsWindowVisible` 正確回報 `False`。根因未查出（已排除：進入點解析、行程位元數）。改用 `probe_all_windows.ps1`。 |
| `probe_all_windows.ps1` | 探測某 PID 的所有頂層視窗（含隱藏）：`EnumWindows`＋`GetWindowThreadProcessId` 過濾＋`GetClassName`／`IsWindowVisible`。用法：`probe_all_windows.ps1 {PID}`。已驗證能正確找到隱藏的 `InSpecAppFrame`（2026-07-30 實機）。 |
| `export_test.iwp` | 啟用 AutoExport 的測試程式副本（`Enab 1`＋匯出路徑）。 |

## 注意
- `.ps1` 一律 ASCII，不放中文註解：PS 5.1 無 BOM 會以 Big5 誤讀導致陣列變 0 長度（見 blueprint `藍圖_dll注入` §9）。
- `inject_dump.ps1` 從自己所在的位置找 DLL（`../../VaeronInSpecDemo/InSpecHookDLL_Source/InSpecHookDLL.dll`）；DLL 不進版控，clone 下來要先用 `auto_build.bat` 編一次。
- 診斷用匯出（`Dump*`，見 `藍圖_控制項偵查`）目前仍混在正式 DLL 原始碼裡，尚未抽離到 Sandbox 專屬 build（待辦）。
- **`FindWindow` 外部探測 InSpec 隱藏視窗不可靠**（見上表 `probe_visible.ps1`）：產品內部（`InSpecController.IsMainWindowVisible`）用同一個 Win32 API 從 app 行程內呼叫，log 證實正常運作（True→False 正確轉換）；只有「從外部另開行程呼叫 FindWindow」這條路徑異常。外部驗證一律改用 `probe_all_windows.ps1`。
