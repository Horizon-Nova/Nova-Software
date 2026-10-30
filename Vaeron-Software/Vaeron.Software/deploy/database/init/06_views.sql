-- 一頁一支 view：那一頁讀的每一樣東西都從那一支來；同一頁要的不同東西用 type 區分
-- 過濾、計數、排序、格式化都在這裡做完；給值不給標籤，不解析文字

-- 外殼（側欄、狀態列、開工具）：shell 一列（工具總數、最後匯入），tool 一支還裝著的工具一列（開它要的檔、最新那一把工具金鑰、自動啟動）
CREATE VIEW dbo.vw_shell AS
WITH installed AS (
    SELECT DISTINCT ON (t.tool_no) t.*,
           (SELECT min(f.id) FROM dbo.tool AS f WHERE f.type = 'tool' AND f.tool_no = t.tool_no) AS first_id
      FROM dbo.tool AS t
     WHERE t.type = 'tool'
       AND NOT EXISTS (SELECT 1 FROM dbo.tool AS r
                        WHERE r.type = 'remove' AND r.tool_no = t.tool_no
                          AND (r.created_at, r.id) > (t.created_at, t.id))
     ORDER BY t.tool_no, t.created_at DESC, t.id DESC)
SELECT 'shell'::character varying(5) AS type,
       NULL::integer AS id,
       NULL::character varying(255) AS tool_no,
       NULL::character varying(50) AS name,
       NULL::character varying(50) AS icon,
       NULL::character varying(255) AS entry,
       NULL::character varying(1023) AS class,
       NULL::character varying(32767) AS home,
       NULL::text AS code,
       NULL::timestamp with time zone AS expires_at,
       NULL::boolean AS autostart,
       (SELECT count(*) FROM installed)::integer AS tool_count,
       coalesce((SELECT to_char(max(i.created_at), 'YYYY-MM-DD HH24:MI') FROM installed AS i), '—')::character varying(16) AS sync_text,
       0::bigint AS sort
UNION ALL
SELECT 'tool', t.id, t.tool_no, t.name, t.icon, t.entry, t.class, t.home, k.code, k.expires_at, coalesce(a.autostart, false),
       NULL, NULL,
       row_number() OVER (ORDER BY t.first_id)
  FROM installed AS t
  LEFT JOIN LATERAL (
        SELECT one.code, one.expires_at
          FROM dbo.tool AS one
         WHERE one.type = 'key' AND one.tool_no = t.tool_no
         ORDER BY one.created_at DESC, one.id DESC
         LIMIT 1) AS k ON true
  LEFT JOIN LATERAL (
        SELECT one.autostart
          FROM dbo.tool AS one
         WHERE one.type = 'start' AND one.tool_no = t.tool_no
         ORDER BY one.created_at DESC, one.id DESC
         LIMIT 1) AS a ON true
 ORDER BY type, sort;

COMMENT ON VIEW dbo.vw_shell IS '外殼：shell 一列是工具總數與最後匯入的時刻；tool 一支還裝著的工具一列，側欄照 sort 排，開工具時讀它的組件、畫面型別、資料夾、最新那一把工具金鑰與自動啟動';
COMMENT ON COLUMN dbo.vw_shell.type IS 'shell＝外殼那一列；tool＝一支工具';
COMMENT ON COLUMN dbo.vw_shell.id IS '[tool] 裝著的那一次匯入的流水號';
COMMENT ON COLUMN dbo.vw_shell.tool_no IS '[tool] 工具代碼';
COMMENT ON COLUMN dbo.vw_shell.name IS '[tool] 工具名稱';
COMMENT ON COLUMN dbo.vw_shell.icon IS '[tool] lucide 的圖示名稱';
COMMENT ON COLUMN dbo.vw_shell.entry IS '[tool] 要載入的組件檔名';
COMMENT ON COLUMN dbo.vw_shell.class IS '[tool] 畫面的型別全名';
COMMENT ON COLUMN dbo.vw_shell.home IS '[tool] 工具的檔案裝在哪一個資料夾';
COMMENT ON COLUMN dbo.vw_shell.code IS '[tool] 最新那一把工具金鑰；沒啟用過是空的';
COMMENT ON COLUMN dbo.vw_shell.expires_at IS '[tool] 那一把金鑰到哪一刻為止；沒有到期日是空的';
COMMENT ON COLUMN dbo.vw_shell.autostart IS '[tool] 自動啟動：最新那一次切的值；沒切過是 false';
COMMENT ON COLUMN dbo.vw_shell.tool_count IS '[shell] 還裝著的工具支數';
COMMENT ON COLUMN dbo.vw_shell.sync_text IS '[shell] 裝著的工具裡最後一次匯入的時刻 YYYY-MM-DD HH24:MI；沒有工具是破折號';
COMMENT ON COLUMN dbo.vw_shell.sort IS '[tool] 側欄的先後，照第一次匯入的先後';

