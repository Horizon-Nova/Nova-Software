-- 讀取全部交給 vw_：一支 view 一個讀取形狀，過濾、計數、格式化在 view 裡做完
-- 藍圖 .claude/blueprint/藍圖_平台資料庫.md

-- 一支工具一列：是否裝好、是否可更新、版本文字、日期文字、能力與權限攤成陣列
CREATE OR REPLACE VIEW dbo.vw_tool AS
SELECT t.tool_no,
       t.name,
       t.icon,
       t.cat,
       t.vendor,
       t.version,
       t.installed_version,
       t.state,
       t.state IN ('installed', 'update') AS installed,
       t.state = 'update' AS updatable,
       CASE WHEN t.state = 'update' THEN t.installed_version || ' → ' || t.version
            ELSE 'v' || coalesce(t.installed_version, t.version) END AS version_text,
       t.description,
       t.size,
       to_char(t.updated, 'YYYY/MM/DD') AS updated_text,
       t.auto_update,
       ARRAY(SELECT jsonb_array_elements_text(t.capabilities)) AS capabilities,
       ARRAY(SELECT jsonb_array_elements_text(t.permissions)) AS permissions,
       t.entry,
       t.class,
       t.home,
       t.source,
       t.settings,
       t.id AS sort,
       CASE t.state WHEN 'installed' THEN '已安裝' WHEN 'update' THEN '可更新' ELSE '未安裝' END AS state_text,
       CASE t.state WHEN 'installed' THEN '開啟' WHEN 'update' THEN '更新' ELSE '安裝' END AS action_text
  FROM dbo.tool_management AS t
 WHERE t.type = 'tool'
 ORDER BY t.id;

COMMENT ON VIEW dbo.vw_tool IS '一支工具一列：是否裝好、是否可更新、版本文字、日期文字、狀態文字、動作文字；能力與權限攤成陣列；照匯入先後排';

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

-- 平台整體只有一列：幾支工具、裝好幾支、可更新幾支、套件庫最後一次匯入
CREATE OR REPLACE VIEW dbo.vw_shell_summary AS
SELECT count(*) AS tool_count,
       count(*) FILTER (WHERE t.state IN ('installed', 'update')) AS installed_count,
       count(*) FILTER (WHERE t.state = 'update') AS updatable_count,
       coalesce(to_char(max(t.updated_at), 'YYYY-MM-DD HH24:MI'), '—') AS sync_text
  FROM dbo.tool_management AS t
 WHERE t.type = 'tool';

COMMENT ON VIEW dbo.vw_shell_summary IS '平台整體只有一列：工具總數、裝好幾支、可更新幾支、套件庫最後一次匯入的時刻';

-- 一個搜尋字一列：最近用到的在前
CREATE OR REPLACE VIEW dbo.vw_search AS
SELECT s.term,
       s.used_at
  FROM dbo.search_management AS s
 ORDER BY s.used_at DESC, s.id DESC;

COMMENT ON VIEW dbo.vw_search IS '一個搜尋字一列：最近用到的在前';

-- 側欄：裝好的工具一支一列，照匯入先後排
CREATE OR REPLACE VIEW dbo.vw_rail AS
SELECT t.tool_no,
       t.name,
       t.icon,
       t.id AS sort
  FROM dbo.tool_management AS t
 WHERE t.type = 'tool' AND t.state IN ('installed', 'update')
 ORDER BY t.id;

COMMENT ON VIEW dbo.vw_rail IS '側欄：裝好的工具一支一列（代碼、名稱、圖示），照匯入先後排';

-- 設定頁「工具」底下：裝好而且有設定畫面的工具一支一列，照匯入先後排
CREATE OR REPLACE VIEW dbo.vw_tool_setting AS
SELECT t.tool_no,
       t.name,
       t.settings,
       t.id AS sort
  FROM dbo.tool_management AS t
 WHERE t.type = 'tool' AND t.state IN ('installed', 'update') AND coalesce(t.settings, '') <> ''
 ORDER BY t.id;

COMMENT ON VIEW dbo.vw_tool_setting IS '設定頁「工具」底下：裝好而且有設定畫面的工具一支一列（代碼、名稱、設定畫面型別），照匯入先後排';
