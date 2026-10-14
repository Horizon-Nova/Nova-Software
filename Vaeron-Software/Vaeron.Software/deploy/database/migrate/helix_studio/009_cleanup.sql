-- 讀取全部交給 vw_ 的收尾：拿掉沒有資料來源的表與欄位、狀態欄；搜尋字補上最後兩欄
-- 藍圖 .claude/blueprint/藍圖_平台資料庫.md

-- 舊的大雜燴 view 與依賴要拿掉的欄位的 view 先拿掉，最後照 init 重建
DROP VIEW IF EXISTS dbo.vw_shell;
DROP VIEW IF EXISTS dbo.vw_tool;
DROP VIEW IF EXISTS dbo.vw_rail;
DROP VIEW IF EXISTS dbo.vw_tool_setting;
DROP VIEW IF EXISTS dbo.vw_shell_summary;
DROP VIEW IF EXISTS dbo.vw_key;

-- 假裝置與假使用者早就清空，這張表沒有任何程式在讀
DROP TABLE IF EXISTS dbo.device_management;

-- 沒有資料來源的欄位：必裝、釘選、徽章、自動更新、使用對象、操作說明、席次、使用量；
-- 狀態欄：匯入就是裝好、移除就是整列拿掉，state 與 installed_version 永遠是已安裝與目前版本
ALTER TABLE dbo.tool_management
    DROP COLUMN IF EXISTS required,
    DROP COLUMN IF EXISTS pinned,
    DROP COLUMN IF EXISTS badge,
    DROP COLUMN IF EXISTS auto_update,
    DROP COLUMN IF EXISTS audience,
    DROP COLUMN IF EXISTS guide,
    DROP COLUMN IF EXISTS seats,
    DROP COLUMN IF EXISTS usage,
    DROP COLUMN IF EXISTS state,
    DROP COLUMN IF EXISTS installed_version;

-- 搜尋字：最後兩欄固定是 created_at、updated_at
ALTER TABLE dbo.search_management
    ADD COLUMN IF NOT EXISTS created_at timestamptz NOT NULL DEFAULT now(),
    ADD COLUMN IF NOT EXISTS updated_at timestamptz NOT NULL DEFAULT now();

COMMENT ON TABLE dbo.tool_management IS '工具：匯入的每一支工具一列；匯入就是裝好，移除就是整列拿掉';
COMMENT ON COLUMN dbo.tool_management.id IS '流水號，也是側欄與工具目錄的先後';
COMMENT ON COLUMN dbo.tool_management.type IS 'tool';
COMMENT ON COLUMN dbo.tool_management.tool_no IS '工具代碼，套件 manifest 的 tool_no';
COMMENT ON COLUMN dbo.tool_management.name IS '工具名稱';
COMMENT ON COLUMN dbo.tool_management.icon IS 'lucide 的圖示名稱';
COMMENT ON COLUMN dbo.tool_management.cat IS '分類';
COMMENT ON COLUMN dbo.tool_management.vendor IS '維護團隊';
COMMENT ON COLUMN dbo.tool_management.version IS '裝著的版本';
COMMENT ON COLUMN dbo.tool_management.size IS '套件大小，例如 42.8 KB';
COMMENT ON COLUMN dbo.tool_management.updated IS '匯入這一版的日期';
COMMENT ON COLUMN dbo.tool_management.description IS '說明';
COMMENT ON COLUMN dbo.tool_management.capabilities IS '這支工具會做的事，字串陣列';
COMMENT ON COLUMN dbo.tool_management.permissions IS '會用到的權限，字串陣列';
COMMENT ON COLUMN dbo.tool_management.changelog IS '版本與更新說明，一則一個元素（ver、date、note），新到舊';
COMMENT ON COLUMN dbo.tool_management.entry IS '開起來要載入的組件檔名，例如 InSpecTool.dll';
COMMENT ON COLUMN dbo.tool_management.class IS '組件裡那個畫面的型別全名，例如 InSpecTool.Measure';
COMMENT ON COLUMN dbo.tool_management.home IS '這支工具的檔案裝在哪一個資料夾';
COMMENT ON COLUMN dbo.tool_management.source IS '從哪裡來，例如 本機套件檔';
COMMENT ON COLUMN dbo.tool_management.settings IS '組件裡設定畫面的型別全名，例如 InSpecTool.Setup；沒有就是這支工具沒有設定';
COMMENT ON COLUMN dbo.tool_management.created_at IS '第一次匯入的時刻';
COMMENT ON COLUMN dbo.tool_management.updated_at IS '最後一次匯入的時刻';

COMMENT ON TABLE dbo.search_management IS '搜尋過的字：一列一個字';
COMMENT ON COLUMN dbo.search_management.id IS '流水號';
COMMENT ON COLUMN dbo.search_management.type IS 'recent';
COMMENT ON COLUMN dbo.search_management.term IS '搜尋的字';
COMMENT ON COLUMN dbo.search_management.used_at IS '最近用到的時刻，決定排在前面';
COMMENT ON COLUMN dbo.search_management.created_at IS '第一次搜尋的時刻';
COMMENT ON COLUMN dbo.search_management.updated_at IS '最後修改時刻';