-- 工具目錄：total 一列（工具總數）；tool 一支還裝著的工具一列（最近開過的在前）；change 一則更新日誌一列（新到舊）；step 操作說明一步一列；search 一個搜尋過的字一列（最近搜的在前）
CREATE VIEW dbo.vw_catalog AS
WITH installed AS (
    SELECT DISTINCT ON (t.tool_no) t.*,
           (SELECT min(f.id) FROM dbo.tool AS f WHERE f.type = 'tool' AND f.tool_no = t.tool_no) AS first_id
      FROM dbo.tool AS t
     WHERE t.type = 'tool'
       AND NOT EXISTS (SELECT 1 FROM dbo.tool AS r
                        WHERE r.type = 'remove' AND r.tool_no = t.tool_no
                          AND (r.created_at, r.id) > (t.created_at, t.id))
     ORDER BY t.tool_no, t.created_at DESC, t.id DESC),
tools AS (
    SELECT t.*,
           k.expires_at AS key_expires_at,
           coalesce(a.autostart, false) AS autostart_now,
           u.opens,
           u.last_open
      FROM installed AS t
      LEFT JOIN LATERAL (
            SELECT one.expires_at
              FROM dbo.tool AS one
             WHERE one.type = 'key' AND one.tool_no = t.tool_no
             ORDER BY one.created_at DESC, one.id DESC
             LIMIT 1) AS k ON true
      LEFT JOIN LATERAL (
            SELECT one.autostart
              FROM dbo.tool AS one
             WHERE one.type = 'start' AND one.tool_no = t.tool_no
             ORDER BY one.created_at DESC, one.id DESC
             LIMIT 1) AS a ON true
      LEFT JOIN LATERAL (
            SELECT count(*) AS opens, max(e.created_at) AS last_open
              FROM dbo.event_log AS e
             WHERE e.type = 'open' AND e.tool_no = t.tool_no) AS u ON true)
SELECT 'total'::character varying(6) AS type,
       NULL::integer AS id,
       count(*)::integer AS tool_count,
       NULL::character varying(255) AS tool_no,
       NULL::character varying(50) AS name,
       NULL::character varying(50) AS icon,
       NULL::character varying(50) AS vendor,
       NULL::character varying(500) AS description,
       NULL::character varying(24) AS version_text,
       NULL::character varying(16) AS size_text,
       NULL::character varying(10) AS imported_text,
       NULL::character varying(200)[] AS capabilities,
       NULL::character varying(200)[] AS permissions,
       NULL::character varying(32767) AS home,
       NULL::character varying(16) AS last_open_text,
       NULL::integer AS opens,
       NULL::character varying(10) AS expires_text,
       NULL::integer AS remaining_days,
       NULL::boolean AS autostart,
       NULL::text AS ver,
       NULL::text AS date,
       NULL::text AS note,
       NULL::character varying(100) AS term,
       NULL::text AS image,
       0::bigint AS sort
  FROM tools
UNION ALL
SELECT 'tool', t.id, NULL, t.tool_no, t.name, t.icon, t.vendor, t.description,
       ('v' || t.version)::character varying(24),
       (CASE WHEN t.bytes < 1024 THEN t.bytes || ' B'
             WHEN t.bytes < 1048576 THEN to_char(t.bytes / 1024.0, 'FM999990.0') || ' KB'
             ELSE to_char(t.bytes / 1048576.0, 'FM999990.0') || ' MB' END)::character varying(16),
       to_char(t.created_at, 'YYYY/MM/DD')::character varying(10),
       t.capabilities, t.permissions, t.home,
       to_char(t.last_open, 'YYYY-MM-DD HH24:MI')::character varying(16),
       t.opens::integer,
       to_char(t.key_expires_at, 'YYYY-MM-DD')::character varying(10),
       (t.key_expires_at::date - current_date)::integer,
       t.autostart_now,
       NULL, NULL, NULL, NULL, NULL,
       row_number() OVER (ORDER BY t.last_open DESC NULLS LAST, t.first_id)
  FROM tools AS t
