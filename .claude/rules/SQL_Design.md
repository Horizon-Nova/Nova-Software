# 資料庫設計

## 會變的資料在 `vw_` 裡處理完（過濾、分組、計數、平均、排序、格式化都由 view 做，程式只把欄位攤成畫面）

## 原始表只在寫入時碰（view 寫不進去）

## 新的讀取形狀先加 view（寫進 `deploy/database/init/*.sql`，同一個改動另寫一支 `deploy/database/migrate/` 的變更檔，套到本機庫後列進 `BuildModels/appsettings.json` 的清單，再執行 BuildModels 產生模型）

## 結構的每一次改動都同時寫兩處：`init` 讓從零建出來的庫就是現在的樣子，`migrate/{庫名}/` 的一支新檔讓已經在跑的庫改成同一個樣子（檔名照順序編號，不寫 `\connect`；網站啟動時自己套，套過的不再套）

view 是 DDL：加了不必重建資料庫，也不會掉資料。

## 每一張表的最後兩欄固定是 `created_at`、`updated_at`

## 每一張表、每一欄都寫 `COMMENT`

## 資料處理靠資料表之間的對應與 `vw_`，不寫承載業務流程的函式或觸發器

觸發器只做記帳：

- 編號（單號、客戶編號）
- 時刻（`created_at`、`updated_at`、結案時刻）
- jsonb 附屬紀錄補編號
- `pg_notify` 叫醒外部服務
- 黑名單判定：`event_log` 新增一件，照 `block_rule` 判定，成立就寫進 `block_list`

## 誰看得到哪幾列，由 view 帶出身分欄，查詢直接比對（不回查別的 view）

## 讀另一個資料庫的資料，在讀取者那一庫把對方的表掛成外部表，由那一支 `vw_` 直接 join（不在兩邊各開一支再由程式組合）

```sql
CREATE EXTENSION IF NOT EXISTS postgres_fdw;
CREATE SERVER other_db FOREIGN DATA WRAPPER postgres_fdw OPTIONS (host '/var/run/postgresql', dbname 'other');
CREATE USER MAPPING FOR CURRENT_USER SERVER other_db OPTIONS (user 'reader');
IMPORT FOREIGN SCHEMA dbo LIMIT TO (item_management) FROM SERVER other_db INTO dbo;
```

## 同一種東西的不同種類存在同一張表，以 `type` 區分（每多一種就多一個 `type` 值，不多開表）

## 流程只存流程本身，每一筆走到哪裡不存成狀態欄