-- 一支工具一列：版本文字、日期文字；能力與權限攤成陣列；照匯入先後排
CREATE OR REPLACE VIEW dbo.vw_tool AS
SELECT t.tool_no,
       t.name,
       t.icon,
       t.cat,
       t.vendor,
       t.version,
       'v' || t.version AS version_text,
       t.description,
       t.size,
       to_char(t.updated, 'YYYY/MM/DD') AS updated_text,
       ARRAY(SELECT jsonb_array_elements_text(t.capabilities)) AS capabilities,
       ARRAY(SELECT jsonb_array_elements_text(t.permissions)) AS permissions,
       t.entry,
       t.class,
       t.home,
       t.source,
       t.settings,
       t.id AS sort
  FROM dbo.tool_management AS t
 WHERE t.type = 'tool'
 ORDER BY t.id;

COMMENT ON VIEW dbo.vw_tool IS '一支工具一列：版本文字、日期文字；能力與權限攤成陣列；照匯入先後排';

-- 一則更新日誌一列：照工具、新到舊
CREATE OR REPLACE VIEW dbo.vw_tool_change AS
SELECT t.tool_no,
       c.item ->> 'ver' AS ver,
       c.item ->> 'date' AS date,
       c.item ->> 'note' AS note,
       c.at = 1 AS latest,
       c.at
  FROM dbo.tool_management AS t
  CROSS JOIN LATERAL jsonb_array_elements(t.changelog) WITH ORDINALITY AS c(item, at)
 WHERE t.type = 'tool'
 ORDER BY t.tool_no, c.at;

COMMENT ON VIEW dbo.vw_tool_change IS '一則更新日誌一列：版本、日期、說明、是不是最新那一則；照工具、新到舊';

-- 平台整體只有一列：幾支工具、套件庫最後一次匯入
CREATE OR REPLACE VIEW dbo.vw_shell_summary AS
SELECT count(*) AS tool_count,
       coalesce(to_char(max(t.updated_at), 'YYYY-MM-DD HH24:MI'), '—') AS sync_text
  FROM dbo.tool_management AS t
 WHERE t.type = 'tool';

COMMENT ON VIEW dbo.vw_shell_summary IS '平台整體只有一列：工具總數、套件庫最後一次匯入的時刻';

-- 一個搜尋字一列：最近用到的在前
CREATE OR REPLACE VIEW dbo.vw_search AS
SELECT s.term,
       s.used_at
  FROM dbo.search_management AS s
 ORDER BY s.used_at DESC, s.id DESC;

COMMENT ON VIEW dbo.vw_search IS '一個搜尋字一列：最近用到的在前';

-- 側欄：每一支工具一列，照匯入先後排
CREATE OR REPLACE VIEW dbo.vw_rail AS
SELECT t.tool_no,
       t.name,
       t.icon,
       t.id AS sort
  FROM dbo.tool_management AS t
 WHERE t.type = 'tool'
 ORDER BY t.id;

COMMENT ON VIEW dbo.vw_rail IS '側欄：每一支工具一列（代碼、名稱、圖示），照匯入先後排';

-- 設定頁「工具」底下：有設定畫面的工具一支一列，照匯入先後排
CREATE OR REPLACE VIEW dbo.vw_tool_setting AS
SELECT t.tool_no,
       t.name,
       t.settings,
       t.id AS sort
  FROM dbo.tool_management AS t
 WHERE t.type = 'tool' AND coalesce(t.settings, '') <> ''
 ORDER BY t.id;

COMMENT ON VIEW dbo.vw_tool_setting IS '設定頁「工具」底下：有設定畫面的工具一支一列（代碼、名稱、設定畫面型別），照匯入先後排';

-- 每支工具一列：存著的金鑰、它的到期日、啟用時刻、開啟與解鎖的次數和最後一次；最近用過的排前面
CREATE OR REPLACE VIEW dbo.vw_key AS
SELECT t.tool_no,
       t.name,
       k.code,
       CASE WHEN k.code IS NULL THEN NULL
            ELSE convert_from(decode(k.code, 'base64'), 'UTF8')::jsonb ->> 'Expires' END AS expires,
       to_char(u.activated_at, 'YYYY-MM-DD HH24:MI') AS activated_text,
       coalesce(u.opens, 0) AS opens,
       to_char(u.last_open, 'YYYY-MM-DD HH24:MI') AS last_open_text,
       coalesce(u.unlocks, 0) AS unlocks,
       to_char(u.last_unlock, 'YYYY-MM-DD HH24:MI') AS last_unlock_text,
       greatest(u.last_open, u.last_unlock, u.activated_at) AS last_used
  FROM dbo.tool_management AS t
  LEFT JOIN LATERAL (
        SELECT one.code
          FROM dbo.key_management AS one
         WHERE one.type = 'key' AND one.tool_no = t.tool_no
         ORDER BY one.created_at DESC, one.id DESC
         LIMIT 1) AS k ON true
  LEFT JOIN LATERAL (
        SELECT min(one.created_at) FILTER (WHERE one.action = 'activate') AS activated_at,
               count(*) FILTER (WHERE one.action = 'open') AS opens,
               max(one.created_at) FILTER (WHERE one.action = 'open') AS last_open,
               count(*) FILTER (WHERE one.action = 'unlock') AS unlocks,
               max(one.created_at) FILTER (WHERE one.action = 'unlock') AS last_unlock
          FROM dbo.key_management AS one
         WHERE one.type = 'use' AND one.tool_no = t.tool_no) AS u ON true
 WHERE t.type = 'tool'
 ORDER BY greatest(u.last_open, u.last_unlock, u.activated_at) DESC NULLS LAST, t.name;

COMMENT ON VIEW dbo.vw_key IS '每支工具一列：工具金鑰、到期日、啟用時刻、開啟與解鎖設定的次數和最後一次；最近用過的排前面';