UNION ALL
SELECT 'change', NULL, NULL, t.tool_no, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL,
       c.item ->> 'ver', c.item ->> 'date', c.item ->> 'note', NULL, NULL, c.at
  FROM tools AS t
 CROSS JOIN LATERAL jsonb_array_elements(t.changelog) WITH ORDINALITY AS c(item, at)
UNION ALL
SELECT 'step', NULL, NULL, t.tool_no, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL,
       NULL, NULL, g.item ->> 'text', NULL,
       CASE WHEN coalesce(g.item ->> 'image', '') = '' THEN NULL ELSE t.home || chr(92) || replace(g.item ->> 'image', '/', chr(92)) END,
       g.at
  FROM tools AS t
 CROSS JOIN LATERAL jsonb_array_elements(t.guide) WITH ORDINALITY AS g(item, at)
UNION ALL
SELECT 'search', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL,
       NULL, NULL, NULL, s.term, NULL,
       row_number() OVER (ORDER BY max(s.created_at) DESC)
  FROM dbo.search AS s
 GROUP BY s.term
 ORDER BY type, sort;

COMMENT ON VIEW dbo.vw_catalog IS '工具目錄：total 是還裝著幾支；tool 一支工具一列，最近開過的在前；change 一則更新日誌一列，新到舊；step 操作說明一步一列；search 一個搜尋過的字一列，最近用到的在前';
COMMENT ON COLUMN dbo.vw_catalog.type IS 'total＝總數；tool＝一支工具；change＝一則更新日誌；step＝操作說明的一步；search＝一個搜尋過的字';
COMMENT ON COLUMN dbo.vw_catalog.id IS '[tool] 裝著的那一次匯入的流水號';
COMMENT ON COLUMN dbo.vw_catalog.tool_count IS '[total] 還裝著的工具支數';
COMMENT ON COLUMN dbo.vw_catalog.tool_no IS '[tool][change][step] 工具代碼';
COMMENT ON COLUMN dbo.vw_catalog.name IS '[tool] 工具名稱';
COMMENT ON COLUMN dbo.vw_catalog.icon IS '[tool] lucide 的圖示名稱';
COMMENT ON COLUMN dbo.vw_catalog.vendor IS '[tool] 維護團隊';
COMMENT ON COLUMN dbo.vw_catalog.description IS '[tool] 說明';
COMMENT ON COLUMN dbo.vw_catalog.version_text IS '[tool] 版本，前面加 v';
COMMENT ON COLUMN dbo.vw_catalog.size_text IS '[tool] 套件大小：B、KB、MB，一位小數';
COMMENT ON COLUMN dbo.vw_catalog.imported_text IS '[tool] 匯入這一版的日期 YYYY/MM/DD';
COMMENT ON COLUMN dbo.vw_catalog.capabilities IS '[tool] 這支工具會做的事';
COMMENT ON COLUMN dbo.vw_catalog.permissions IS '[tool] 會用到的權限';
COMMENT ON COLUMN dbo.vw_catalog.home IS '[tool] 工具的檔案裝在哪一個資料夾';
COMMENT ON COLUMN dbo.vw_catalog.last_open_text IS '[tool] 最後一次開啟 YYYY-MM-DD HH24:MI；沒開過是空的';
COMMENT ON COLUMN dbo.vw_catalog.opens IS '[tool] 開過幾次';
COMMENT ON COLUMN dbo.vw_catalog.expires_text IS '[tool] 工具金鑰到期日 YYYY-MM-DD；沒有到期日或沒啟用過是空的';
COMMENT ON COLUMN dbo.vw_catalog.remaining_days IS '[tool] 離金鑰到期還有幾天；過了是負的，照樣列出；沒有到期日是空的';
COMMENT ON COLUMN dbo.vw_catalog.autostart IS '[tool] 自動啟動：最新那一次切的值；沒切過是 false';
COMMENT ON COLUMN dbo.vw_catalog.ver IS '[change] 版本';
COMMENT ON COLUMN dbo.vw_catalog.date IS '[change] 日期';
COMMENT ON COLUMN dbo.vw_catalog.note IS '[change] 更新說明；[step] 這一步做什麼';
COMMENT ON COLUMN dbo.vw_catalog.term IS '[search] 搜尋過的字';
COMMENT ON COLUMN dbo.vw_catalog.image IS '[step] 截圖的完整路徑；沒有圖是空的';
COMMENT ON COLUMN dbo.vw_catalog.sort IS '[tool] 最近開過的在前；[change] 新到舊；[step] 步驟順序；[search] 最近用到的在前';

