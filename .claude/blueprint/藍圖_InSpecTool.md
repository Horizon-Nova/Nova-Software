# 藍圖：InSpecTool

2026-10-12

需求：`.claude/tasks/InSpecTool-功能需求.md`（第二版＋審核紀錄）。
技術依據：`Vaeron-Software\Vaeron.Software\research\InSpec\Sandbox\RunProbe`（2026-10-12 實測）。
規則：`~/.claude/rules/Rules.md`、`SQL_Design.md`、`UIUX.md`。

## 1. 組成

| 部分 | 位置 | 做什麼 |
|---|---|---|
| 資料庫 | `deploy/database/init/04_inspec.sql`、線上 `helix-studio` 庫 | 一張原始表 `dbo.inspec`，一支 `dbo.vw_inspec` |
| 模型 | `BuildModels/`（新建，照 Vaeron-web 的 BuildModels）→ `InSpecTool/Models/HelixStudio/` | 由資料庫產生，不手寫 |
| 合約 | `HelixStudio.Contract/IData.cs`（新增） | 平台把資料庫連線交給工具；不實作就不給 |
| 平台 | `HelixStudio/Views/HostPanel.xaml.cs` | 掛工具時，實作 `IData` 的就塞連線 |
| 工具 | `InSpecTool/`（新專案） | 畫面 `Board`、`manifest.json` |
| 套件 | `helixpack pack` → `InSpecTool.stpkg` | 匯入平台用 |

工具代碼 `local.inspec`、名稱「InSpecTool」：照 `local.checksum` 的命名，**我先定的，請確認**。

## 2. 資料庫

### 2.1 原始表 `dbo.inspec`

一列是什麼由 `type` 決定：`setting`（一項設定）、`field`（一個欄位）、`run`（一次執行）。只在寫入時碰。

| 欄位 | 型別 | 用在 | 說明 |
|---|---|---|---|
| id | serial PK | 全部 | |
| type | text NOT NULL | 全部 | setting / field / run |
| setting_key | text | setting | searchFolder、logFolder、backupFolder、scanInterval、showResult、unlockCode |
| value | text | setting | 設定值 |
| options | jsonb NOT NULL DEFAULT '[]' | setting | 可選的值（scanInterval 的 80/120/160/200/300/500） |
| name | text | field | 欄位名稱：料號、批號、壓合、機台、操作員 |
| prompt | text | field | 對應的 InSpec 提示文字，完全相同（不分大小寫）才填 |
| mode | text | field | fill（直接填）／confirm（先確認再填） |
| editable | boolean | field | 主頁上可否改 |
| sort | integer | field | 欄位順序，也是條碼分段的順序 |
| program | text | run | 執行的 .iwp 原檔完整路徑 |
| fields | jsonb NOT NULL DEFAULT '{}' | run | 這次執行的欄位值 `{"料號":"…","批號":"…",…}` |
| prompts | jsonb NOT NULL DEFAULT '[]' | run | 每個提示怎麼處理：`[{"prompt":"Part Number","value":"…","at":"…"}]` |
| started_at | timestamptz | run | 按下執行的時刻 |
| finished_at | timestamptz | run | iscmd 回來的時刻（＝InSpec 跑完） |
| exit_code | integer | run | iscmd 回傳代碼 |
| export | text | run | 這次匯出檔的內容 |
| archive | text | run | 實際寫入的歸檔檔路徑（主要路徑或備份路徑） |
| note | text | run | 沒歸檔或出錯時的原因 |
| created_at | timestamptz NOT NULL DEFAULT now() | 全部 | |
| updated_at | timestamptz NOT NULL DEFAULT now() | 全部 | |

不存狀態欄：一次執行跑到哪裡，由 `finished_at`、`archive`、`note` 有沒有值看出來。

### 2.2 讀取 `dbo.vw_inspec`

一支 view，兩種查詢：單一（Item）、列表（Items）。排序與計算由 view 做完。

輸出欄位：原始表全部欄位，再加：

| 欄位 | 算法 |
|---|---|
| elapsed_ms | `finished_at - started_at` 的毫秒數；沒跑完是 NULL |
| started_text | `started_at` 格式化成 `yyyy-MM-dd HH:mm:ss` |

排序：`type`，再 `sort`，再 `started_at DESC`，再 `id`。

### 2.3 初始資料

| type | 內容 |
|---|---|
| setting | searchFolder＝''、logFolder＝''、backupFolder＝''、scanInterval＝'80'（options 80/120/160/200/300/500）、showResult＝'true'、unlockCode＝'123' |
| field | 料號/part number/fill/可改/1、批號/lot number/fill/可改/2、壓合/press/confirm/不可改/3、機台/machine/fill/可改/4、操作員/operator/fill/可改/5 |

