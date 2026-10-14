# AutoExport 讀值 藍圖

## 1. 定位
用 InSpec 自身的自動匯出（AutoExport）把量測資料寫進 `.txt` 檔,我方讀檔取值——資料不經螢幕、不需注入。無畫面量測的正式讀值路徑。
依據規格：InSpec 手冊「Export File Parameters／Playback Options」+ 附錄 B（`UGInSpec2Man.pdf` 2.92.0283）。

## 2. 範圍
- 涵蓋：在 `.iwp` 啟用 AutoExport（設檔路徑＋`Enab 1`）、執行後讀匯出 `.txt`。
- 排除：驅動執行（→ `藍圖_iscmd`）、GDI 讀畫面（→ `藍圖_gdi讀畫面`,姊妹,§10）。

## 3. 資料模型
`.iwp` 檔（UTF-16LE 括號式腳本）的 `Prog` 標頭欄位：
- `(ExpTarg File)` ← 匯出目標＝檔案
- `(AutoExpFile "{路徑}")` ← 自動匯出檔路徑
- `(AutoExpFileInf ((Enab 0|1) (ReTm 1) (Conf 0) (FrmInf (...delimiters...))))` ← 啟用旗標＋格式
- 匯出的「欄位選擇」是 per-feature 設定,**測試檔完全沒有選任何屬性**（見 §9）

## 4. 資料交換

### 匯出檔——收到
| 欄位 | 型別 | 必 | 說明 |
|---|---|---|---|
| 每行 | Tab 分隔文字 | | 一列＝依 `.iwp` 選定的 feature 屬性 ← `FldDlm Tab`/`RecDlm CrLf` |

編碼：依 `.iwp` 的 `(Unicode 0|1)` ← 可能 ANSI 或 UTF-16;讀取先試預設編碼,見到 `\0` 退回 UTF-16。
成功範例：`?` 未取得非空範例（測試檔無選欄位＋無真機台）。
失敗/空範例：0 bytes 檔（實測）——檔案確實被建立,但無內容。

## 5. 端點與流程
`PrepareAutoExportCopy(來源iwp)`：複製 `.iwp` 到工作副本（不動客戶原檔）→ `(AutoExpFile "")`→填路徑、`(AutoExpFileInf ((Enab 0)`→`Enab 1` → 刪舊匯出檔（AutoExport 是 append,先清）→ 回 `(工作副本, 匯出檔路徑)`
執行：`藍圖_iscmd` 的 `/run` 工作副本 → InSpec 每次執行寫匯出檔
`ReadExportFile(匯出檔)`：不存在→空清單;讀檔→依編碼解碼→切行→回非空行
程式碼：`InSpecController.cs`（`PrepareAutoExportCopy`／`ReadExportFile`）

## 6. 業務規則
- 匯出時機 ← `(AutoExpFileInf` 的語意：Disabled／During Run／After Run／After Run Confirm（手冊）;`Conf 0`＝不跳確認框（無人值守要 `Conf 0`）。
- append 模式：每次執行往檔尾加 → 讀乾淨要先刪舊檔（`PrepareAutoExportCopy` 已做）。
- 空匯出檔的成因有二,缺一即空：(1) 程式沒選任何要匯出的屬性;(2) 沒有實際量測值（無元素/無機台）。

## 7. 權限
無使用者權限機制。授權：原廠口頭同意。

## 8. 跨功能依賴
- 對外暴露：`ReadExportFile`／`PrepareAutoExportCopy`（`InSpecController`）,被 VaeronInSpecDemo「無畫面量測」讀值步驟用。
- 依賴：`藍圖_iscmd`（觸發執行才會產生匯出）。

## 9. 已知限制／待決定／修正記錄
- ✗ 真實量測資料內容未驗——測試 `.iwp` 全文只有一個 `Prmt`、無量測元素、無 export 屬性選擇,又無真機台;三者任一缺就是空檔。已實機驗證「iscmd /run → 匯出檔在指定路徑被建立」（0 bytes,2026-07-29）,未驗證「有資料」。
- ✗ 要有非空匯出,需：用 GUI（Feature→Exports）在程式裡選要匯出的屬性並存檔（一次性設定,之後執行才是無畫面）,或找一個已選好屬性的程式——目前無此檔。
- 待決定：AutoExport 是「編輯 `.iwp` 副本」啟用（現行,不動原檔）還是走 Playback Options／InSpec.ini 全域設定,未定案。

## 10. 同系列比對
`藍圖_gdi讀畫面.md`——另一條讀值路徑（攔 `TextOutW` 讀 GXWND 畫面）。比對後發現：AutoExport 走檔案、不需視窗可見、不需注入、是原廠機制;GDI 讀畫面需視窗實際繪製（隱藏即失效）、需注入、是逆向手法。結論：AutoExport 為正式讀值路徑,GDI 讀畫面降為 legacy／備用（僅在需要即時看畫面當下值、且視窗可見時）。