-- 指令面板：tool 一支還裝著的工具一列，search 一個搜尋過的字一列（最近搜的在前）
CREATE VIEW dbo.vw_palette AS
WITH installed AS (
    SELECT DISTINCT ON (t.tool_no) t.*,
           (SELECT min(f.id) FROM dbo.tool AS f WHERE f.type = 'tool' AND f.tool_no = t.tool_no) AS first_id
      FROM dbo.tool AS t
     WHERE t.type = 'tool'
       AND NOT EXISTS (SELECT 1 FROM dbo.tool AS r
                        WHERE r.type = 'remove' AND r.tool_no = t.tool_no
                          AND (r.created_at, r.id) > (t.created_at, t.id))
     ORDER BY t.tool_no, t.created_at DESC, t.id DESC)
SELECT 'tool'::character varying(6) AS type,
       t.tool_no,
       t.name,
       t.icon,
       t.vendor,
       NULL::character varying(100) AS term,
       (SELECT count(*) FROM installed)::integer AS tool_count,
       t.first_id::bigint AS sort
  FROM installed AS t
UNION ALL
SELECT 'search', NULL, NULL, NULL, NULL, s.term,
       (SELECT count(*) FROM installed)::integer,
       row_number() OVER (ORDER BY max(s.created_at) DESC)
  FROM dbo.search AS s
 GROUP BY s.term
 ORDER BY type, sort;

COMMENT ON VIEW dbo.vw_palette IS '指令面板：tool 一支還裝著的工具一列，照匯入的先後；search 一個搜尋過的字一列，最近用到的在前';
COMMENT ON COLUMN dbo.vw_palette.type IS 'tool＝一支工具；search＝一個搜尋過的字';
COMMENT ON COLUMN dbo.vw_palette.tool_no IS '[tool] 工具代碼';
COMMENT ON COLUMN dbo.vw_palette.name IS '[tool] 工具名稱';
COMMENT ON COLUMN dbo.vw_palette.icon IS '[tool] lucide 的圖示名稱';
COMMENT ON COLUMN dbo.vw_palette.vendor IS '[tool] 維護團隊';
COMMENT ON COLUMN dbo.vw_palette.term IS '[search] 搜尋過的字';
COMMENT ON COLUMN dbo.vw_palette.tool_count IS '還裝著的工具支數（每一列都帶）';
COMMENT ON COLUMN dbo.vw_palette.sort IS '[tool] 第一次匯入的先後；[search] 最近用到的在前';

-- 設定：setting 一項設定一列（現在的值、建議的值、跟建議的不一樣）；tool 一支還裝著的工具一列（設定畫面、解鎖分鐘、工具金鑰、使用次數；最近用過的在前）
CREATE VIEW dbo.vw_settings AS
WITH installed AS (
    SELECT DISTINCT ON (t.tool_no) t.*,
           (SELECT min(f.id) FROM dbo.tool AS f WHERE f.type = 'tool' AND f.tool_no = t.tool_no) AS first_id
      FROM dbo.tool AS t
     WHERE t.type = 'tool'
       AND NOT EXISTS (SELECT 1 FROM dbo.tool AS r
                        WHERE r.type = 'remove' AND r.tool_no = t.tool_no
                          AND (r.created_at, r.id) > (t.created_at, t.id))
     ORDER BY t.tool_no, t.created_at DESC, t.id DESC)
SELECT 'setting'::character varying(7) AS type,
       v.id,
       f.id AS fallback_id,
       v.setting_key,
       v.enabled,
       v.number,
       v.path,
       f.enabled AS fallback_enabled,
       f.number AS fallback_number,
       f.path AS fallback_path,
       (v.enabled IS DISTINCT FROM f.enabled OR v.number IS DISTINCT FROM f.number OR v.path IS DISTINCT FROM f.path) AS moved,
       NULL::character varying(255) AS tool_no,
       NULL::character varying(50) AS name,
       NULL::character varying(255) AS entry,
       NULL::character varying(32767) AS home,
       NULL::character varying(1023) AS settings,
       NULL::integer AS unlock_minutes,
       NULL::character varying(10) AS expires_text,
       NULL::integer AS remaining_days,
       NULL::character varying(16) AS activated_text,
       NULL::integer AS opens,
       NULL::character varying(16) AS last_open_text,
       NULL::integer AS unlocks,
       NULL::character varying(16) AS last_unlock_text,
       v.id::bigint AS sort
  FROM dbo.setting AS v
  LEFT JOIN dbo.setting AS f ON f.type = 'fallback' AND f.setting_key = v.setting_key
 WHERE v.type = 'value'
