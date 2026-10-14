# iscmd 驅動 藍圖

## 1. 定位
用原廠官方 `iscmd.exe` 從外部驅動 InSpec 開檔＋執行量測程式,不靠 DLL 注入、不靠畫面操作。
依據規格：InSpec 手冊附錄 B（`UGInSpec2Man.pdf`,隨安裝目錄,版本 2.92.0283）。規格範圍與程式碼範圍一致。

## 2. 範圍
- 涵蓋：`/run`、`/run /nowait`、`/halt` 三個命令（手冊附錄 B 窮舉,無其他命令）。
- 排除：讀量測資料（§8 → `藍圖_autoexport讀值`）、提示填值（§8 → `藍圖_訊息注入`）、注入（`藍圖_dll注入`）、視窗隱藏（`藍圖_無畫面啟動`）。
- 姊妹：`藍圖_訊息注入`（另一條驅動路徑），比對 §10。

## 3. 資料模型
無——iscmd 不碰資料表/檔案內容,只送命令字串、回 exit code。

## 4. 資料交換

### iscmd 命令列——送出
| 欄位 | 型別 | 必 | 說明 |
|---|---|---|---|
| 命令 | 字串 | * | `/run` \| `/halt` |
| iwp 路徑 | 字串 | `/run` 時 * | 絕對路徑;含空格/特殊字元由 argv 陣列各自成員處理,不手動加引號 |
| `/nowait` | 旗標 | | 有＝非同步;無＝同步等到量測完 ← 手冊 |

格式：`iscmd /run "{iwp}" [/nowait]` ｜ `iscmd /halt`
回傳：exit code ← `0`＝命令送達成功;非零＝InSpec 沒在跑 或 參數不認得（手冊附錄 B）
成功範例：`iscmd /run "…SETUP…複製.iwp" /nowait` → exit `0`、InSpec 標題列由「未命名」變檔名（實測 2026-07-29）
失敗範例：InSpec 未執行 → exit 非零（`?` 確切碼未實測）

## 5. 端點與流程
`RunViaIscmdAsync(iwp, noWait)` → `RunIscmdAsync("/run", iwp [, "/nowait"])` → `Process.Start(iscmd.exe, ArgumentList)` → `WaitForExitAsync` → `ExitCode==0`
`HaltViaIscmdAsync()` → `RunIscmdAsync("/halt")`
iscmd 路徑 ← `InSpecExeDefaultPath` 同目錄的 `iscmd.exe`

## 6. 業務規則
- iscmd 用本機命名事件（`OpenEventA` 開既有事件）→ 要求 InSpec.exe 在執行（引擎＝行程,不可省;見 `藍圖_無畫面啟動`）。
- `/run` 同步（阻塞到量測完成）；`/nowait` 開始執行後立即返回 ← 手冊。含 `Prmt` 提示的程式,`/run` 會停在提示 → 需 `藍圖_訊息注入` 填值才會續跑。
- `/run` 開檔會讓 InSpec 主視窗 self-activate 重新顯示 → 無畫面情境需事後 re-hide（`藍圖_無畫面啟動` §9）。

## 7. 權限
無使用者權限機制。授權層級：原廠 Micro-Vu 口頭同意對此 EOL 版本做逆向/整合（非書面）。

## 8. 跨功能依賴
- 對外暴露：`RunViaIscmdAsync`／`HaltViaIscmdAsync`（`InSpecController`）,被 VaeronInSpecDemo「無畫面量測」主流程當驅動步驟。
- 依賴：`藍圖_無畫面啟動`（InSpec 需在跑;/run 重顯示視窗需補 re-hide）；搭配 `藍圖_autoexport讀值`（資料出口）、`藍圖_訊息注入`（程式含提示時填值）。

## 9. 已知限制／待決定／修正記錄
- ✗ `/run` 真實量測資料內容未驗——需真機台＋有量測元素的程式;目前只驗到 exit `0` ＋開檔 ＋觸發 AutoExport 產生匯出檔（0 bytes,§見 `藍圖_autoexport讀值`）。
- ✗ `/halt` 未實測。
- ✗ 失敗 exit 碼確切值未實測。
- `/run` 重顯示視窗：已知,由 `藍圖_無畫面啟動` 的 re-hide 補償（該 re-hide 本身在無畫面流程裡尚未實機重測）。

## 10. 同系列比對
`藍圖_訊息注入.md`——另一條驅動路徑（工具列 `WM_LBUTTONDOWN` 點「執行」鈕）。比對後發現：iscmd 是原廠官方介面、穩定、不需注入、不靠坐標;訊息注入是逆向手法、需先注入 DLL、依賴工具列 id(1165) 與坐標、脆弱。結論：iscmd 為正式驅動路徑,訊息注入降為 legacy／展示用。