## 3. 畫面（照 UIUX.md）

一個 `Board`（UserControl），裡面切兩頁：**量測**、**設定**。字族 Microsoft JhengHei UI；4px 格線；圓角 4/8；強調色只用平台藍 `#0f6cbd`，只出現在主要按鈕與被選取的列；卡片不加陰影、1px 細線。

### 3.1 量測頁（61.8 : 38.2）

| 區 | 位置 | 內容 |
|---|---|---|
| 頁首 | 上 | 標題「InSpecTool」＋一句說明；右上「設定」 |
| 輸入（38.2%） | 左 | 條碼框（標籤在框外，placeholder「掃描或貼上條碼」）、清除；欄位清單（標籤在框外，不可改的唯讀） |
| 量測程式（61.8%） | 右 | 標題＋筆數＋「重新查找」；清單（檔名，新的在上，數字靠右）；雙擊或選取後按「執行」 |
| 執行中 | 右側清單下方 | 這一次的進度一行一行列出：開檔、每個提示填了什麼、跑完、歸檔到哪裡 |

狀態：載入（skeleton）、空（四種：沒條碼／沒設資料夾／找不到檔案／資料夾不存在，各一句人話）、錯誤（行內）、執行中（按鈕停用＋spinner）、完成。

### 3.2 結果

跑完歸檔後，`showResult` 為 true 就在右側蓋一層結果卡：
- 這次的欄位值、耗時、歸檔路徑
- 匯出內容攤成表格（Tab 分隔，一列一列）
- **左下角**勾選框「下次跑完也顯示結果」→ 寫回 `showResult`
- 右下「關閉」

### 3.3 設定頁

| 群 | 項目 |
|---|---|
| 路徑 | 搜尋資料夾、系統紀錄資料夾、備份輸出路徑（主要路徑寫不進去時才用），各附「瀏覽」 |
| 條碼 | 條碼解析間隔（下拉 80/120/160/200/300/500 ms） |
| 欄位結構 | 鎖住時唯讀；輸入解鎖號碼（`unlockCode`）解鎖 3 分鐘；解鎖後可新增、移除、改名稱／提示／模式／可否編輯，「儲存」 |

改了立刻存、主頁立刻生效。

## 4. 執行量測（一支方法：`Run`）

依序寫在同一支方法裡：

1. 檢查欄位：mode=fill 的不可空白
2. 檢查檔案：存在且 > 2 bytes
3. 寫一列 run：program、fields、started_at
4. 複製副本到 `%TEMP%\InSpecTool\run_{id}.iwp`：自動匯出指到 `export_{id}.txt`、`Enab 1`、`Unicode 1`、保持 CRLF
5. 找 InSpec：在跑就用；沒跑就從登錄檔的安裝位置啟動，等主視窗出現
6. 登記 Windows 通知（InSpec 行程的視窗出現）
7. 背景跑 `iscmd /run 副本`（不加 /nowait）
8. 每個對話框：有 1278 就是提示 → 文字完全相同（不分大小寫）對到欄位 → mode=confirm 先跳確認 → 填 499／1269／1297 → 送 IDOK；對不上就不填並記進 note
9. iscmd 回來：寫 finished_at、exit_code
10. 讀匯出檔 → 寫 export
11. 算歸檔路徑 → 附加；主要路徑寫不進去改備份路徑 → 寫 archive；都失敗寫 note
12. 解除通知；showResult 為 true 就顯示結果卡

iscmd、InSpec.exe 的位置：從登錄檔解除安裝資訊裡 InSpec 的安裝位置讀，不寫死。

## 5. 驗收

| 功能 | 驗證方式 | 通過標準 |
|---|---|---|
| 掃條碼 | 貼 `\|CDA343-AD\|LOT-0001\|STV-1\|M01\|OP1\|` | 五個欄位依序填好；料號整理後找得到 `CDA343-A` 資料夾裡的檔 |
| 找檔 | 搜尋資料夾指到 `供測試用 (1)` | 只列出檔名含 SETUP 和料號的 .iwp，新的在上 |
| 執行＋自動輸入 | 雙擊有 3 個提示的測試程式 | 3 個提示自動填好，iscmd 回傳 0 |
| 跑完才歸檔 | 同上 | iscmd 回來後才寫歸檔檔；資料庫 run 列有 finished_at、export、archive |
| 備份路徑 | 系統紀錄資料夾設成不存在的磁碟 | 寫到備份輸出路徑，archive 是備份路徑 |
| 結果顯示 | 跑完 | 結果卡出現；取消左下勾選後再跑一次不出現 |
| 解鎖 | 輸入 123 | 欄位結構可改；3 分鐘後鎖回 |
| 授權 | KEY 沒列 local.inspec | 匯入、開啟都被擋 |