UNION ALL
SELECT 'tool', t.id, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL,
       t.tool_no, t.name, t.entry, t.home, t.settings,
       (SELECT m.number FROM dbo.setting AS m WHERE m.type = 'value' AND m.setting_key = 'toolKeyMinutes'),
       to_char(k.expires_at, 'YYYY-MM-DD')::character varying(10),
       (k.expires_at::date - current_date)::integer,
       to_char(u.activated_at, 'YYYY-MM-DD HH24:MI')::character varying(16),
       coalesce(u.opens, 0)::integer,
       to_char(u.last_open, 'YYYY-MM-DD HH24:MI')::character varying(16),
       coalesce(u.unlocks, 0)::integer,
       to_char(u.last_unlock, 'YYYY-MM-DD HH24:MI')::character varying(16),
       row_number() OVER (ORDER BY greatest(u.last_open, u.last_unlock, u.activated_at) DESC NULLS LAST, t.name)
  FROM installed AS t
  LEFT JOIN LATERAL (
        SELECT one.expires_at
          FROM dbo.tool AS one
         WHERE one.type = 'key' AND one.tool_no = t.tool_no
         ORDER BY one.created_at DESC, one.id DESC
         LIMIT 1) AS k ON true
  LEFT JOIN LATERAL (
        SELECT min(e.created_at) FILTER (WHERE e.type = 'activate') AS activated_at,
               count(*) FILTER (WHERE e.type = 'open') AS opens,
               max(e.created_at) FILTER (WHERE e.type = 'open') AS last_open,
               count(*) FILTER (WHERE e.type = 'unlock') AS unlocks,
               max(e.created_at) FILTER (WHERE e.type = 'unlock') AS last_unlock
          FROM dbo.event_log AS e
         WHERE e.tool_no = t.tool_no) AS u ON true
 ORDER BY type, sort;

COMMENT ON VIEW dbo.vw_settings IS '設定：setting 一項設定一列（現在的值、資訊部建議的值、跟建議的不一樣）；tool 一支還裝著的工具一列，有 settings 的才有設定畫面，帶解鎖分鐘、工具金鑰到期、啟用時刻、開啟與解鎖次數和最後一次，最近用過的在前';
COMMENT ON COLUMN dbo.vw_settings.type IS 'setting＝一項設定；tool＝一支工具';
COMMENT ON COLUMN dbo.vw_settings.id IS '[setting] 現在的值那一列的流水號；[tool] 裝著的那一次匯入的流水號';
COMMENT ON COLUMN dbo.vw_settings.fallback_id IS '[setting] 建議的值那一列的流水號';
COMMENT ON COLUMN dbo.vw_settings.setting_key IS '[setting] 哪一項（見 setting.setting_key）';
COMMENT ON COLUMN dbo.vw_settings.enabled IS '[setting] 開關類現在的值';
COMMENT ON COLUMN dbo.vw_settings.number IS '[setting] 數字類現在的值';
COMMENT ON COLUMN dbo.vw_settings.path IS '[setting] 路徑類現在的值';
COMMENT ON COLUMN dbo.vw_settings.fallback_enabled IS '[setting] 開關類資訊部建議的值';
COMMENT ON COLUMN dbo.vw_settings.fallback_number IS '[setting] 數字類資訊部建議的值';
COMMENT ON COLUMN dbo.vw_settings.fallback_path IS '[setting] 路徑類資訊部建議的值';
COMMENT ON COLUMN dbo.vw_settings.moved IS '[setting] 現在的值跟資訊部建議的不一樣';
COMMENT ON COLUMN dbo.vw_settings.tool_no IS '[tool] 工具代碼';
COMMENT ON COLUMN dbo.vw_settings.name IS '[tool] 工具名稱';
COMMENT ON COLUMN dbo.vw_settings.entry IS '[tool] 要載入的組件檔名';
COMMENT ON COLUMN dbo.vw_settings.home IS '[tool] 工具的檔案裝在哪一個資料夾';
COMMENT ON COLUMN dbo.vw_settings.settings IS '[tool] 設定畫面的型別全名；沒有就是這支工具沒有設定';
COMMENT ON COLUMN dbo.vw_settings.unlock_minutes IS '[tool] 工具設定解鎖幾分鐘（setting 的 toolKeyMinutes）';
COMMENT ON COLUMN dbo.vw_settings.expires_text IS '[tool] 工具金鑰到期日 YYYY-MM-DD；沒有到期日或沒啟用過是空的';
COMMENT ON COLUMN dbo.vw_settings.remaining_days IS '[tool] 離金鑰到期還有幾天；過了是負的；沒有到期日是空的';
COMMENT ON COLUMN dbo.vw_settings.activated_text IS '[tool] 第一次啟用的時刻 YYYY-MM-DD HH24:MI';
COMMENT ON COLUMN dbo.vw_settings.opens IS '[tool] 開過幾次';
COMMENT ON COLUMN dbo.vw_settings.last_open_text IS '[tool] 最後一次開啟 YYYY-MM-DD HH24:MI';
COMMENT ON COLUMN dbo.vw_settings.unlocks IS '[tool] 解鎖過幾次';
COMMENT ON COLUMN dbo.vw_settings.last_unlock_text IS '[tool] 最後一次解鎖 YYYY-MM-DD HH24:MI';
COMMENT ON COLUMN dbo.vw_settings.sort IS '[setting] 建立的先後；[tool] 最近用過的在前';

