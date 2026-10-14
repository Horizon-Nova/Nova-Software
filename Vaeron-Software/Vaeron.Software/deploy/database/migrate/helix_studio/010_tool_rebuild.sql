-- tool_management 照 init 重建一次：後來加的 entry 到 settings 排在 created_at、updated_at 後面，
-- 最後兩欄要固定是 created_at、updated_at；PostgreSQL 不能調欄位順序，只能搬到新表
BEGIN;

DROP VIEW IF EXISTS dbo.vw_tool;
DROP VIEW IF EXISTS dbo.vw_tool_change;
DROP VIEW IF EXISTS dbo.vw_shell_summary;
DROP VIEW IF EXISTS dbo.vw_rail;
DROP VIEW IF EXISTS dbo.vw_tool_setting;
DROP VIEW IF EXISTS dbo.vw_key;

ALTER TABLE dbo.tool_management RENAME TO tool_management_old;
ALTER SEQUENCE dbo.tool_management_id_seq RENAME TO tool_management_old_id_seq;
ALTER TABLE dbo.tool_management_old RENAME CONSTRAINT tool_management_pkey TO tool_management_old_pkey;

CREATE TABLE dbo.tool_management (
    id           serial PRIMARY KEY,
    type         text NOT NULL,                       -- tool
    tool_no      text NOT NULL,                       -- 工具代碼
    name         text NOT NULL,                       -- 工具名稱
    icon         text NOT NULL,                       -- lucide 的圖示名稱
    cat          text NOT NULL,                       -- 分類
    vendor       text NOT NULL,                       -- 維護團隊
    version      text NOT NULL,                       -- 裝著的版本
    size         text NOT NULL,                       -- 套件大小
    updated      date NOT NULL,                       -- 匯入這一版的日期
    description  text NOT NULL,                       -- 說明
    capabilities jsonb NOT NULL DEFAULT '[]',         -- 這支工具會做的事
    permissions  jsonb NOT NULL DEFAULT '[]',         -- 會用到的權限
    changelog    jsonb NOT NULL DEFAULT '[]',         -- 版本與更新說明，一則一個元素，新到舊
    entry        text,                                -- 開起來要載入的組件檔名
    class        text,                                -- 組件裡那個畫面的型別全名
    home         text,                                -- 這支工具的檔案裝在哪一個資料夾
    source       text,                                -- 從哪裡來
    settings     text,                                -- 組件裡設定畫面的型別全名；沒有就是這支工具沒有設定
    created_at   timestamptz NOT NULL DEFAULT now(),
    updated_at   timestamptz NOT NULL DEFAULT now()
);

INSERT INTO dbo.tool_management (id, type, tool_no, name, icon, cat, vendor, version, size, updated, description,
                                 capabilities, permissions, changelog, entry, class, home, source, settings, created_at, updated_at)
SELECT id, type, tool_no, name, icon, cat, vendor, version, size, updated, description,
       capabilities, permissions, changelog, entry, class, home, source, settings, created_at, updated_at
  FROM dbo.tool_management_old;

SELECT setval('dbo.tool_management_id_seq', coalesce((SELECT max(id) FROM dbo.tool_management), 1));

DROP TABLE dbo.tool_management_old;

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

COMMIT;
