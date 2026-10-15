# DLL 注入 藍圖

## 1. 定位
把 `InSpecHookDLL.dll` 載入 InSpec 行程,讓其匯出函式能在 InSpec 行程內執行——是 GDI 讀畫面、訊息注入、前端注入三種技術的共同底座。
依據規格：無正式規格文件,依現有程式碼行為逆向記錄。

## 2. 範圍
- 涵蓋：注入 DLL、在遠端行程解析匯出函式位址、以 `CreateRemoteThread` 呼叫匯出函式（含傳 UTF-16 字串參數）。
- 排除：注入後各匯出函式做什麼（→ `藍圖_gdi讀畫面`／`藍圖_訊息注入`／`藍圖_前端注入`／`藍圖_控制項偵查`）。
- 姊妹：`藍圖_iscmd`（免注入的官方驅動）,比對 §10。

## 3. 資料模型
無——注入層只搬 DLL 路徑字串與函式參數,不碰業務資料。

## 4. 資料交換
無——本功能不涉及自訂資料交換格式;跨行程傳遞的只有 DLL 路徑（UTF-16）與單一字串參數（UTF-16,寫進遠端記憶體後傳位址）。

## 5. 端點與流程
注入：`OpenProcess(PROCESS_ALL_ACCESS)` → `VirtualAllocEx`（寫 DLL 路徑 UTF-16）→ `CreateRemoteThread(kernel32!LoadLibraryW, 遠端路徑位址)` → `WaitForSingleObject`
找遠端模組 base：`EnumProcessModulesEx` + `GetModuleFileNameEx` 比對檔名（**不用 LoadLibrary thread 的 exit code 當 base**,32-bit 截斷）
解析匯出位址（不硬編 RVA）：本機 `LoadLibrary` 同一支 DLL → `GetProcAddress(本機handle, 名)` − 本機 base ＝ RVA → 遠端 base ＋ RVA ＝ 遠端函式位址
呼叫：`CreateRemoteThread(遠端函式位址 [, 遠端字串參數位址])`
程式碼：`InSpecController.cs`（`InjectDll`／`FindRemoteModuleBase`／`ResolveRemoteFunctionAddress`／`CallExport`／`CallExportWithString`）、`NativeMethods.cs`

## 6. 業務規則
- 位元數必須一致：InSpec 是 32-bit → host 必須 `PlatformTarget=x86`;跨位元 `CreateRemoteThread` 不可靠（本 session 確認）。
- RVA 不硬編：診斷腳本可手動 `dumpbin` 抓 RVA,但正式程式必須用「本機載入同 DLL 算 RVA」,因為 DLL 每次重建 RVA 會變。
- 字串參數：`CallExportWithString` 先 `VirtualAllocEx`+`WriteProcessMemory` 寫 UTF-16,把位址當 `lpParameter`;`ThreadWrapper` 端原樣轉型接收。

## 7. 權限
需對目標行程 `PROCESS_ALL_ACCESS`（同使用者、同 session 即可,未跨使用者提權）。授權：原廠口頭同意逆向。

## 8. 跨功能依賴
- 對外暴露：`CallExport`／`CallExportWithString`（`InSpecController`）,被 `藍圖_gdi讀畫面`／`藍圖_訊息注入`（工具列點擊/提示填值）／`藍圖_前端注入`／`藍圖_控制項偵查` 呼叫。
- 依賴：無（是最底層）。

## 9. 已知限制／待決定／修正記錄
- 注入本身：本 session 多次實機驗證成功（DLL 載入、匯出可被 `CreateRemoteThread` 呼叫）。
- `?` 未處理「同名已載入其他 DLL」情形;`FindRemoteModuleBase` 以檔名尾比對,若有同名不同路徑模組會誤判（現況無此情形）。
- 32-bit PowerShell 注入器（`inject_dump.ps1`）：`.ps1` 若含中文註解且無 BOM,PS 5.1 會以 Big5 誤讀導致 `New-Object IntPtr[]` 變 0 長度 → 診斷腳本一律 ASCII（見 [[ps1-ascii-only-and-screenshot-scale]] 記憶）。

## 10. 同系列比對
`藍圖_iscmd.md`——免注入的官方驅動。比對後發現：iscmd 不需注入即可驅動執行,但只能 run/halt、不能讀值/改前端;注入是「讀畫面＋改前端＋填提示」這些 iscmd 做不到之事的唯一途徑。結論：兩者互補,不是取代——正式無畫面量測用 iscmd 驅動＋AutoExport 讀值（皆免注入）,注入只在需要「改 InSpec 前端」或「填提示」時才用。