-- 日誌：count 一種事件一列（幾筆，多的在前），event 一件一列（新的在前）；事件名是設計圖上固定的字，照 event_type 寫在日誌頁的 XAML
CREATE VIEW dbo.vw_event_log AS
SELECT 'count'::character varying(5) AS type,
       NULL::integer AS id,
       e.type AS event_type,
       count(*)::integer AS event_count,
       NULL::character varying(255) AS tool_no,
       NULL::character varying(255) AS tool_name,
       NULL::character varying(20) AS account,
       NULL::character varying(2047) AS function_name,
       NULL::text AS message,
       NULL::text AS stack_trace,
       NULL::timestamp with time zone AS created_at,
       NULL::character varying(19) AS created_text,
       row_number() OVER (ORDER BY count(*) DESC, e.type) AS sort
  FROM dbo.event_log AS e
 GROUP BY e.type
UNION ALL
SELECT 'event', e.id, e.type, NULL,
       e.tool_no, coalesce(t.name, e.tool_no)::character varying(255), e.account, e.function_name, e.message, e.stack_trace,
       e.created_at, to_char(e.created_at, 'YYYY-MM-DD HH24:MI:SS')::character varying(19),
       row_number() OVER (ORDER BY e.created_at DESC, e.id DESC)
  FROM dbo.event_log AS e
  LEFT JOIN LATERAL (
        SELECT one.name
          FROM dbo.tool AS one
         WHERE one.type = 'tool' AND one.tool_no = e.tool_no
         ORDER BY one.created_at DESC, one.id DESC
         LIMIT 1) AS t ON true
 ORDER BY type, sort;

COMMENT ON VIEW dbo.vw_event_log IS '日誌：count 一種事件一列（幾筆，多的在前）；event 一件一列（時間、事件、工具名稱、帳號、方法、訊息、呼叫堆疊，新的在前）';
COMMENT ON COLUMN dbo.vw_event_log.type IS 'count＝一種事件的筆數；event＝一件事';
COMMENT ON COLUMN dbo.vw_event_log.id IS '[event] event_log 的流水號';
COMMENT ON COLUMN dbo.vw_event_log.event_type IS 'activate、open、unlock、error（見 event_log.type）';
COMMENT ON COLUMN dbo.vw_event_log.event_count IS '[count] 這種事件幾筆';
COMMENT ON COLUMN dbo.vw_event_log.tool_no IS '[event] 哪一支工具';
COMMENT ON COLUMN dbo.vw_event_log.tool_name IS '[event] 工具最後一次匯入時的名稱；沒匯入過是代碼';
COMMENT ON COLUMN dbo.vw_event_log.account IS '[event] 當時登入 Windows 的帳號';
COMMENT ON COLUMN dbo.vw_event_log.function_name IS '[event] 系統由哪一支方法處理';
COMMENT ON COLUMN dbo.vw_event_log.message IS '[event] 處理的結果；錯誤時是錯誤訊息';
COMMENT ON COLUMN dbo.vw_event_log.stack_trace IS '[event] 錯誤時的呼叫堆疊';
COMMENT ON COLUMN dbo.vw_event_log.created_at IS '[event] 發生的時刻（照時間排序用）';
COMMENT ON COLUMN dbo.vw_event_log.created_text IS '[event] 發生的時刻 YYYY-MM-DD HH24:MI:SS';
COMMENT ON COLUMN dbo.vw_event_log.sort IS '[count] 多的在前；[event] 新的在前';

