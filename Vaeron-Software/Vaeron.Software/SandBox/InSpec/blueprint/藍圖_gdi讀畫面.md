# GDI 讀畫面 藍圖

## 1. 定位
攔截 InSpec 摘要面板（GXWND）重繪期間的 GDI 文字輸出,讀出 `GetWindowText` 讀不到的 owner-drawn 內容。已降為 legacy／備用（正式讀值改走 `藍圖_autoexport讀值`,§10）。
依據規格：無正式規格,依現有程式碼與實機攔截記錄。

## 2. 範圍
- 涵蓋：找摘要 GXWND、強制重繪、IAT-hook `TextOutW`/`ExtTextOutW` 攔截當次繪製文字＋座標、寫檔供 host 解析。
- 排除：注入底座（→ `藍圖_dll注入`）、AutoExport 讀值（→ `藍圖_autoexport讀值`,姊妹 §10）。

## 3. 資料模型
GXWND ← class `GXWND`、id `1268`,`GetWindowText` 回空（owner-drawn）;真實內容只在 `WM_PAINT` 期間經 GDI 畫出。

## 4. 資料交換

### 攔截 log 檔 `C:\InSpecHookDLL_paint.txt`——收到
| 欄位 | 型別 | 必 | 說明 |
|---|---|---|---|
| 行 | 文字 | | 格式 `TextOutW hdc=0x… (50,{Y}) len=… "{文字}"` |

版面（實測固定）：`x=50`;`y=1`「提示:」／`y=18`{提示名稱}／`y=38`「輸入:」／`y=55`{實際輸入值}
host 解析正規式：`^TextOutW.*?\(50,(\d+)\).*?"(.*)"`,依 `Y` 排序（排除 `ExtTextOutW`,同內容重複片段）
成功範例：`TextOutW hdc=… (50,55) len=18 "CDA343-AD-AUTOFILL"`（實測）

## 5. 端點與流程
`CaptureGxwndPaint`（DLL 匯出）：`FindSummaryGrid`（EnumWindows→InSpecAppFrame→GXWND id 1268）→ 開 log 檔 → `InstallGdiCaptureHooks`（只裝 GDI/繪圖 10 個 hook,見 §6）→ `InvalidateRect`+`UpdateWindow` 強制重繪 → hook 期間 `CaptureLog` 寫檔 → 關閉擷取
host：`InSpecController.CaptureSummary` → `CallExport("CaptureGxwndPaint")` → 讀檔 → `ParseCaptureLog`
程式碼：`InSpecHookDLL.cpp`（`CaptureGxwndPaint`／`InstallGdiCaptureHooks`／`CaptureLog`／`Hooked_TextOutW` 等）、`InSpecController.cs`（`CaptureSummary`／`ParseCaptureLog`）

## 6. 業務規則
- 隱藏視窗 → 不會收到 `WM_PAINT`,`InvalidateRect`+`UpdateWindow` 叫不動 → 攔不到目標視窗（實測 log 只有其他視窗的 `GetDC`）。這是本技術的硬限制,也是改用 AutoExport 的主因。
- 執行緒安全：hook 函式從多執行緒（InSpec 攝影機/探針子視窗會並發呼叫 `GetDC` 等）寫同一 `FILE*` 必須加 `CRITICAL_SECTION`（`g_csCapture`/`CaptureLog`）;未加會腐蝕 CRT 緩衝 → 崩潰（本 session crash #1）。
- hook 範圍必須最小：`InstallGdiCaptureHooks` 只裝 10 個 GDI/繪圖 hook,**不可**借用 `EnableApiHook` 的完整 hook 集（含 `SendMessageW` 做 `GetCallerInfo` 堆疊查詢、開銷大、從不卸載）→ 否則疊加後續操作會崩潰（本 session crash #2）。

## 7. 權限
需注入（見 `藍圖_dll注入`）。授權：原廠口頭同意。

## 8. 跨功能依賴
- 依賴：`藍圖_dll注入`（hook 需在 InSpec 行程內）。
- 被誰用：目前 host「摘要即時顯示」1s timer;正式無畫面流程已改用 AutoExport,此路徑保留但非主流程。

## 9. 已知限制／待決定／修正記錄
- ✗ 隱藏視窗下失效（見 §6）——無法用於純無畫面即時讀值。
- ~~crash #1 未同步寫檔~~——已修正（`CRITICAL_SECTION`）,重測不崩、並觀察到並發 `GetDC` 穿插無誤。
- ~~crash #2 借用過廣 hook~~——已修正（拆出 `InstallGdiCaptureHooks`,獨立旗標 `g_gdiHooksInstalled`）,重測完整序列不崩。
- host 讀檔編碼：DLL 以 `ccs=UTF-8` 寫,host 曾誤用 `Encoding.Unicode` 讀導致全部比對失敗——已修正為 `UTF8`;正規式曾漏 `hdc=` 段導致零比對——已修正為 `^TextOutW.*?\(50,`。

## 10. 同系列比對
`藍圖_autoexport讀值.md`——正式讀值路徑。比對結論見該檔 §10：AutoExport（檔案、免視窗、免注入、官方）取代 GDI 讀畫面（需繪製、需注入、逆向）為主路徑;GDI 僅在「視窗可見且要當下畫面值」時備用。