-- InSpecTool 量測頁：setting 一項設定一列，field 一個還在的欄位一列（照順序），run 一次執行一列（新的在前）
CREATE VIEW dbo.vw_inspec_measure AS
SELECT 'setting'::character varying(7) AS type,
       i.id,
       i.setting_key,
       i.enabled,
       i.number,
       i.path,
       NULL::character varying(50) AS name,
       NULL::character varying(255) AS prompt,
       NULL::character varying(7) AS mode,
       NULL::boolean AS editable,
       NULL::character varying(19) AS started_text,
       NULL::numeric AS seconds,
       NULL::character varying(32767) AS archive,
       NULL::text AS note,
       NULL::character varying(32767) AS program_name,
       NULL::text[] AS field_values,
       NULL::character varying(11) AS run_text,
       NULL::character varying(32767) AS program,
       NULL::jsonb AS fields,
       NULL::jsonb AS prompts,
       NULL::timestamp with time zone AS started_at,
       NULL::timestamp with time zone AS finished_at,
       NULL::integer AS exit_code,
       NULL::text AS export,
       0::bigint AS sort
  FROM dbo.inspec AS i
 WHERE i.type = 'setting'
UNION ALL
SELECT 'field', i.id, NULL, NULL, NULL, NULL,
       i.name, i.prompt, i.mode, i.editable, NULL, NULL, NULL, NULL, NULL, NULL, NULL,
       NULL, NULL, NULL, NULL, NULL, NULL, NULL,
       i.sort
  FROM dbo.inspec AS i
 WHERE i.type = 'field' AND i.removed_at IS NULL
UNION ALL
SELECT 'run', i.id, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL,
       to_char(i.started_at, 'YYYY-MM-DD HH24:MI:SS')::character varying(19),
       round(extract(epoch FROM (i.finished_at - i.started_at))::numeric, 1),
       i.archive,
       i.note,
       reverse(split_part(reverse(coalesce(i.program, '')), chr(92), 1))::character varying(32767),
       ARRAY(SELECT coalesce(i.fields ->> f.name, '')
               FROM dbo.inspec AS f
              WHERE f.type = 'field' AND f.removed_at IS NULL
              ORDER BY f.sort, f.id),
       to_char(i.started_at, 'MM-DD HH24:MI')::character varying(11),
       i.program,
       i.fields,
       i.prompts,
       i.started_at,
       i.finished_at,
       i.exit_code,
       i.export,
       row_number() OVER (ORDER BY i.started_at DESC, i.id DESC)
  FROM dbo.inspec AS i
 WHERE i.type = 'run'
 ORDER BY type DESC, sort;

COMMENT ON VIEW dbo.vw_inspec_measure IS 'InSpecTool 量測頁：setting 一項設定一列；field 一個還在的欄位一列，照順序；run 一次執行一列，新的在前';
COMMENT ON COLUMN dbo.vw_inspec_measure.type IS 'setting＝一項設定；field＝一個欄位；run＝一次執行';
COMMENT ON COLUMN dbo.vw_inspec_measure.id IS 'inspec 那一列的流水號（寫回去照它換整列）';
COMMENT ON COLUMN dbo.vw_inspec_measure.setting_key IS '[setting] 哪一項（見 inspec.setting_key）';
COMMENT ON COLUMN dbo.vw_inspec_measure.enabled IS '[setting] 開關類的值（showResult）';
COMMENT ON COLUMN dbo.vw_inspec_measure.number IS '[setting] 數字類的值：scanInterval 毫秒、leftWidth px（沒拖過是空的）';
COMMENT ON COLUMN dbo.vw_inspec_measure.path IS '[setting] 路徑類的值；還沒選是空的';
COMMENT ON COLUMN dbo.vw_inspec_measure.name IS '[field] 欄位名稱';
COMMENT ON COLUMN dbo.vw_inspec_measure.prompt IS '[field] 對應的 InSpec 提示文字';
COMMENT ON COLUMN dbo.vw_inspec_measure.mode IS '[field] fill＝直接填；confirm＝先確認再填';
COMMENT ON COLUMN dbo.vw_inspec_measure.editable IS '[field] 主頁上可否改';
COMMENT ON COLUMN dbo.vw_inspec_measure.started_text IS '[run] 按下執行的時刻 YYYY-MM-DD HH24:MI:SS';
COMMENT ON COLUMN dbo.vw_inspec_measure.seconds IS '[run] 從按下執行到 InSpec 跑完幾秒，一位小數；還沒跑完是空的';
COMMENT ON COLUMN dbo.vw_inspec_measure.archive IS '[run] 歸檔檔路徑；沒歸檔是空的';
COMMENT ON COLUMN dbo.vw_inspec_measure.note IS '[run] 沒歸檔或出錯時的原因';
COMMENT ON COLUMN dbo.vw_inspec_measure.program_name IS '[run] 程式的檔名（路徑最後一段）';
COMMENT ON COLUMN dbo.vw_inspec_measure.field_values IS '[run] 這次的欄位值，照現在的欄位順序';
COMMENT ON COLUMN dbo.vw_inspec_measure.run_text IS '[run] 按下執行的時刻 MM-DD HH24:MI';
COMMENT ON COLUMN dbo.vw_inspec_measure.program IS '[run] 執行的 .iwp 原檔完整路徑（寫回去整列帶過來）';
COMMENT ON COLUMN dbo.vw_inspec_measure.fields IS '[run] 這次執行的欄位值：欄位名稱 → 值（寫回去整列帶過來）';
COMMENT ON COLUMN dbo.vw_inspec_measure.prompts IS '[run] 每個提示怎麼處理：prompt、value、at（寫回去整列帶過來）';
COMMENT ON COLUMN dbo.vw_inspec_measure.started_at IS '[run] 按下執行的時刻（寫回去整列帶過來）';
COMMENT ON COLUMN dbo.vw_inspec_measure.finished_at IS '[run] iscmd 回來的時刻（InSpec 跑完）；還沒跑完是空的';
COMMENT ON COLUMN dbo.vw_inspec_measure.exit_code IS '[run] iscmd 回傳代碼';
COMMENT ON COLUMN dbo.vw_inspec_measure.export IS '[run] 這次匯出檔的內容';
COMMENT ON COLUMN dbo.vw_inspec_measure.sort IS '[field] 欄位順序；[run] 新的在前';

-- InSpecTool 設定：setting 一項設定一列，field 一個還在的欄位一列（照順序）
CREATE VIEW dbo.vw_inspec_setup AS
SELECT 'setting'::character varying(7) AS type,
       i.id,
       i.setting_key,
       i.enabled,
       i.number,
       i.path,
       NULL::character varying(50) AS name,
       NULL::character varying(255) AS prompt,
       NULL::character varying(7) AS mode,
       NULL::boolean AS editable,
       0::bigint AS sort
  FROM dbo.inspec AS i
 WHERE i.type = 'setting'
UNION ALL
SELECT 'field', i.id, NULL, NULL, NULL, NULL,
       i.name, i.prompt, i.mode, i.editable, i.sort
  FROM dbo.inspec AS i
 WHERE i.type = 'field' AND i.removed_at IS NULL
 ORDER BY type DESC, sort;

COMMENT ON VIEW dbo.vw_inspec_setup IS 'InSpecTool 設定：setting 一項設定一列；field 一個還在的欄位一列，照順序';
COMMENT ON COLUMN dbo.vw_inspec_setup.type IS 'setting＝一項設定；field＝一個欄位';
COMMENT ON COLUMN dbo.vw_inspec_setup.id IS 'inspec 那一列的流水號（寫回去照它換整列）';
COMMENT ON COLUMN dbo.vw_inspec_setup.setting_key IS '[setting] 哪一項（見 inspec.setting_key）';
COMMENT ON COLUMN dbo.vw_inspec_setup.enabled IS '[setting] 開關類的值（showResult）';
COMMENT ON COLUMN dbo.vw_inspec_setup.number IS '[setting] 數字類的值：scanInterval 毫秒';
COMMENT ON COLUMN dbo.vw_inspec_setup.path IS '[setting] 路徑類的值；還沒選是空的';
COMMENT ON COLUMN dbo.vw_inspec_setup.name IS '[field] 欄位名稱';
COMMENT ON COLUMN dbo.vw_inspec_setup.prompt IS '[field] 對應的 InSpec 提示文字';
COMMENT ON COLUMN dbo.vw_inspec_setup.mode IS '[field] fill＝直接填；confirm＝先確認再填';
COMMENT ON COLUMN dbo.vw_inspec_setup.editable IS '[field] 主頁上可否改';
COMMENT ON COLUMN dbo.vw_inspec_setup.sort IS '[field] 欄位順序';
